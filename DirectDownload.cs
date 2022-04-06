namespace FlaxPlugMan;

public class DirectDownload : Download
{
	public override async Task<bool> DownloadAll(ILookup<bool, PluginEntry> plugins, string path, CancellationToken token)
	{
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "request");
        foreach (var item in plugins[true])
        {
            string repoLocation = item.Url.Replace(Manager.GithubUrl, null);
        }
        foreach (var item in plugins[false])
        {
            
        }
        return true;
		/*using var client = new HttpClient();
        var dir = Path.Combine(fileInfo.DirectoryName, "Plugins");
        var count = plugins.Count(x => x.CheckUi.IsChecked != Directory.Exists(Path.Combine(dir, x.Name)));
        var failedOnce = false;
        int done = -1;
        client.DefaultRequestHeaders.Add("User-Agent", "request");
        _progressBar.Value = 0d;
        _progressBar.IsVisible = true;
        foreach (var item in _plugins)
        {
            var itemDir = Path.Combine(dir, item.Name);
            var itemChecked = item.CheckUi.IsChecked ?? false;
            if (itemChecked == Directory.Exists(itemDir))
                continue;
            done++;
            if(count > 0)
                _progressBar.Value = (done * 100) / count;
            if (itemChecked)
            {
                string repoLocation = item.Url.Replace(GithubUrl, null);
                // Download plugin
                HttpResponseMessage response = await client.GetAsync(string.Format(TreeUrl, repoLocation, item.Branch ?? "master") + "?recursive=true", cancellationToken);
                int status = (int)response.StatusCode;
                if (status != 200 && status != 304)
                {
                    failedOnce = true;
                    item.CheckUi.IsChecked = false;
                    item.CheckUi.IsEnabled = false;
                    continue;
                }
                
                var root = JObject.Parse(await response.Content.ReadAsStringAsync());
                var url = string.Format(RawUrl, repoLocation);
                var tree = root["tree"].Where(x => (string)x["type"] == "blob").ToArray();
                var lenght = tree.Count();
                for (int i = 0; i < lenght; i++)
                {
                    var obj = tree[i];
                    var filePath = Path.Combine(itemDir, (string)obj["path"]);
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    try
                    {
                        var fileStream = File.OpenWrite(filePath);
                        var webStream = await client.GetStreamAsync(url + (string)obj["path"], cancellationToken);
                        await webStream.CopyToAsync(fileStream);
                        webStream.Close();
                        fileStream.Close();
                    }
                    catch
                    {
                        failedOnce = true;
                    }
                    var percentage = done / count;
                    percentage += (lenght - i) / (lenght * count);
                    _progressBar.Value = percentage * 100;
                }
                continue;
            }
            // Delete plugin
            try
            {
                Directory.Delete(itemDir, true);
            } 
            catch
            {
                // Windows is retarded 
            }
        }
        if(failedOnce)
            await MessageBox.Show(this, "Warning", "Downloading some plugin files failed!");
            */
	}

	public override Task<bool> DownloadPlugin(PluginEntry plugin, CancellationToken token)
	{
		throw new NotImplementedException();
	}
}