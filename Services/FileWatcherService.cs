namespace QuickLaunch.Services;

public sealed class FileWatcherService : IDisposable
{
    private readonly ScannerService _scanner;
    private readonly List<FileSystemWatcher> _watchers = new();
    private System.Threading.Timer? _debounce;
    private readonly object _gate = new();
    public event Action? RefreshRequested;
    public FileWatcherService(ScannerService scanner) { _scanner = scanner; }
    public void Configure(IEnumerable<string> folders)
    {
        DisposeWatchers();
        foreach (var folder in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var w = new FileSystemWatcher(folder) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite, IncludeSubdirectories = true, Filter = "*" };
                FileSystemEventHandler handler = (_, __) => Debounce();
                RenamedEventHandler rh = (_, __) => Debounce();
                w.Created += handler; w.Deleted += handler; w.Changed += handler; w.Renamed += rh; w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }
            catch { }
        }
    }
    private void Debounce()
    {
        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = new System.Threading.Timer(_ => RefreshRequested?.Invoke(), null, 350, Timeout.Infinite);
        }
    }
    private void DisposeWatchers()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
    }
    public void Dispose() { DisposeWatchers(); _debounce?.Dispose(); }
}
