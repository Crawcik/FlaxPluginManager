namespace FlaxPlugMan;

public class GitDownload : Download
{

	public override async Task<bool> ProcessAll(ILookup<bool, PluginEntry> plugins, string path, CancellationToken token)
	{
        // Repo presence check
        var process = Manager.StartGitProcess("status", path);
        await process.WaitForExitAsync(token);
        token.ThrowIfCancellationRequested();
        var submodule = process.ExitCode == 0;
		var gitmoduleFile = Path.Combine(path, ".gitmodules");
        if(!File.Exists(gitmoduleFile))
            gitmoduleFile = Path.Combine(path, "..", ".gitmodules");
        if(!File.Exists(gitmoduleFile) || !submodule)
            gitmoduleFile = null;
        
        var allSuccess = true;
        foreach (var item in plugins[false])
        {
            if(token.IsCancellationRequested)
				break;
            await RemovePlugin(item, path, gitmoduleFile, token);
            item.Installed = false;
        }
        foreach (var item in plugins[true])
        {
            if(token.IsCancellationRequested)
				break;
            if(!await AddPlugin(item, path, submodule, token))
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

        // Repo presence check
		var process = Manager.StartGitProcess("status", path);
        await process.WaitForExitAsync(token);
        token.ThrowIfCancellationRequested();
        var submodule = process.ExitCode == 0;
		var gitmoduleFile = Path.Combine(path, ".gitmodules");
        if(!File.Exists(gitmoduleFile))
            gitmoduleFile = Path.Combine(path, "..", ".gitmodules");
        if(!File.Exists(gitmoduleFile) || !submodule)
            gitmoduleFile = null;

		if(isChecked)
			return plugin.Installed = await AddPlugin(plugin, plugPath, submodule, token);
		await RemovePlugin(plugin, path, gitmoduleFile, token);
		return true;
	}

    private async Task<bool> AddPlugin(PluginEntry plugin, string path, bool submodule, CancellationToken token)
    {
        var arg = (submodule ? "submodule add -f" : "clone") + $" {plugin.Url} \"{plugin.Name}\"";
        try
        {
            var process = Manager.StartGitProcess(arg, path);
            await process.WaitForExitAsync(token);
            token.ThrowIfCancellationRequested();
            
            return process.ExitCode == 0;
        
        } catch { return false; }
    }

    private async Task RemovePlugin(PluginEntry plugin, string path, string gitmoduleFile, CancellationToken token)
    {
        if (gitmoduleFile is not null)
        {
            var process = Manager.StartGitProcess("submodule deinit -f " + plugin.Name, path);
            await process.WaitForExitAsync(token);
            token.ThrowIfCancellationRequested();

            string[] lines = File.ReadAllLines(gitmoduleFile);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!line.StartsWith('[') || !line.Contains(plugin.Name))
                    continue;
                lines[i] = lines[i+1] = lines[i+2] = null;
                i += 2;
            }
            File.WriteAllLines(gitmoduleFile, lines.Where(x => x is not null));
        }
        try
        {
            Directory.Delete(Path.Combine(path, plugin.Name), true);
        }
        catch
        {
            // Windows is retarded 
        }
    }
}