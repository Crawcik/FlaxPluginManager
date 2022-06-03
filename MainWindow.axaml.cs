using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Layout;
using Newtonsoft.Json.Linq;

namespace FlaxPlugMan;

public class MainWindow : Window
{
	private const string 
		Version = " 1.5",
		VersionUrl = "https://api.github.com/repos/Crawcik/FlaxPluginManager/releases/latest";

	private Manager _manager = new ();
	private CheckBox _gitSupportBox;
	private ScrollViewer _pluginListModel;
	private Button _selectButton;
	private Button _applyButton;

	public MainWindow()
	{
		InitializeComponent();
		InitializeAsync().GetAwaiter();
	}

	private async Task InitializeAsync() 
	{
		// Check manager update
		var result =  await new DirectDownload().GetWebString(VersionUrl);
		if(result is not null)
		{
			var versionRemote = JObject.Parse(result)["tag_name"].ToString().Remove(0, 1);
			if(versionRemote != Version.Trim())
				this.FindControl<Label>("Info").Content += " (new version avalible)";
				
		}

		try
		{
			// Check git support
			var process = Manager.StartGitProcess("--version");
			await process.WaitForExitAsync();
			if (process.ExitCode == 0)
				_gitSupportBox.IsChecked =_gitSupportBox.IsEnabled = true;
		}
		catch { }

		// Initialize plugins list & manager	
		await _manager.GetPluginList();
		_manager.OnDownloadStarted += () => OnDownload(true);
		_manager.OnDownloadFinished += () => OnDownload(false);
		_pluginListModel.Content = GetGrid();
		foreach (var item in _manager.Plugins)
			item.UpdateUi.Click += (sender, args) => _manager.UpdatePlugin(item).GetAwaiter();
		if(Program.Args is null || Program.Args.Length == 0)
			return;
		string path = Program.Args[0];
		if(File.Exists(path) && path.EndsWith(".flaxproj"))
			_manager.SetProject(path).GetAwaiter().OnCompleted(() => {
				this.Title = Path.GetFileName(path);
				_applyButton.IsEnabled = true;
				_pluginListModel.IsEnabled = true; 
			});
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
		this.FindControl<Label>("Info").Content += Version;
		_pluginListModel = this.FindControl<ScrollViewer>("PluginList");
		_selectButton = this.FindControl<Button>("SelectButton");
		_applyButton = this.FindControl<Button>("ApplyButton");
		_gitSupportBox = this.FindControl<CheckBox>("GitSupport");
		_applyButton.IsEnabled = false;
		_pluginListModel.IsEnabled = false;
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
		var path = result[0];
		if(!await _manager.SetProject(path))
			return;
		this.Title = Path.GetFileName(path);
		_applyButton.IsEnabled = true;
		_pluginListModel.IsEnabled = true;   
	}

	private void OnApplyClick(object sender, RoutedEventArgs e)
	{
		if(_manager.IsProcessActive)
			_manager.CancelAll();
		else
			_manager.DownloadAll(_gitSupportBox.IsChecked ?? false).GetAwaiter();
	}

	private void OnDownload(bool start)
	{
		_applyButton.Content = start ? "Cancel" : "Apply";
		_selectButton.IsEnabled = !start;
		_gitSupportBox.IsEnabled = !start;
		_pluginListModel.IsEnabled = !start;
	}

	private Grid GetGrid()
	{
		var tagBackground = Brush.Parse("#32417d");
		var grid = new Grid();
		grid.ColumnDefinitions.Add(new () { Width = GridLength.Parse("*") });
		grid.ColumnDefinitions.Add(new () { Width = GridLength.Auto});
		var items = _manager.Plugins;
		for (int i = 0; i < items.Count; i++)
		{
			var item = items[i];
			var isTagNull = !string.IsNullOrEmpty(item.Tag);
			item.CheckUi = new() {
				Content = item.Name,
				FontSize = 16
			};
			item.UpdateUi = new () { 
				Content = "Update",
				IsVisible = false,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center
			};
			item.TagUi = new () { 
				Content = item.Tag,
				IsVisible = true,
				Background = tagBackground,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				CornerRadius = new Avalonia.CornerRadius(25),
				Margin = new Avalonia.Thickness(8)
			};
			grid.RowDefinitions.Add(new () { Height = GridLength.Auto });
			grid.Children.Add(item.CheckUi);
			grid.Children.Add(item.UpdateUi);
			if (isTagNull)
				grid.Children.Add(item.TagUi);
			Grid.SetColumn(item.CheckUi, 0);
			Grid.SetRow(item.CheckUi, i);
			Grid.SetColumn(item.UpdateUi, 1);
			Grid.SetRow(item.UpdateUi, i);
			if (isTagNull)
			{
				Grid.SetColumn(item.TagUi, 1);
				Grid.SetRow(item.TagUi, i);
			}
			ToolTip.SetTip(item.CheckUi, item.Description);
		}
		return grid;
	}
}