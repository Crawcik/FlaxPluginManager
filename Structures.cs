using System.Collections.Generic;
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
            item.ui = new() { Content = item.name };
            if(!OperatingSystem.IsLinux())
                ToolTip.SetTip(item.ui, item.description);
            Items.Add(item.ui);
        }
    }
}