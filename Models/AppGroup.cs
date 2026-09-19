using System.Collections.ObjectModel;

namespace QuickLaunch.Models;

public sealed class AppGroup
{
    public string Path { get; init; } = "";
    public string Name => string.IsNullOrWhiteSpace(Path) ? "其他" : new DirectoryInfo(Path).Name;
    public ObservableCollection<AppEntry> Items { get; } = new();
    public bool IsExpanded { get; set; } = true;
}
