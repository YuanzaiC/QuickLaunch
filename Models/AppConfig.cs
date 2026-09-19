namespace QuickLaunch.Models;

public sealed class AppConfig
{
    public List<string> ScanFolders { get; set; } = new();
    public bool Recursive { get; set; } = true;
    public int MaxDepth { get; set; } = 3;
    public List<string> IncludeExtensions { get; set; } = new() { ".exe", ".lnk" };
    public List<string> ExcludeFolders { get; set; } = new();
    public string Hotkey { get; set; } = "Alt+Space";
    public string Theme { get; set; } = "system";
    public bool StartWithWindows { get; set; }
    public int MaxRecent { get; set; } = 30;

    /// <summary>最小化（收起面板）时是否在通知区域显示托盘图标；关闭后只靠快捷键呼出。</summary>
    public bool MinimizeToTray { get; set; } = true;
}
