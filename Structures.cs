using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace FlaxPlugMan;

public record PluginEntry(string name, string description, string url, string moduleName, string projectFile)
{
    public CheckBox ui { get; set; }
}

public class PluginListViewModel
{
    public ObservableCollection<CheckBox> Items { get; }
    public PluginListViewModel(IEnumerable<PluginEntry> items)
    {
        Items = new();
        foreach (PluginEntry item in items)
        {
            CheckBox checkBox = new() { Content = item.name };
            if(!OperatingSystem.IsLinux())
                ToolTip.SetTip(checkBox, item.description);
            Items.Add(checkBox);
        }
    }
}