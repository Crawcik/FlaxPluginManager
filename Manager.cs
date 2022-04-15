using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlaxPlugMan;

public class Manager
{
	public const string
        ListUrl = "https://raw.githubusercontent.com/Crawcik/FlaxPluginManager/master/plugin_list.json",
        TreeUrl = "https://api.github.com/repos/{0}/git/trees/{1}",
        GithubUrl = "https://github.com/",
        RawUrl = "https://raw.githubusercontent.com/{0}/{1}/",
        ModuleDependency = "options.PrivateDependencies.Add(\"{0}\");";

    private IReadOnlyList<PluginEntry> _failedPlugins;
	private CancellationTokenSource _cancelToken;

	public IReadOnlyList<PluginEntry> Plugins { get; private set; }
	public string ProjectPath { get; private set; }

    public event Action OnDownloadStarted, OnDownloadFinished;

    public static Process StartGitProcess(string args, string path = "") => Process.Start(new ProcessStartInfo()
    {
        FileName = "git",
        UseShellExecute = false,
        WorkingDirectory = path,
        Arguments = args
    });

    public async Task GetPluginList()
    {
        using var client = new HttpClient();
        try
        {
#if DEBUG
            var result = await File.ReadAllTextAsync("plugin_list.json");
#else
            var result = await client.GetStringAsync(ListUrl);
#endif
            Plugins = JsonConvert.DeserializeObject<List<PluginEntry>>(result);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

	public async Task<bool> SetProject(string path)
    {
        foreach(var item in Plugins)
            item.Installed = false;
        try
        {
            var root = JObject.Parse(await File.ReadAllTextAsync(path));
            foreach(var obj in root["References"])
            {
                var name = (string)obj["Name"];
                var plugin = Plugins.FirstOrDefault(x=>name.Contains(x.ProjectFile));
                if(plugin is null)
                    continue;
                plugin.CheckUi.IsChecked = true;
                plugin.Installed = true;
                plugin.SetPath(path, name);
                plugin.UpdateUi.IsVisible = await IsUpdateNeeded(plugin);
                ProjectPath = path;
            }
        }
        catch
        {
#if DEBUG
            Console.WriteLine("Project is invalid");
#else
            await MessageBox.Show(this, "Error", "Project file is invalid!");
#endif
            ProjectPath = null;
            return false;
        }
        return true;
    }

	public async Task DownloadAll(bool gitChecked)
    {
        OnDownloadStarted?.Invoke();
        var fileInfo = new FileInfo(ProjectPath);
        _cancelToken = new CancellationTokenSource();
        try
        {
            // Download files
            Download downloader = gitChecked ? new GitDownload() : new DirectDownload();
            var dirInfo = Directory.CreateDirectory(Path.Combine(fileInfo.DirectoryName, "Plugins"));
            var lookup = Plugins.Where(x=>x.CheckUi.IsChecked != x.Installed).ToLookup(x=>x.CheckUi.IsChecked ?? false);
            await downloader.ProcessAll(lookup, dirInfo.FullName, _cancelToken.Token);

            // Update project
            var gameTarget = await UpdateFlaxProject();
            await UpdateGameModules(gameTarget, fileInfo);
            await MessageBox.Show(null, "Info", "Success!");
            
        }
        catch (Exception exception)
        {
#if DEBUG
            Console.WriteLine(exception.ToString());
#else
            await MessageBox.Show(this, "Error", "Updating project files failed! Check if they're valid");
#endif
        }
        
        _cancelToken = null;
        OnDownloadFinished?.Invoke();
    }

    private async Task<string> UpdateFlaxProject()
    {
        var root = JObject.Parse(await File.ReadAllTextAsync(ProjectPath));
        var array = (JArray)root["References"];
        array = new JArray(array.Where(x => !Plugins.Any(y => ((string)x["Name"]).Contains(y.ProjectFile))));
        foreach (var item in Plugins.Where(x => x.Installed))
        {
            if(item.FlaxprojPath is null)
                item.SetPath(ProjectPath, "$(ProjectPath)/Plugins/" + item.Name + '/' + item.ProjectFile);
            var token = new JObject();
            token.Add("Name", item.FlaxprojPath);
            array.Add(token);
        }
        root["References"] = array;
        var str = root.ToString(Formatting.Indented);
#if DEBUG
        Console.WriteLine(str);
#else
        File.WriteAllText(_currentProjectPath, str);
#endif
        return (string)root["GameTarget"];
    }

    private async Task UpdateGameModules(string gameTarget, FileInfo fileInfo)
    {
        var path = fileInfo.Directory.ToString();
        var target = gameTarget is null ? "Game" : gameTarget.Replace("Target", null);
        path = Path.Combine(path, "Source", target, target + ".Build.cs");
        if (!File.Exists(path))
            return;
        using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        using var reader = new StreamReader(stream);
#if !DEBUG
        using var writer = new StreamWriter(stream);
#endif

        // Read and arrange
        var lines = new List<string>();
        int startLineNum = 0, lineNum = 0;
        var line = string.Empty;
        bool platformDepSwitch = false;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            // Finding function pos
            if (startLineNum == 0)
            {
                if (line.Contains("public override void Setup(BuildOptions options)"))
                    startLineNum = lineNum + (line.Contains('{') ? 1 : 2);
            }
            else
            {
                bool con = false;
                
                // Finding old dependencies
                if (line.Contains("Do not remove this comment (FlaxPlugMan)"))
                    platformDepSwitch = true;
                if(platformDepSwitch)
                {
                    con = true;
                    platformDepSwitch = !line.Contains("}");
                }
                else
                {
                    foreach (var item in Plugins)
                    {
                        if (string.IsNullOrEmpty(item.ModuleName) || !line.Contains('"' + item.ModuleName + '"'))
                            continue;
                        con = true;
                        break;
                    }
                }
                if (con)
                    continue;

                // Adding new dependencies
                if (startLineNum == lineNum)
                {
                    var platformSpecific = new Dictionary<string, List<string>>();
                    foreach (var item in Plugins)
                    {
                        if (item.ModuleName is null || _failedPlugins.Contains(item))
                            continue;
                        if (item.Platforms is null)
                        {
                            lines.Add("\t\t" + string.Format(ModuleDependency, item.ModuleName));
                            continue;
                        }
                        foreach(var str in item.Platforms)
                        {
                            if(!platformSpecific.ContainsKey(str))
                                platformSpecific.Add(str, new List<string>());
                            platformSpecific[str].Add(item.ModuleName);
                        }
                    }
                    if (platformSpecific.Count > 0)
                    {
                        lines.Add("\t\tswitch (options.Platform.Target) // Do not remove this comment (FlaxPlugMan)");
                        lines.Add("\t\t{");
                        foreach (var pair in platformSpecific)
                        {
                            lines.Add("\t\t\tcase TargetPlatform." + pair.Key + ":");
                            foreach(var moduleName in pair.Value)
                                lines.Add("\t\t\t\t" + string.Format(ModuleDependency, moduleName));
                            lines.Add("\t\t\t\tbreak;");
                        }
                        lines.Add("\t\t}");
                    }
                }
                            
            }
            lines.Add(line);
            lineNum++;
        }

        // Write to file
#if !DEBUG
        stream.Seek(0, SeekOrigin.Begin);
        stream.SetLength(0);
#endif
        foreach (var tmp in lines)
#if DEBUG
            Console.WriteLine(tmp);
#else
            await writer.WriteLineAsync(tmp);
#endif
#if !DEBUG
        writer.Close();
#endif
    }

    private async Task<bool> IsUpdateNeeded(PluginEntry item)
    {
        if(ProjectPath is null || !item.Installed)
            return false;
        string path = item.VersionPath;
        string local = null, remote = null;
        item.IsGitManaged = null;
        try
        {
            if(File.Exists(path))
            {
                //Direct version
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "request");
                string repoLocation = item.Url.Replace(GithubUrl, null);
                // Download plugin
                HttpResponseMessage response = await client.GetAsync(string.Format(TreeUrl, repoLocation, item.Branch ?? "master") + "?recursive=true");
                var root = JObject.Parse(await response.Content.ReadAsStringAsync());
                int status = (int)response.StatusCode;
                if (status == 200 || status == 304)
                {
                    local = await File.ReadAllTextAsync(path);
                    remote = (string)root["sha"];
                }
                item.IsGitManaged = true;
            }
            else
            {
                //Git version
                var process = StartGitProcess("rev-parse " + item.Branch ?? "master");
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                if(process.ExitCode == 0)
                {
                    item.IsGitManaged = false;
                    local = output;
                    process = StartGitProcess("rev-parse origin/" + item.Branch ?? "master");
                    await process.WaitForExitAsync();
                    if(process.ExitCode == 0)
                        remote = output;
                }
            }
        }
        catch(Exception exp)
        {
            Console.WriteLine(exp);
            return false;
        }
        return local != remote;
    }

    private async Task SaveVersion(PluginEntry item)
    {
        if(!item.Installed || (item.IsGitManaged ?? true))
            return;
        await File.WriteAllTextAsync(item.VersionPath, item.CurrentVersion);
    }
}