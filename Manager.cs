using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlaxPlugMan;

public class Manager
{
	private const string
		ListUrl = "https://raw.githubusercontent.com/Crawcik/FlaxPluginManager/master/plugin_list.json",
		ModuleDependency = "options.PrivateDependencies.Add(\"{0}\");";

	private CancellationTokenSource _cancelToken;

	public IReadOnlyList<PluginEntry> Plugins { get; private set; }
	public string ProjectPath { get; private set; }
	public bool IsProcessActive => _cancelToken is not null;

	public event Action OnDownloadStarted, OnDownloadFinished;

	public static Process StartGitProcess(string args, string path = "", bool stdout = false) => Process.Start(new ProcessStartInfo()
	{
		FileName = "git",
		UseShellExecute = false,
		WorkingDirectory = path,
		RedirectStandardOutput = stdout,
		Arguments = args
	});

	public async Task GetPluginList()
	{
		using var client = new HttpClient();
		try
		{
#if DEBUG
			var result = await File.ReadAllTextAsync("plugin_list.json");
#else
			var result = await client.GetStringAsync(ListUrl);
#endif
			Plugins = JsonConvert.DeserializeObject<List<PluginEntry>>(result);
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}

	public async Task<bool> SetProject(string path)
	{
		ProjectPath = path;
		foreach(var item in Plugins)
			item.Installed = false;
		try
		{
			var root = JObject.Parse(await File.ReadAllTextAsync(path));
			foreach(var obj in root["References"])
			{
				var name = (string)obj["Name"];
				var plugin = Plugins.FirstOrDefault(x=>name.Contains(x.ProjectFile));
				if(plugin is null)
					continue;
				plugin.CheckUi.IsChecked = true;
				plugin.Installed = true;
				plugin.SetPath(path, Path.GetDirectoryName(name));
				var update = await IsUpdateNeeded(plugin);
				plugin.UpdateUi.IsVisible = update;
				plugin.TagUi.IsVisible = !update;
			}
		}
		catch
		{
			await MessageBox.Show(null, "Error", "Project file is invalid!");
			ProjectPath = null;
			return false;
		}
		return true;
	}

	public void CancelAll() => _cancelToken?.Cancel();

	public async Task DownloadAll(bool gitChecked)
	{
		OnDownloadStarted?.Invoke();
		_cancelToken = new CancellationTokenSource();
		var fileInfo = new FileInfo(ProjectPath);
		var allSuccess = true;
		try
		{
			// Download files
			Download downloader = gitChecked ? new GitDownload() : new DirectDownload();
			var dirInfo = Directory.CreateDirectory(Path.Combine(fileInfo.DirectoryName, "Plugins"));
			var lookup = Plugins.Where(x => x.CheckUi.IsChecked != x.Installed).ToLookup(x => x.CheckUi.IsChecked ?? false);
			foreach (var item in lookup[true])
				item.IsGitManaged = gitChecked;
			allSuccess = await downloader.ProcessAll(lookup, dirInfo.FullName, _cancelToken.Token);

			// Update project
			var gameTarget = await UpdateFlaxProject();
			await UpdateGameModules(gameTarget.Item1, fileInfo);
			if (Plugins.Any(x => !string.IsNullOrEmpty(x.EditorModuleName))
			{
				await UpdateGameModules(gameTarget.Item2, fileInfo, true);
			}

			await MessageBox.Show(null, "Info", "Success!");
			
		}
		catch
		{
			await MessageBox.Show(null, "Error", "Updating project files failed! Check if they're valid");
		}
		if(!allSuccess)
			await MessageBox.Show(null, "Error", "Some plugins failed to install");
		_cancelToken = null;
		OnDownloadFinished?.Invoke();
	}

	public async Task UpdatePlugin(PluginEntry item)
	{
		if(ProjectPath is null || !item.Installed || !item.IsGitManaged.HasValue)
			return;
		_cancelToken = new CancellationTokenSource();
		item.UpdateStyle(true);
		OnDownloadStarted.Invoke();
		try
		{
			Download download = item.IsGitManaged.Value ? new GitDownload() : new DirectDownload();
			var success = await download.Update(item, _cancelToken.Token);
			await MessageBox.Show(null, success ? "Info" : "Error", success ? "Success!" : "Updating failed!");
			item.UpdateUi.IsVisible = !success;
			item.TagUi.IsVisible = success;
		}
		catch(Exception exp)
		{
			Console.WriteLine(exp);
		}
		item.UpdateStyle(false);
		OnDownloadFinished.Invoke();
		_cancelToken = null;
		
	}

	private async Task<(string, string)> UpdateFlaxProject()
	{
		var root = JObject.Parse(await File.ReadAllTextAsync(ProjectPath));
		var array = (JArray)root["References"];
		array = new JArray(array.Where(x => !Plugins.Any(y => ((string)x["Name"]).Contains(y.ProjectFile))));
		foreach (var item in Plugins.Where(x => x.Installed))
		{
			if(item.FlaxprojPath is null)
				item.SetPath(ProjectPath, "$(ProjectPath)/Plugins/" + item.Name + '/' + item.ProjectFile);
			var token = new JObject();
			token.Add("Name", item.FlaxprojPath);
			array.Add(token);
		}
		root["References"] = array;
		var str = root.ToString(Formatting.Indented);
		File.WriteAllText(ProjectPath, str);
		return ((string)root["GameTarget"], (string)root["GameTargetEditor"]);
	}

	private async Task UpdateGameModules(string gameTarget, FileInfo fileInfo, bool isEditor = false)
	{
		var path = fileInfo.Directory.ToString();
		var target = gameTarget is null ? "Game" : gameTarget.Replace("Target", null);
		path = Path.Combine(path, "Source", target, target + ".Build.cs");
		if (!File.Exists(path))
			return;
		using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
		using var reader = new StreamReader(stream);
		using var writer = new StreamWriter(stream);

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
				var con = false;
				
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
					foreach (var item in Plugins)
					{
						var moduleName = isEditor ? item.EditorModuleName : item.ModuleName;
						if (string.IsNullOrEmpty(moduleName) || !line.Contains('"' + moduleName + '"'))
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
					foreach (var item in Plugins.Where(x => x.Installed))
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

		stream.Seek(0, SeekOrigin.Begin);
		stream.SetLength(0);
		foreach (var obj in lines)
			await writer.WriteLineAsync(obj);
		writer.Close();
	}

	private async Task<bool> IsUpdateNeeded(PluginEntry item)
	{
		if(ProjectPath is null || !item.Installed)
			return false;
		item.IsGitManaged = !File.Exists(item.VersionPath);
		try
		{
			Download download = item.IsGitManaged.Value ? new GitDownload() : new DirectDownload();
			return await download.CheckForUpdate(item);
			
		}
		catch(Exception exp)
		{
			Console.WriteLine(exp);
			return false;
		}
	}
}