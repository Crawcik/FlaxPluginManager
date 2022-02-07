using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace FlaxPlugMan;

public class MainWindow : Window
{
    private const string
        Version = " 1.3",
        ListUrl = "https://raw.githubusercontent.com/Crawcik/FlaxPluginManager/master/plugin_list.json",
        TreeUrl = "https://api.github.com/repos/{0}/git/trees/master?recursive=true",
        GithubUrl = "https://github.com/",
        RawUrl = "https://raw.githubusercontent.com/{0}/{1}/",
        ModuleDependency = "options.PrivateDependencies.Add(\"{0}\");";

    private IReadOnlyList<PluginEntry> _plugins;
    private IReadOnlyList<PluginEntry> _selectedPlugins;
    private ProgressBar _progressBar;
    private CheckBox _gitSupportBox;
    private ListBox _pluginList;
    private PluginListViewModel _pluginListView;
    private Button _selectButton;
    private Button _applyButton;
    private string _currentProjectPath;
    private CancellationTokenSource _cancelToken;

    public MainWindow()
    {
        InitializeComponent();
        GetPluginList().GetAwaiter();
        GitCheckSupport().GetAwaiter();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Label>("Info").Content += Version;
        _progressBar = this.FindControl<ProgressBar>("Progress");
        _pluginList = this.FindControl<ListBox>("PluginList");
        _selectButton = this.FindControl<Button>("SelectButton");
        _applyButton = this.FindControl<Button>("ApplyButton");
        _gitSupportBox = this.FindControl<CheckBox>("GitSupport");
        _progressBar.Value = 0;
        _applyButton.IsEnabled = false;
        _progressBar.IsVisible = false;
        _pluginList.IsEnabled = false;
        if(Program.Args is null || Program.Args.Length == 0)
            return;
        string path = Program.Args[0];
        if(File.Exists(path) && path.EndsWith(".flaxproj"))
            SetProject(path).GetAwaiter();
    }
    
    private async Task GetPluginList()
    {
        using var client = new HttpClient();
        try
        {
#if DEBUG
            var result = await File.ReadAllTextAsync("plugin_list.json");
#else
            var result = await client.GetStringAsync(ListUrl);
#endif
            _plugins = JsonConvert.DeserializeObject<List<PluginEntry>>(result);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        if (_plugins is null)
            return;

        _pluginList.DataContext = _pluginListView = new PluginListViewModel(_plugins);
    }

    private async Task GitCheckSupport()
    {
        var process = StartGitProcess("--version", shell: false);
        await process.WaitForExitAsync();
        if (process.ExitCode == 0)
            _gitSupportBox.IsChecked =_gitSupportBox.IsEnabled = true;
    }

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        _selectButton.IsEnabled = false;
        SelectProject().GetAwaiter();
    }

    private async Task SelectProject()
    {
        var dialog = new OpenFileDialog();
        dialog.Filters.Add(new() { Name = "Flaxproj", Extensions =  { "flaxproj" } });
        dialog.Directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        dialog.AllowMultiple = false;
        dialog.Title = "Select flax project";
        var result = await dialog.ShowAsync(this);
        _selectButton.IsEnabled = true;
        if (result is null)
            return;
        await SetProject(result[0]);
    }

    private async Task SetProject(string path)
    {
        try
        {
            var root = JObject.Parse(await File.ReadAllTextAsync(path));
            foreach(var obj in root["References"])
            {
                var name = (string)obj["Name"];
                var plugin = _plugins.FirstOrDefault(x=>name.Contains(x.ProjectFile));
                if(plugin is not null)
                    plugin.Ui.IsChecked = true;
            }
        }
        catch
        {
#if DEBUG
            Console.WriteLine("Project is invalid");
#else
            await MessageBox.Show(this, "Error", "Project file is invalid!");
#endif
        }
        _currentProjectPath = path;
        this.Title = Path.GetFileName(_currentProjectPath);
        _applyButton.IsEnabled = true;
        _pluginList.IsEnabled = true;
        
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if(((string)_applyButton.DataContext) == "Cancel")
            _cancelToken.Cancel();
        else
            Update().GetAwaiter();
    }

    private async Task Update()
    {
        var fileInfo = new FileInfo(_currentProjectPath);
        _cancelToken = new CancellationTokenSource();
        _applyButton.DataContext = "Cancel";
        try
        {
            Directory.CreateDirectory(Path.Combine(fileInfo.DirectoryName, "Plugins"));
            // Download files
            if (!await TryGitDownload(_cancelToken.Token, fileInfo))
                await TryDirectDownload(_cancelToken.Token, fileInfo);

            // Update project
            _progressBar.IsVisible = false;
            _selectedPlugins = _plugins.Where(x => x.Ui.IsChecked ?? false).ToList();
            var gameTarget = await UpdateFlaxProject();
            await UpdateGameModules(gameTarget, fileInfo);
            
        }
        catch (Exception exception)
        {
#if DEBUG
            Console.WriteLine(exception.ToString());
#else
            await MessageBox.Show(this, "Error", "Updating project files failed! Check if they're valid");
#endif
        }
        _applyButton.DataContext = "Apply";
        _cancelToken = null;
    }

    private async Task<string> UpdateFlaxProject()
    {
        var root = JObject.Parse(await File.ReadAllTextAsync(_currentProjectPath));
        var array = (JArray)root["References"];
        array = new JArray(array.Where(x => !_plugins.Any(y => ((string)x["Name"]).Contains(y.ProjectFile))));
        foreach (var item in _selectedPlugins)
        {
            var token = new JObject();
            token.Add("Name", "$(ProjectPath)/Plugins/" + item.Name + '/' + item.ProjectFile);
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
                    foreach (var item in _plugins)
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
                    foreach (var item in _selectedPlugins)
                    {
                        if (item.ModuleName is null)
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

    private async Task<bool> TryGitDownload(CancellationToken cancellationToken, FileInfo fileInfo)
    {
        if((!_gitSupportBox.IsChecked) ?? true)
            return false;
        var dir = Path.Combine(fileInfo.DirectoryName, "Plugins");
        var gitmoduleFile = Path.Combine(fileInfo.DirectoryName, ".gitmodules");
        var gitmoduleExist = File.Exists(gitmoduleFile);
        bool submodule = false, failedOnce = false;

        // Check if project is repository
        var process = StartGitProcess("status", dir);
        await process.WaitForExitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode == 0)
            submodule = true;
        var count = _plugins.Count(x => x.Ui.IsChecked != Directory.Exists(Path.Combine(dir, x.Name)));
        int done = 0;

        _progressBar.Value = 0d;
        _progressBar.IsVisible = true;
        foreach (var item in _plugins)
        {
            if(count > 0)
                _progressBar.Value = (done * 100) / count;
            var itemDir = Path.Combine(dir, item.Name);
            var itemChecked = item.Ui.IsChecked ?? false;
            if (itemChecked == Directory.Exists(itemDir))
            {
                if (itemChecked && !submodule)
                {
                    process = StartGitProcess("pull", Path.Combine(dir, item.Name));
                    await process.WaitForExitAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (process.ExitCode != 0)
                    {
                        failedOnce = true;
                        item.Ui.IsEnabled = false;
                    }
                }
                continue;
            }

            done++;
            if (itemChecked)
            {
                // Download plugin
                var arg = (submodule ? "submodule add -f" : "clone") + $" {item.Url} \"{item.Name}\"";
                process = StartGitProcess(arg, dir);
                await process.WaitForExitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (process.ExitCode != 0)
                {
                    failedOnce = true;
                    item.Ui.IsChecked = false;
                    item.Ui.IsEnabled = false;
                }
                continue;
            }

            // Delete plugin
            if (submodule && gitmoduleExist)
            {
                
                process = StartGitProcess("submodule deinit -f " + item.Name, dir);
                await process.WaitForExitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                string[] lines = File.ReadAllLines(gitmoduleFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (!line.StartsWith('[') || !line.Contains(item.Name))
                        continue;
                    lines[i] = lines[i+1] = lines[i+2] = null;
                    i += 2;
                }
                File.WriteAllLines(gitmoduleFile, lines.Where(x=>x is not null));
            }
            try
            {
                Directory.Delete(itemDir, true);
            }
            catch
            {
                // Windows is retarded 
            }
        }

        // Update if needed;
        if (submodule)
        {
            process = StartGitProcess("submodule update --recursive", dir);
            await process.WaitForExitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (failedOnce)
            await MessageBox.Show(this, "Warning", "Downloading some plugin files failed!");

        return true;
    }

    // TODO: Version checking
    private async Task TryDirectDownload(CancellationToken cancellationToken, FileInfo fileInfo)
    {
        using var client = new HttpClient();
        var dir = Path.Combine(fileInfo.DirectoryName, "Plugins");
        var count = _plugins.Count(x => x.Ui.IsChecked != Directory.Exists(Path.Combine(dir, x.Name)));
        var failedOnce = false;
        int done = -1;
        client.DefaultRequestHeaders.Add("User-Agent", "request");
        _progressBar.Value = 0d;
        _progressBar.IsVisible = true;
        foreach (var item in _plugins)
        {
            var itemDir = Path.Combine(dir, item.Name);
            var itemChecked = item.Ui.IsChecked ?? false;
            if (itemChecked == Directory.Exists(itemDir))
                continue;
            done++;
            if(count > 0)
                _progressBar.Value = (done * 100) / count;
            if (itemChecked)
            {
                string repoLocation = item.Url.Replace(GithubUrl, null);
                // Download plugin
                HttpResponseMessage response = await client.GetAsync(string.Format(TreeUrl, repoLocation, item.Branch ?? "master"), cancellationToken);
                int status = (int)response.StatusCode;
                if (status != 200 && status != 304)
                {
                    failedOnce = true;
                    item.Ui.IsChecked = false;
                    item.Ui.IsEnabled = false;
                    continue;
                }
                
                var root = JObject.Parse(await response.Content.ReadAsStringAsync());
                var url = string.Format(RawUrl, repoLocation);
                var tree = root["tree"].Where(x => (string)x["type"] == "blob").ToArray();
                var lenght = tree.Count();
                for (int i = 0; i < lenght; i++)
                {
                    var obj = tree[i];
                    var filePath = Path.Combine(itemDir, (string)obj["path"]);
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    try
                    {
                        var fileStream = File.OpenWrite(filePath);
                        var webStream = await client.GetStreamAsync(url + (string)obj["path"], cancellationToken);
                        await webStream.CopyToAsync(fileStream);
                        webStream.Close();
                        fileStream.Close();
                    }
                    catch
                    {
                        failedOnce = true;
                    }
                    var percentage = done / count;
                    percentage += (lenght - i) / (lenght * count);
                    _progressBar.Value = percentage * 100;
                }
                continue;
            }
            // Delete plugin
            try
            {
                Directory.Delete(itemDir, true);
            } 
            catch
            {
                // Windows is retarded 
            }
        }
        if(failedOnce)
            await MessageBox.Show(this, "Warning", "Downloading some plugin files failed!");
    }

    private Process StartGitProcess(string args, string path = "", bool shell = true) => Process.Start(new ProcessStartInfo()
    {
        FileName = "git",
        UseShellExecute = false,
        WorkingDirectory = path,
        Arguments = args
    });
}