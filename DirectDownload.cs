using Newtonsoft.Json.Linq;

namespace FlaxPlugMan;

public class DirectDownload : Download
{
	private HttpClient _web = new HttpClient() {
		DefaultRequestHeaders = {
			{"User-Agent", "request"}
		}	
	};

	public override async Task<bool> ProcessAll(ILookup<bool, PluginEntry> plugins, string path, CancellationToken token)
	{
		var allSuccess = true;
		foreach (var item in plugins[false])
		{
			if(token.IsCancellationRequested)
				break;
			RemovePlugin(Path.Combine(path, item.Name));
			item.Installed = false;
		}
		foreach (var item in plugins[true])
		{
			if(token.IsCancellationRequested)
				break;
			if(!await AddPlugin(item, Path.Combine(path, item.Name), token))
			{
				item.CheckUi.IsEnabled = false;
				allSuccess = false;
				continue;
			}
			item.Installed = true;
		}
		return allSuccess;
	}

	public override async Task<bool> CheckForUpdate(PluginEntry plugin)
	{
		var repoLocation = plugin.Url.Replace(Manager.GithubUrl, null);
		var result =  await GetWebString(string.Format(Manager.TreeUrl, repoLocation, plugin.Branch ?? "master"));
		if(result is null)
			return false;

		var root = JObject.Parse(result);
		plugin.CurrentVersion = await File.ReadAllTextAsync(plugin.VersionPath);
		var remote = (string)root["sha"];
		return plugin.CurrentVersion != remote; // local != remote
	}

	public override async Task<bool> ProcessPlugin(PluginEntry plugin, string path, CancellationToken token)
	{
		var plugPath = Path.Combine(path, plugin.Name);
		var isChecked = plugin.CheckUi.IsChecked ?? false;
		if(plugin.Installed && isChecked)
			return false;
		if(isChecked)
			return plugin.Installed = await AddPlugin(plugin, plugPath, token);
		RemovePlugin(plugPath);
		return true;
	}

	private async Task<bool> AddPlugin(PluginEntry plugin, string path,  CancellationToken token)
	{
		var repoLocation = plugin.Url.Replace(Manager.GithubUrl, null);
		var apiList = string.Format(Manager.TreeUrl, repoLocation, plugin.Branch ?? "master") + "?recursive=true";

		try
		{
			var root = JObject.Parse(await GetWebString(apiList, token));
			var url = string.Format(Manager.RawUrl, repoLocation, plugin.Branch ?? "master");
			var tree = root["tree"].Where(x => (string)x["type"] == "blob");
			foreach (var obj in tree)
			{
				var repoPath = (string)obj["path"];
				var filePath = Path.Combine(path, repoPath);
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				var webStream = await GetWebStream(url + repoPath, token);
				var fileStream = File.OpenWrite(filePath);
				await webStream.CopyToAsync(fileStream);
				webStream.Close();
				fileStream.Close();
			}
			await File.WriteAllTextAsync(Path.Combine(path, ".plugin-version"), (string)root["sha"], token);
		}
		catch { return false; }
		return true;
	}

	private void RemovePlugin(string path)
	{
		try
		{
			Directory.Delete(path, true);
		} 
		catch
		{
			// Windows is retarded 
		}
	}

	private async Task<Stream> GetWebStream(string uri, CancellationToken cancellationToken = default)
	{
		var request = await _web.GetAsync(uri, cancellationToken);
		var status = (int)request.StatusCode;
		return status == 200 || status == 304 ? await request.Content.ReadAsStreamAsync() : null;
	}

	private async Task<string> GetWebString(string uri, CancellationToken cancellationToken = default)
	{
		var request = await _web.GetAsync(uri, cancellationToken);
		var status = (int)request.StatusCode;
		return status == 200 || status == 304 ? await request.Content.ReadAsStringAsync() : null;
	}
}