using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Layout;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace FlaxPlugMan;

public class MainWindow : Window
{
	private const string 
		Version = " 1.6",
		VersionApi = "https://api.github.com/repos/Crawcik/FlaxPluginManager/releases/latest",
		LatestUrl = "https://github.com/Crawcik/FlaxPluginManager/releases/latest";

	private Manager _manager;
	private ProgressBar _progressBar;
	private CheckBox _gitSupportBox;
	private ScrollViewer _pluginListModel;
	private Button _selectButton;
	private Button _applyButton;

	public MainWindow()
	{
		_manager = new();
		InitializeComponent();
		InitializeAsync().GetAwaiter();
	}

	private async Task InitializeAsync() 
	{
		// Check manager update
		var result =  await new DirectDownload().GetWebString(VersionApi);
		if(result is not null)
		{
			var versionRemote = JObject.Parse(result)["tag_name"].ToString().Remove(0, 1);
			if(versionRemote != Version.Trim())
			{
				var info = this.FindControl<TextBlock>("Info");
				info.TextDecorations = TextDecorations.Underline;
				info.Text += " (new version avalible)";
				info.Tapped += (sender, ev) => Process.Start(Program.GetOSCommand(), LatestUrl);

			}
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
		_manager.UpdateProgress += percentage => _progressBar.Value = percentage;
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
		this.FindControl<TextBlock>("Info").Text += Version;
		_pluginListModel = this.FindControl<ScrollViewer>("PluginList");
		_progressBar = this.FindControl<ProgressBar>("Progress");
		_selectButton = this.FindControl<Button>("SelectButton");
		_applyButton = this.FindControl<Button>("ApplyButton");
		_gitSupportBox = this.FindControl<CheckBox>("GitSupport");
		_progressBar.IsVisible = false;
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
		_progressBar.IsVisible = start;
	}

	private Grid GetGrid()
	{
		var tagBackground = Brush.Parse("#32417d");
		var grid = new Grid();
		grid.ColumnDefinitions.Add(new () { Width = GridLength.Star });
		grid.ColumnDefinitions.Add(new () { Width = GridLength.Auto});
		grid.ColumnDefinitions.Add(new () { Width = GridLength.Auto});
		var items = _manager.Plugins;
		var geometry = (Geometry)Resources["link_square_regular"];
		for (int i = 0; i < items.Count; i++)
		{
			var item = items[i];
			var isTagNull = string.IsNullOrEmpty(item.Tag);
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
			var link = new PathIcon() {
				Data = geometry
			};
			link.Tapped += item.OpenPage;
			
			grid.RowDefinitions.Add(new () { Height = GridLength.Auto });
			grid.Children.Add(item.CheckUi);
			grid.Children.Add(link);
			grid.Children.Add(item.UpdateUi);
			if (!isTagNull)
				grid.Children.Add(item.TagUi);
			Grid.SetColumn(item.CheckUi, 0);
			Grid.SetRow(item.CheckUi, i);
			Grid.SetColumn(link, 1);
			Grid.SetRow(link, i);
			Grid.SetColumn(item.UpdateUi, 2);
			Grid.SetRow(item.UpdateUi, i);
			if (!isTagNull)
			{
				Grid.SetColumn(item.TagUi, 2);
				Grid.SetRow(item.TagUi, i);
			}
			ToolTip.SetTip(item.CheckUi, item.Description);
		}
		return grid;
	}
}