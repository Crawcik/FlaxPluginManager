using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace FlaxPlugMan;

public class MainWindow : Window
{
    private const string Version = " 1.3";

    private Manager _manager = new ();
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
#if DEBUG
        MessageBox.Show(null, "Warning", "Program is in DEBUG mode. Project files will not be updated!");
#endif
        GitCheckSupport().GetAwaiter();
        _manager.GetPluginList().GetAwaiter().GetResult();
        _manager.OnDownloadStarted += () =>_applyButton.DataContext = "Cancel";
        _manager.OnDownloadStarted += () => _applyButton.DataContext = "Apply";
        _pluginList.DataContext = _pluginListView = new PluginListViewModel(_manager.Plugins);
        if(Program.Args is null || Program.Args.Length == 0)
            return;
        string path = Program.Args[0];
        if(File.Exists(path) && path.EndsWith(".flaxproj"))
            _manager.SetProject(path).GetAwaiter();
    }

    public void UpdateManually(PluginEntry plugin)
    {
        if (plugin.Installed)
        {
            
        }
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
    }

    private async Task GitCheckSupport()
    {
        var process = Manager.StartGitProcess("--version");
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
        if(!await _manager.SetProject(result[0]))
            return;
        _currentProjectPath = result[0];
        this.Title = Path.GetFileName(_currentProjectPath);
        _applyButton.IsEnabled = true;
        _pluginList.IsEnabled = true;   
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if(((string)_applyButton.DataContext) == "Cancel")
            _cancelToken.Cancel();
        else
            _manager.DownloadAll(_gitSupportBox.IsChecked ?? false).GetAwaiter();
    }

}