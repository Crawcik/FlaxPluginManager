namespace FlaxPlugMan;

public abstract class Download
{
	private List<PluginEntry> _failedPlugins;

	public IReadOnlyList<PluginEntry> FailedPlugins => _failedPlugins;

	public abstract Task<bool> DownloadAll(ILookup<bool, PluginEntry> plugins, string path, CancellationToken token);
	public abstract Task<bool> DownloadPlugin(PluginEntry plugin, CancellationToken token);
}