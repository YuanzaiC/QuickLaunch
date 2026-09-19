using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using QuickLaunch.Helpers;
using QuickLaunch.Models;
using QuickLaunch.Services;

namespace QuickLaunch.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ConfigService _configService;
    private readonly PersistenceService _persistence;
    private readonly LaunchService _launcher;
    private readonly ScannerService _scanner;
    private readonly FileWatcherService _watcher;
    private readonly HotkeyService _hotkey;
    private readonly StartupService _startup;
    private readonly PinyinSearchService _pinyin = new();
    private readonly DispatcherTimer _searchDebounce;
    private CancellationTokenSource? _scanCts;
    private AppConfig _config = new();
    private string _query = "";
    private string _status = "正在加载缓存…";
    private bool _isScanning;
    private double _scanProgress;
    private AppEntry? _selected;
    private WindowInteropHelperAdapter? _hotkeyHelper;
    private HashSet<string> _hiddenIds = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<AppEntry> AllItems { get; } = new();
    public ObservableCollection<AppEntry> FilteredItems { get; } = new();
    public ObservableCollection<AppEntry> Favorites { get; } = new();
    public ObservableCollection<AppEntry> Recents { get; } = new();

    public string Query { get => _query; set { if (SetProperty(ref _query, value)) { _searchDebounce.Stop(); _searchDebounce.Start(); } } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsScanning { get => _isScanning; private set => SetProperty(ref _isScanning, value); }
    public double ScanProgress { get => _scanProgress; private set => SetProperty(ref _scanProgress, value); }
    public AppEntry? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public string HotkeyText => _config.Hotkey;

    /// <summary>收起面板时是否在通知区域保留托盘图标（供主窗口同步托盘与任务栏状态）。</summary>
    public bool MinimizeToTray => _config.MinimizeToTray;

    public RelayCommand LaunchCommand { get; }
    public RelayCommand AdminLaunchCommand { get; }
    public RelayCommand ToggleFavoriteCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand CopyPathCommand { get; }
    public RelayCommand RenameCommand { get; }
    public RelayCommand HideCommand { get; }
    public RelayCommand RemoveRecentCommand { get; }
    public RelayCommand CleanBrokenCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand CancelScanCommand { get; }

    public event Action? RequestShow;
    public event Action? RequestHide;

    public MainViewModel(ConfigService configService, PersistenceService persistence, LaunchService launcher, ScannerService scanner, FileWatcherService watcher, HotkeyService hotkey, StartupService startup)
    {
        _configService=configService; _persistence=persistence; _launcher=launcher; _scanner=scanner; _watcher=watcher; _hotkey=hotkey; _startup=startup;
        LaunchCommand = new RelayCommand(p => Launch(p as AppEntry));
        AdminLaunchCommand = new RelayCommand(p => Launch(p as AppEntry, true));
        ToggleFavoriteCommand = new RelayCommand(p => ToggleFavorite(p as AppEntry));
        OpenFolderCommand = new RelayCommand(p => { if (p is AppEntry x) _launcher.OpenContainingFolder(x); });
        CopyPathCommand = new RelayCommand(p => { if (p is AppEntry x) System.Windows.Clipboard.SetText(x.Path); });
        RenameCommand = new RelayCommand(p => Rename(p as AppEntry));
        HideCommand = new RelayCommand(p => HideItem(p as AppEntry));
        RemoveRecentCommand = new RelayCommand(p => RemoveRecent(p as AppEntry));
        CleanBrokenCommand = new RelayCommand(_ => CleanBroken());
        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
        CancelScanCommand = new RelayCommand(_ => _scanCts?.Cancel(), _ => IsScanning);
        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); ApplyFilter(); };
        _watcher.RefreshRequested += () => System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = RefreshAsync()));
        _hotkey.Pressed += () => System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => RequestShow?.Invoke()));
    }

    public async Task InitializeAsync(WindowInteropHelperAdapter helper)
    {
        _config = await _configService.LoadAsync();
        var favorites = await _persistence.LoadFavoritesAsync();
        var recentRecords = await _persistence.LoadRecentsAsync();
        _hiddenIds = await _persistence.LoadHiddenAsync();
        var cache = await _persistence.LoadCatalogCacheAsync();
        ApplyRecords(cache, favorites, recentRecords);
        Status = $"已加载缓存 · {AllItems.Count} 个项目";
        _watcher.Configure(_config.ScanFolders);
        _startup.SetEnabled(_config.StartWithWindows);
        _hotkeyHelper=helper;
        var registered=helper.Register(_hotkey, _config.Hotkey);
        if(!registered) Status=$"热键 {HotkeyText} 注册失败，可能已被其他软件占用";
        ThemeService.Apply(_config.Theme);
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (IsScanning) return;
        _scanCts?.Cancel(); _scanCts?.Dispose(); _scanCts = new CancellationTokenSource();
        IsScanning = true; ScanProgress = 0; Status = "准备扫描…";
        try
        {
            var progress = new Progress<(int Current,int Total,string Status)>(p => { ScanProgress = p.Total == 0 ? 100 : p.Current * 100d / p.Total; Status = p.Status; });
            var old = AllItems.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            var items = await _scanner.ScanToListAsync(_config, progress, _scanCts.Token);
            foreach (var i in items)
                if (old.TryGetValue(i.Id, out var prev)) { i.LaunchCount=prev.LaunchCount; i.LastUsedUtc=prev.LastUsedUtc; i.IsFavorite=prev.IsFavorite; i.IsHidden=prev.IsHidden; }
            AllItems.Clear();
            foreach (var item in items.Where(x=>!x.IsHidden && !_hiddenIds.Contains(x.Id)).OrderByDescending(x=>x.IsFavorite).ThenByDescending(x=>x.LastUsedUtc).ThenByDescending(x=>x.LaunchCount).ThenBy(x=>x.SortOrder).ThenBy(x=>x.Name)) AllItems.Add(item);
            BuildSearchIndex(AllItems); ApplyFilter(); RebuildFavoritesAndRecents();
            await _persistence.SaveCatalogCacheAsync(AllItems);
            await _persistence.SaveFavoritesAsync(Favorites);
            await _persistence.SaveRecentsAsync(AllItems);
            Status = $"扫描完成 · {AllItems.Count} 个项目";
            _watcher.Configure(_config.ScanFolders);
        }
        catch (OperationCanceledException) { Status = "扫描已取消"; }
        catch (Exception ex) { LogService.Error("扫描失败", ex); Status = $"扫描失败：{ex.Message}"; }
        finally { IsScanning=false; ScanProgress=100; CancelScanCommand.RaiseCanExecuteChanged(); }
    }

    private void ApplyRecords(List<AppEntry> items, HashSet<string> favoriteIds, List<PersistedItemRecord> recentRecords)
    {
        var recordMap = recentRecords.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        AllItems.Clear();
        foreach (var item in items)
        {
            if (recordMap.TryGetValue(item.Id, out var r)) { item.LaunchCount=r.LaunchCount; item.LastUsedUtc=r.LastUsedUtc; item.IsHidden=r.Hidden; }
            item.IsHidden = item.IsHidden || _hiddenIds.Contains(item.Id);
            item.IsFavorite = favoriteIds.Contains(item.Id) || item.IsFavorite;
            if (!item.IsHidden && !_hiddenIds.Contains(item.Id)) AllItems.Add(item);
        }
        ApplyFilter(); RebuildFavoritesAndRecents();
    }

    private void BuildSearchIndex(IEnumerable<AppEntry> items)
    {
        Parallel.ForEach(items, item => { item.Pinyin=_pinyin.GetPinyin(item.Name); item.PinyinInitials=_pinyin.GetInitials(item.Name); });
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        var q = Query.Trim();
        var scored = string.IsNullOrWhiteSpace(q) ? AllItems.Select(x => (x, 1d)) : AllItems.Select(x => (x, Math.Max(Math.Max(FuzzyMatcher.Score(q, x.Name, x.Pinyin), FuzzyMatcher.Score(q, x.Name, x.PinyinInitials)), FuzzyMatcher.Score(q, x.Path, x.Pinyin)))).Where(x=>x.Item2>0);
        foreach (var pair in scored.OrderByDescending(x=>x.Item2).ThenByDescending(x=>x.x.IsFavorite).ThenByDescending(x=>x.x.LastUsedUtc).ThenBy(x=>x.x.SortOrder).Take(300)) FilteredItems.Add(pair.x);
        OnPropertyChanged(nameof(FilteredItems));
    }

    private void RebuildFavoritesAndRecents()
    {
        Favorites.Clear(); foreach (var x in AllItems.Where(x=>x.IsFavorite).OrderBy(x=>x.Name)) Favorites.Add(x);
        Recents.Clear(); foreach (var x in AllItems.Where(x=>x.LastUsedUtc.HasValue).OrderByDescending(x=>x.LastUsedUtc).Take(_config.MaxRecent)) Recents.Add(x);
    }

    private void Launch(AppEntry? item, bool admin=false)
    {
        if (item is null) return;
        if (item.IsBroken && !admin) { AppDialog.Warn($"快捷方式已失效：\n{item.Path}"); return; }
        if (admin && !AppDialog.Confirm($"将以管理员身份运行“{item.Name}”。\nWindows 仍可能显示 UAC 确认。", "以管理员身份运行")) return;
        if (_launcher.Launch(item, admin))
        {
            item.LaunchCount++; item.LastUsedUtc=DateTime.UtcNow;
            RebuildFavoritesAndRecents();
            _ = _persistence.SaveRecentsAsync(AllItems);
            _ = _persistence.SaveFavoritesAsync(Favorites);
            _ = _persistence.SaveCatalogCacheAsync(AllItems);
            RequestHide?.Invoke();
        }
    }

    private void ToggleFavorite(AppEntry? item)
    {
        if (item is null) return; item.IsFavorite=!item.IsFavorite; RebuildFavoritesAndRecents(); _ = _persistence.SaveFavoritesAsync(Favorites); ApplyFilter();
    }

    private void Rename(AppEntry? item)
    {
        if (item is null) return;
        var input = SimpleInputDialog.Show("重命名", item.Name);
        if (string.IsNullOrWhiteSpace(input) || input == item.Name) return;
        try { var newPath = Path.Combine(Path.GetDirectoryName(item.Path)!, input + item.Extension); File.Move(item.Path, newPath); _ = RefreshAsync(); } catch(Exception ex) { AppDialog.Error(ex.Message, "重命名失败"); }
    }

    private void HideItem(AppEntry? item)
    {
        if (item is null) return;
        item.IsHidden=true;
        _hiddenIds.Add(item.Id);
        AllItems.Remove(item);
        FilteredItems.Remove(item);
        RebuildFavoritesAndRecents();
        _ = _persistence.SaveCatalogCacheAsync(AllItems);
        _ = _persistence.SaveHiddenAsync(_hiddenIds);
        _ = _persistence.SaveFavoritesAsync(Favorites);
    }

    private void RemoveRecent(AppEntry? item)
    {
        if (item is null) return; item.LastUsedUtc=null; item.LaunchCount=0; RebuildFavoritesAndRecents(); _= _persistence.SaveRecentsAsync(AllItems); ApplyFilter();
    }

    private void CleanBroken()
    {
        var broken = AllItems.Where(x=>x.IsBroken).ToList();
        foreach (var x in broken) { try { File.Delete(x.Path); } catch { } }
        _ = RefreshAsync();
    }

    public void RestoreHidden()
    {
        _hiddenIds.Clear();
        _ = _persistence.SaveHiddenAsync(_hiddenIds);
        _ = RefreshAsync();
    }

    public void MoveItemToGroup(AppEntry item, AppEntry target)
    {
        if (item.Id.Equals(target.Id, StringComparison.OrdinalIgnoreCase)) return;
        item.CustomGroup = target.DisplayGroup;
        item.SortOrder = target.SortOrder + 1;
        SortAllItems();
        ApplyFilter();
        _ = _persistence.SaveCatalogCacheAsync(AllItems);
    }

    private void SortAllItems()
    {
        var ordered=AllItems.OrderByDescending(x=>x.IsFavorite).ThenByDescending(x=>x.LastUsedUtc).ThenByDescending(x=>x.LaunchCount).ThenBy(x=>x.DisplayGroup).ThenBy(x=>x.SortOrder).ThenBy(x=>x.Name).ToList();
        AllItems.Clear(); foreach(var x in ordered) AllItems.Add(x);
    }

    public AppConfig GetConfigSnapshot() => new()
    {
        ScanFolders=_config.ScanFolders.ToList(), Recursive=_config.Recursive, MaxDepth=_config.MaxDepth, IncludeExtensions=_config.IncludeExtensions.ToList(), ExcludeFolders=_config.ExcludeFolders.ToList(), Hotkey=_config.Hotkey, Theme=_config.Theme, StartWithWindows=_config.StartWithWindows, MaxRecent=_config.MaxRecent, MinimizeToTray=_config.MinimizeToTray
    };

    public async Task SaveConfigAsync(AppConfig config)
    {
        if(_hotkeyHelper!=null)
        {
            _hotkeyHelper.Unregister(_hotkey);
            _config=config;
            var registered=_hotkeyHelper.Register(_hotkey,_config.Hotkey);
            if(!registered) Status=$"热键 {_config.Hotkey} 注册失败，可能已被其他软件占用";
        }
        else _config=config;
        await _configService.SaveAsync(_config);
        _startup.SetEnabled(_config.StartWithWindows);
        _watcher.Configure(_config.ScanFolders);
        ThemeService.Apply(_config.Theme);
        OnPropertyChanged(nameof(HotkeyText));
        OnPropertyChanged(nameof(MinimizeToTray));
        await RefreshAsync();
    }

    public void Dispose() { _scanCts?.Cancel(); _scanCts?.Dispose(); _watcher.Dispose(); _hotkey.Dispose(); }
}

public sealed class WindowInteropHelperAdapter
{
    private readonly System.Windows.Interop.WindowInteropHelper _helper;
    public WindowInteropHelperAdapter(Window window) { _helper=new System.Windows.Interop.WindowInteropHelper(window); }
    public void EnsureHandle() { _ = _helper.EnsureHandle(); }
    public bool Register(HotkeyService service, string hotkey) { return service.Register(_helper, hotkey); }
    public void Unregister(HotkeyService service) { service.Unregister(_helper); }
}
