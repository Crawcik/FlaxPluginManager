using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;

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
    private IReadOnlyList<PluginEntry> _unselectedPlugins;
    private ProgressBar _progress;
    private ListBox _pluginList;
    private PluginListViewModel _pluginListView;
    private Button _selectButton;
    private Button _applyButton;
    private string _currentProjectPath;

    public MainWindow()
    {
        InitializeComponent();
        GetPluginList().GetAwaiter();
    } 

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Label>("Info").Content += Version;
        _progress = this.FindControl<ProgressBar>("Progress");
        _pluginList = this.FindControl<ListBox>("PluginList");
        _selectButton = this.FindControl<Button>("SelectButton");
        _applyButton = this.FindControl<Button>("ApplyButton");
        _progress.Value = 0;
        _progress.IsVisible = false;
        _applyButton.IsEnabled = false;
        _pluginList.IsEnabled = false;
    }
    
    private async Task GetPluginList()
    {
        using HttpClient client = new();
        try
        {
#if DEBUG
            string result = await File.ReadAllTextAsync("plugin_list.json");
#else
            string result = await client.GetStringAsync(ListUrl);
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

    private void OnSelectClick(object? sender, RoutedEventArgs e)
    {
        _selectButton.IsEnabled = false;
        SelectProject().GetAwaiter();
    }

    private async Task SelectProject()
    {
        OpenFileDialog dialog = new();
        dialog.Filters.Add(new() { Name = "Flaxproj", Extensions =  { "flaxproj" } });
        dialog.Directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        dialog.AllowMultiple = false;
        dialog.Title = "Select flax project";
        string[] result = await dialog.ShowAsync(this);
        _selectButton.IsEnabled = true;
        if (result is null)
            return;
        _currentProjectPath = result[0];
        this.Title = Path.GetFileName(_currentProjectPath);
        _applyButton.IsEnabled = true;
        _pluginList.IsEnabled = true;
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        _applyButton.IsEnabled = false;
        Update().GetAwaiter();
    }

    private async Task Update()
    {
        var fileInfo = new FileInfo(_currentProjectPath);
        _selectedPlugins = _plugins.Where(x => x.Ui.IsChecked ?? false).ToList();
        try
        {
            var gameTarget = await UpdateFlaxProject();
            await UpdateGameModules(gameTarget, fileInfo);
        }
        catch(Exception exception)
        {
            Console.WriteLine(exception.ToString());
        }
        _applyButton.IsEnabled = true;
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
        if(!File.Exists(path))
            return;
        using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream);

        // Read and arrange
        List<string> lines = new List<string>();
        int startLineNum = 0, lineNum = 0;
        var line = string.Empty;
        while((line = await reader.ReadLineAsync()) is not null)
        {
            // Finding function pos
            if(line.Contains("public override void Setup(BuildOptions options)"))
                startLineNum = lineNum + (line.Contains('{') ? 1 : 2);
            if(startLineNum != 0)
            {
                bool con = false;
                // Finding old dependencies
                foreach (var item in _plugins)
                {
                    if(string.IsNullOrEmpty(item.ModuleName) || !line.Contains('"' + item.ModuleName + '"'))
                        continue;
                    con = true;
                    break;
                }
                if(con)
                    continue;

                // Adding new dependencies
                if(startLineNum == lineNum)
                    foreach (var item in _selectedPlugins)
                        if(!string.IsNullOrEmpty(item.ModuleName))
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
}