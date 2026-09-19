using System.Text.Json.Serialization;
using System.Windows.Media;

namespace QuickLaunch.Models;

public sealed class AppEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string LaunchPath { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string GroupPath { get; set; } = "";
    public string CustomGroup { get; set; } = "";
    public int SortOrder { get; set; }
    public string Extension { get; set; } = "";
    public bool IsShortcut { get; set; }
    public bool IsBroken { get; set; }
    public long Size { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public int LaunchCount { get; set; }
    public DateTime? LastUsedUtc { get; set; }

    [JsonIgnore] public string DisplayGroup => string.IsNullOrWhiteSpace(CustomGroup) ? GroupPath : CustomGroup;
    [JsonIgnore] public string Pinyin { get; set; } = "";
    [JsonIgnore] public string PinyinInitials { get; set; } = "";
    [JsonIgnore] public ImageSource? Icon { get; set; }
    [JsonIgnore] public bool IsFavorite { get; set; }
    [JsonIgnore] public bool IsHidden { get; set; }
}

public sealed class PersistedItemRecord
{
    public string Id { get; set; } = "";
    public int LaunchCount { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public bool IsFavorite { get; set; }
    public bool Hidden { get; set; }
}
