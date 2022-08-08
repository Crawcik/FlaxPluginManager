using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace FlaxPlugMan;

public record PluginEntry(string Name, string Description, string Tag, string Url, string Branch, string ModuleName, string ProjectFile, string[] Platforms)
{
	private bool _installed = false;

	public CheckBox CheckUi { get; set; }    
	public Button UpdateUi { get; set; }
	public Label TagUi { get; set; }
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
		pluginPath = pluginPath.Replace( ProjectFile, null);
		if(pluginPath.Contains("$(ProjectPath)"))
			pluginPath = pluginPath.Replace("$(ProjectPath)", Path.GetDirectoryName(projectPath));
		VersionPath = Path.Combine(pluginPath, ".plugin-version");
	}

	public void UpdateStyle(bool start)
	{
		UpdateUi.Content = start ? "Updating..." : "Update";
		UpdateUi.Background = start ? Brushes.Gray : null;
		UpdateUi.Foreground = start ? Brushes.White : null;
		UpdateUi.IsEnabled = !start;
	}

	public void OpenPage(object sender, RoutedEventArgs e)
	{
		var command = Program.GetOSCommand();
		if (command is null)
			return;
		Process.Start(command, Url);
	}
}