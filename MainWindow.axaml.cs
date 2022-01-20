using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace FlaxPlugMan;

public class MainWindow : Window
{
    private const string
        Version = " 1.2",
        ListUrl = "https://raw.githubusercontent.com/Crawcik/FlaxPluginManager/master/plugin_list.json",
        GameModulePath = "/Source/{0}/{0}.Build.cs",
        ModuleDependency = "        options.PrivateDependencies.Add(\"{0}\");";

    private IReadOnlyList<PluginEntry> _plugins;
    private IReadOnlyList<PluginEntry> _selectedPlugins;
    private ProgressBar _progressBar;
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
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Label>("Info").Content += Version;
        _progressBar = this.FindControl<ProgressBar>("Progress");
        _pluginList = this.FindControl<ListBox>("PluginList");
        _selectButton = this.FindControl<Button>("SelectButton");
        _applyButton = this.FindControl<Button>("ApplyButton");
        _progressBar.Value = 0;
        _applyButton.IsEnabled = false;
        _progressBar.IsVisible = false;
        _pluginList.IsEnabled = false;
        if(Program.Args is null || Program.Args.Length == 0)
            return;
        string path = Program.Args[0];
        if(File.Exists(path) && path.EndsWith(".flaxproj"))
            SetProject(path);
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
        SetProject(result[0]);
    }

    private void SetProject(string path)
    {
        _currentProjectPath = path;
        this.Title = Path.GetFileName(_currentProjectPath);
        _applyButton.IsEnabled = true;
        _pluginList.IsEnabled = true;
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if(_applyButton.DataContext == "Cancel")
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
            {
                return;
            }

            // Update project
            _progressBar.IsVisible = false;
            _selectedPlugins = _plugins.Where(x => x.Ui.IsChecked ?? false).ToList();
            var gameTarget = await UpdateFlaxProject();
            await UpdateGameModules(gameTarget, fileInfo);
            
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.ToString());
        }
        _applyButton.DataContext = "Apply";
        _cancelToken = null;
    }

    private async Task<string> UpdateFlaxProject()
    {
        var root = JObject.Parse(await File.ReadAllTextAsync(_currentProjectPath));
        var array = JArray.Parse(root["References"].ToString());
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
        path += string.Format(GameModulePath, gameTarget is null ? "Game" : gameTarget.Replace("Target", null));
        if (!File.Exists(path))
            return;
        using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream);

        // Read and arrange
        List<string> lines = new List<string>();
        int startLineNum = 0, lineNum = 0;
        var line = string.Empty;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            // Finding function pos
            if (line.Contains("public override void Setup(BuildOptions options)"))
                startLineNum = lineNum + (line.Contains('{') ? 1 : 2);
            if (startLineNum != 0)
            {
                bool con = false;
                // Finding old dependencies
                foreach (var item in _plugins)
                {
                    if (string.IsNullOrEmpty(item.ModuleName) || !line.Contains('"' + item.ModuleName + '"'))
                        continue;
                    con = true;
                    break;
                }
                if (con)
                    continue;

                // Adding new dependencies
                if (startLineNum == lineNum)
                    foreach (var item in _selectedPlugins)
                        if (!string.IsNullOrEmpty(item.ModuleName))
                            lines.Add(string.Format(ModuleDependency, item.ModuleName));
            }
            lines.Add(line);
            lineNum++;
        }

        // Write to file
        stream.Seek(0, SeekOrigin.Begin);
        stream.SetLength(0);
        foreach (var tmp in lines)
#if DEBUG
            Console.WriteLine(tmp);
#else
            await writer.WriteLineAsync(tmp);
#endif
    
        writer.Close();
    }

    private async Task<bool> TryGitDownload(CancellationToken cancellationToken, FileInfo fileInfo)
    {
        var dir = Path.Combine(fileInfo.DirectoryName, "Plugins");
        var gitmoduleFile = Path.Combine(fileInfo.DirectoryName, ".gitmodules");
        var gitmoduleExist = File.Exists(gitmoduleFile);
        var submodule = false;

        //Check if git exist
        var process = StartGitProcess("--version", shell: false);
        await process.WaitForExitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            return false;

        // Check if project is repository
        process = StartGitProcess("status", dir);
        await process.WaitForExitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode == 0)
            submodule = true;

        var count = _plugins.Count(x=>x.Ui.IsChecked == Directory.Exists(Path.Combine(dir, x.Name)));
        int done = 0;

        _progressBar.Value = 0d;
        _progressBar.IsVisible = true;
        foreach (var item in _plugins)
        {
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
                        item.Ui.IsEnabled = false;
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

                if(process.ExitCode != 0)
                {
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
            Directory.Delete(itemDir, true);
        }

        // Update if needed;
        if (submodule)
        {
            process = StartGitProcess("submodule update --recursive", dir);
            await process.WaitForExitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return true;
    }

    private Process StartGitProcess(string args, string path = "", bool shell = true) => Process.Start(new ProcessStartInfo()
    {
        FileName = "git",
        UseShellExecute = shell,
        WorkingDirectory = path,
        Arguments = args
    });
}