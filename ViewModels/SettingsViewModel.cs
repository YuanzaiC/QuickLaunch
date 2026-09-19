using System.Collections.ObjectModel;
using QuickLaunch.Models;
using QuickLaunch.Services;

namespace QuickLaunch.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private AppConfig _config;
    public ObservableCollection<string> ScanFolders { get; } = new();
    public ObservableCollection<string> ExcludeFolders { get; } = new();
    public string ExcludeText { get => string.Join(", ", ExcludeFolders); set { ExcludeFolders.Clear(); foreach(var x in value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)) ExcludeFolders.Add(x); OnPropertyChanged(); } }
    public string Hotkey { get => _config.Hotkey; set { _config.Hotkey=value; OnPropertyChanged(); } }
    public bool Recursive { get=>_config.Recursive; set { _config.Recursive=value; OnPropertyChanged(); } }
    public int MaxDepth { get=>_config.MaxDepth; set { _config.MaxDepth=value; OnPropertyChanged(); } }
    public string Theme { get=>_config.Theme; set { _config.Theme=value; OnPropertyChanged(); } }
    public bool StartWithWindows { get=>_config.StartWithWindows; set { _config.StartWithWindows=value; OnPropertyChanged(); } }
    public int MaxRecent { get=>_config.MaxRecent; set { _config.MaxRecent=value; OnPropertyChanged(); } }
    public bool MinimizeToTray { get=>_config.MinimizeToTray; set { _config.MinimizeToTray=value; OnPropertyChanged(); } }
    public string IncludeExtensionsText { get=>string.Join(", ",_config.IncludeExtensions); set { _config.IncludeExtensions=value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(x=>x.StartsWith('.')?x:'.'+x).ToList(); OnPropertyChanged(); } }
    public SettingsViewModel(MainViewModel main, AppConfig config){_main=main;_config=config; foreach(var x in config.ScanFolders)ScanFolders.Add(x);foreach(var x in config.ExcludeFolders)ExcludeFolders.Add(x);}
    public AppConfig BuildConfig()=>new(){ScanFolders=ScanFolders.ToList(),Recursive=Recursive,MaxDepth=Math.Clamp(MaxDepth,0,20),IncludeExtensions=_config.IncludeExtensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),ExcludeFolders=ExcludeFolders.ToList(),Hotkey=Hotkey,Theme=Theme,StartWithWindows=StartWithWindows,MaxRecent=Math.Clamp(MaxRecent,1,200),MinimizeToTray=MinimizeToTray};
    public void AddFolder(string path){if(!ScanFolders.Contains(path,StringComparer.OrdinalIgnoreCase))ScanFolders.Add(path); OnPropertyChanged(nameof(ScanFolders));}
    public void RemoveFolder(string path){ScanFolders.Remove(path); OnPropertyChanged(nameof(ScanFolders));}
    public async Task SaveAsync()=>await _main.SaveConfigAsync(BuildConfig());
}
