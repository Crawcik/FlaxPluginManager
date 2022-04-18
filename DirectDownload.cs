using Newtonsoft.Json.Linq;

namespace FlaxPlugMan;

public class DirectDownload : Download
{
	private const string
		TreeUrl = "https://api.github.com/repos/{0}/git/trees/{1}",
		CompareUrl = "https://api.github.com/repos/{0}/compare/{1}...{2}",
		GithubUrl = "https://github.com/",
		RawUrl = "https://raw.githubusercontent.com/{0}/{1}/";

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

	public override async Task<bool> CheckForUpdate(PluginEntry plugin)
	{
		var repoLocation = plugin.Url.Replace(GithubUrl, null);
		var result =  await GetWebString(string.Format(TreeUrl, repoLocation, plugin.Branch ?? "master"));
		if(result is null)
			return false;

		var root = JObject.Parse(result);
		plugin.CurrentVersion = (await File.ReadAllTextAsync(plugin.VersionPath)).TrimEnd('\r','\n');
		var remote = (string)root["sha"];
		return plugin.CurrentVersion != remote; // local != remote
	}

	public override async Task<bool> Update(PluginEntry plugin, CancellationToken token)
	{
		var dir = Path.GetDirectoryName(plugin.VersionPath);
		var repoLocation = plugin.Url.Replace(GithubUrl, null);
		var result =  await GetWebString(string.Format(CompareUrl, repoLocation, plugin.CurrentVersion, plugin.Branch ?? "master"), token);
		if(result is null)
			return false;

		var root = JObject.Parse(result);
		foreach (var item in root["files"])
		{
			try 
			{
				var filename = Path.Combine(dir, (string)item["filename"]);
				switch ((string)item["status"])
				{
					case "added" or "modified":
						if(!await WriteFromStream((string)item["raw_url"], filename, token))
							return false;
						break;
					case "removed": 
						File.Delete(filename); //Windows is retarded, might crash
						break;
					case "renamed":
						File.Move((string)item["previous_filename"], filename, true);
						if((int)item["changes"] == 0)
							break;
						if(!await WriteFromStream((string)item["raw_url"], filename, token))
							return false;
						break;
				}
			} catch { return false; }
		}
		var sha = (string)root["commits"].Last["sha"];
		await File.WriteAllTextAsync(plugin.VersionPath, sha, token);
		return true;
	}

	private async Task<bool> AddPlugin(PluginEntry plugin, string path, CancellationToken token)
	{
		var repoLocation = plugin.Url.Replace(GithubUrl, null);
		var apiList = string.Format(TreeUrl, repoLocation, plugin.Branch ?? "master") + "?recursive=true";

		try
		{
			var root = JObject.Parse(await GetWebString(apiList, token));
			var url = string.Format(RawUrl, repoLocation, plugin.Branch ?? "master");
			var tree = root["tree"].Where(x => (string)x["type"] == "blob");
			foreach (var obj in tree)
			{
				var repoPath = (string)obj["path"];
				var filePath = Path.Combine(path, repoPath);
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				if(!await WriteFromStream(url + repoPath, filePath, token))
					return false;
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
		catch { /* Windows is retarded */}
	}

	private async Task<bool> WriteFromStream(string url, string filename, CancellationToken cancellationToken = default)
	{
		var request = await _web.GetAsync(url, cancellationToken);
		var status = (int)request.StatusCode;
		if (status != 200 && status != 304)
			return false;
		var webStream = await request.Content.ReadAsStreamAsync(cancellationToken);
		var fileStream = File.OpenWrite(filename);
		await webStream.CopyToAsync(fileStream, cancellationToken);
		webStream.Close();
		fileStream.Close();
		return true;
	}

	private async Task<string> GetWebString(string url, CancellationToken cancellationToken = default)
	{
		var request = await _web.GetAsync(url, cancellationToken);
		var status = (int)request.StatusCode;
		return status == 200 || status == 304 ? await request.Content.ReadAsStringAsync() : null;
	}
}