namespace FlaxPlugMan;

public abstract class Download
{
	public abstract Task<bool> ProcessAll(ILookup<bool, PluginEntry> plugins, string path, CancellationToken token);
	public abstract Task<bool> ProcessPlugin(PluginEntry plugin, string path, CancellationToken token);
}