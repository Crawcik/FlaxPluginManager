﻿using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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

	public MainWindow()
	{
		InitializeComponent();
		InitializeAsync().GetAwaiter();
	}

	private async Task InitializeAsync() 
	{
#if DEBUG
		await MessageBox.Show(null, "Warning", "Program is in DEBUG mode. Project files will not be updated!");
#endif
		await GitCheckSupport();
		await _manager.GetPluginList();
		_manager.OnDownloadStarted += () => OnDownload(true);
		_manager.OnDownloadFinished += () => OnDownload(false);
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
		var path = result[0];
		if(!await _manager.SetProject(path))
			return;
		this.Title = Path.GetFileName(path);
		_applyButton.IsEnabled = true;
		_pluginList.IsEnabled = true;   
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
		_pluginList.IsEnabled = !start;
	}
}