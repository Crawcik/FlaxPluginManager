using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;

namespace FlaxPlugMan;

public record PluginEntry(string Name, string Description, string Url, string Branch, string ModuleName, string ProjectFile, string[] Platforms)
{
	private bool _installed = false;

	public CheckBox CheckUi { get; set; }    
	public Button UpdateUi { get; set; }
	public bool Installed
	{
		get => _installed;
		set 
		{
			_installed = value;
			if(value == true)
				return;
			IsGitManaged = null;
			CurrentVersion = null;
			VersionPath = null;
			FlaxprojPath = null;
		}
	}
	public bool? IsGitManaged { get; set; }
	public string CurrentVersion { get; set; }
	public string VersionPath { get; private set; }
	public string FlaxprojPath { get; private set; }

	public void SetPath(string projectPath, string pluginPath)
	{
		FlaxprojPath = pluginPath;
		if(pluginPath.Contains("$(ProjectPath)"))
		{
			pluginPath.Replace("$(ProjectPath)", Path.GetDirectoryName(projectPath));
		}
		VersionPath = Path.Combine(pluginPath, ".plugin-version");
	}
}

public class PluginListViewModel
{
	public ObservableCollection<Grid> Items { get; }
	public PluginListViewModel(IEnumerable<PluginEntry> items)
	{
		Items = new();
		foreach (PluginEntry item in items)
		{
			var grid = new Grid();
			item.CheckUi = new() { Content = item.Name };
			item.UpdateUi = new () { Content = "Update", IsVisible = false };
			item.UpdateUi.Click += (sender, e) => UpdatePlugin(item);
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Parse("*") });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto});
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			grid.Children.Add(item.CheckUi);
			grid.Children.Add(item.UpdateUi);
			Grid.SetColumn(item.CheckUi, 0);
			Grid.SetColumn(item.UpdateUi, 1);
			ToolTip.SetTip(item.CheckUi, item.Description);
			Items.Add(grid);
		}
	}

	public void UpdatePlugin(PluginEntry item)
	{
		item.UpdateUi.Content = "Updating...";
		item.UpdateUi.Background = Brushes.Gray;
		item.UpdateUi.Foreground = Brushes.White;
		item.UpdateUi.IsEnabled = false;
	}
}