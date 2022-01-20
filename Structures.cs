using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace FlaxPlugMan;

public record PluginEntry(string Name, string Description, string Url, string ModuleName, string ProjectFile)
{
    public CheckBox Ui { get; set; }
}

public class PluginListViewModel
{
    public ObservableCollection<CheckBox> Items { get; }
    public PluginListViewModel(IEnumerable<PluginEntry> items)
    {
        Items = new();
        foreach (PluginEntry item in items)
        {
            item.Ui = new() { Content = item.Name };
            if(!OperatingSystem.IsLinux())
                ToolTip.SetTip(item.Ui, item.Description);
            Items.Add(item.Ui);
        }
    }
}