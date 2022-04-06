namespace FlaxPlugMan;

public class GitDownload : Download
{
	public override Task<bool> DownloadAll(ILookup<bool, PluginEntry> plugins, string path, CancellationToken token)
	{
		var gitmoduleFile = Path.Combine(path, "..", ".gitmodules");
        var gitmoduleExist = File.Exists(gitmoduleFile);
        bool submodule = false, failedOnce = false;

        // Check if project is repository
        /*var process = StartGitProcess("status", dir);
        await process.WaitForExitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode == 0)
            submodule = true;
        var count = _plugins.Count(x => x.CheckUi.IsChecked != Directory.Exists(Path.Combine(dir, x.Name)));
        int done = 0;

        _progressBar.Value = 0d;
        _progressBar.IsVisible = true;
        foreach (var item in _plugins)
        {
            if(count > 0)
                _progressBar.Value = (done * 100) / count;
            var itemDir = Path.Combine(dir, item.Name);
            var itemChecked = item.CheckUi.IsChecked ?? false;
            item.SetPath(null, itemDir);
            item.IsGitManaged = true;
            if (itemChecked == Directory.Exists(itemDir))
            {
                if (itemChecked && !submodule)
                {
                    process = StartGitProcess("pull", Path.Combine(dir, item.Name));
                    await process.WaitForExitAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (process.ExitCode != 0)
                    {
                        failedOnce = true;
                        item.CheckUi.IsEnabled = false;
                    }
                }
                item.Installed = true;
                continue;
            }
            done++;
            if (itemChecked)
            {
                // Download plugin
                var arg = (submodule ? "submodule add -f" : "clone") + $" {item.Url} \"{item.Name}\"";
                process = StartGitProcess(arg, dir);
                await process.WaitForExitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (process.ExitCode != 0)
                {
                    failedOnce = true;
                    item.CheckUi.IsChecked = false;
                    item.CheckUi.IsEnabled = false;
                }
                item.Installed = true;
                continue;
            }

            // Delete plugin
            if (submodule && gitmoduleExist)
            {
                
                process = StartGitProcess("submodule deinit -f " + item.Name, dir);
                await process.WaitForExitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                string[] lines = File.ReadAllLines(gitmoduleFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (!line.StartsWith('[') || !line.Contains(item.Name))
                        continue;
                    lines[i] = lines[i+1] = lines[i+2] = null;
                    i += 2;
                }
                File.WriteAllLines(gitmoduleFile, lines.Where(x=>x is not null));
            }
            try
            {
                Directory.Delete(itemDir, true);
            }
            catch
            {
                // Windows is retarded 
            }
        }

        // Update if needed;
        if (submodule)
        {
            process = StartGitProcess("submodule update --recursive", dir);
            await process.WaitForExitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        if (failedOnce)
            await MessageBox.Show(null, "Warning", "Downloading some plugin files failed!");

        return true;*/
	}

	public override Task<bool> DownloadPlugin(PluginEntry plugin, CancellationToken token)
	{
		throw new NotImplementedException();
	}
}