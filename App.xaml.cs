using System.Text.Json;
using System.Windows;
using QuickLaunch.Services;
using QuickLaunch.ViewModels;

namespace QuickLaunch;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;
    private MainWindow? _window;
    private AppBootstrapper? _bootstrapper;
    private bool _systemEventsHooked;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _singleInstance = new SingleInstanceService("QuickLaunch.Singleton.8F2C30A4");
            if (!_singleInstance.TryAcquire())
            {
                Shutdown();
                return;
            }

            // 在创建任何窗口之前先把主题令牌准备好，避免首帧出现没有配色的闪烁。
            ThemeService.Apply(PeekConfiguredTheme());
            HookSystemThemeChanges();

            _bootstrapper = new AppBootstrapper();
            _window = new MainWindow(_bootstrapper.MainViewModel);
            MainWindow = _window;

            // 先以全透明显示（完成初始化）再隐藏，这样启动时不会有一闪而过的面板。
            _window.Opacity = 0;
            _window.Show();
            _window.HidePanel();
            _window.Opacity = 1;
        }
        catch (Exception ex)
        {
            LogStartupException("OnStartup", ex);
            System.Windows.MessageBox.Show(BuildExceptionText(ex), "QuickLaunch 启动失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    /// <summary>轻量读取 config.json 里的主题，早于 ViewModel 初始化使用。</summary>
    private static string PeekConfiguredTheme()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (!File.Exists(path)) return "system";

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var name in new[] { "theme", "Theme" })
            {
                if (doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString() ?? "system";
            }
        }
        catch
        {
            // 配置损坏时按跟随系统处理。
        }

        return "system";
    }

    /// <summary>系统切换浅色/深色时，跟随系统的主题要实时刷新。</summary>
    private void HookSystemThemeChanges()
    {
        try
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _systemEventsHooked = true;
        }
        catch
        {
            // 没有消息泵等特殊情况，忽略即可。
        }
    }

    private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(new Action(ThemeService.OnSystemThemeChanged));
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogStartupException("DispatcherUnhandledException", e.Exception);
        System.Windows.MessageBox.Show(BuildExceptionText(e.Exception), "QuickLaunch 未处理异常", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) LogStartupException("AppDomainUnhandledException", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogStartupException("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void LogStartupException(string stage, Exception ex)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {stage}\r\n{ex}\r\n\r\n");
        }
        catch { }
    }

    private static string BuildExceptionText(Exception ex)
        => $"{ex.GetType().FullName}\r\n\r\n{ex.Message}\r\n\r\n详细堆栈：\r\n{ex.StackTrace}\r\n\r\n日志：{Path.Combine(AppContext.BaseDirectory, "logs", "startup.log")}";

    protected override void OnExit(ExitEventArgs e)
    {
        if (_systemEventsHooked)
        {
            try { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; } catch { }
            _systemEventsHooked = false;
        }

        _bootstrapper?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

public sealed class AppBootstrapper : IDisposable
{
    public MainViewModel MainViewModel { get; }

    public AppBootstrapper()
    {
        var baseDir = AppContext.BaseDirectory;
        var config = new ConfigService(baseDir);
        var persistence = new PersistenceService(baseDir);
        var launcher = new LaunchService();
        var iconCache = new IconCacheService(baseDir);
        var scanner = new ScannerService(iconCache, launcher);
        var watcher = new FileWatcherService(scanner);
        var hotkey = new HotkeyService();
        var startup = new StartupService();

        MainViewModel = new MainViewModel(config, persistence, launcher, scanner, watcher, hotkey, startup);
    }

    public void Dispose() => MainViewModel.Dispose();
}