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
        ListUrl = "https://raw.githubusercontent.com/Crawcik/FlaxPluginManager/master/plugin_list.json";

    private IReadOnlyList<PluginEntry> Plugins;

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
            Plugins = JsonConvert.DeserializeObject<List<PluginEntry>>(await client.GetStringAsync(ListUrl));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        if (Plugins is null)
            return;

        _pluginList.DataContext = _pluginListView = new PluginListViewModel(Plugins);
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
        await UpdateFlaxProject();
        _applyButton.IsEnabled = true;
    }

    private async Task UpdateFlaxProject()
    {
        var root = JObject.Parse(await File.ReadAllTextAsync(_currentProjectPath));
        var selectedItems = Plugins.Where(x => x.ui.IsChecked ?? false).ToArray();
        var array = JArray.Parse(root["References"].ToString());
        foreach (var item in selectedItems)
        {
            var token = new JObject();
            token.Add("Name", "$(ProjectPath)/Plugins/" + item.name + '/' + item.projectFile);
            array.Add(token);
        }
        root["References"] = array;
        var str = root.ToString(Formatting.Indented);
        File.WriteAllText(_currentProjectPath, str);
    }
}