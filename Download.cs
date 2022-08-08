namespace FlaxPlugMan;

public abstract class Download
{
	protected Action<double> Progress {private set; get;}

	public abstract Task<bool> ProcessAll(ILookup<bool, PluginEntry> plugins, string path, CancellationToken token);
	public abstract Task<bool> CheckForUpdate(PluginEntry plugin);
	public abstract Task<bool> Update(PluginEntry plugin,  CancellationToken token);

	public void SetProgressDelegate(Action<double> action) => Progress = action;
}