using Newtonsoft.Json.Linq;

namespace FlaxPlugMan;

public class DirectDownload : Download
{
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

	private async Task<bool> AddPlugin(PluginEntry plugin, string path,  CancellationToken token)
	{
		using var client = new HttpClient();
		client.DefaultRequestHeaders.Add("User-Agent", "request");
		var repoLocation = plugin.Url.Replace(Manager.GithubUrl, null);
		var apiList = string.Format(Manager.TreeUrl, repoLocation, plugin.Branch ?? "master") + "?recursive=true";

		try
		{
			var response = await client.GetAsync(apiList, token);
			var status = (int)response.StatusCode;
			if (status != 200 && status != 304)
				return false;
			
			var root = JObject.Parse(await response.Content.ReadAsStringAsync());
			var url = string.Format(Manager.RawUrl, repoLocation);
			var tree = root["tree"].Where(x => (string)x["type"] == "blob").ToArray();
			var lenght = tree.Count();
			for (int i = 0; i < lenght; i++)
			{
				var obj = tree[i];
				var filePath = Path.Combine(path, (string)obj["path"]);
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				var webStream = await client.GetStreamAsync(url + (string)obj["path"], token);
				var fileStream = File.OpenWrite(filePath);
				await webStream.CopyToAsync(fileStream);
				webStream.Close();
				fileStream.Close();
			}
			await File.WriteAllTextAsync(Path.Combine(path, ".plugin-version"), root["sha"].ToString());
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
}