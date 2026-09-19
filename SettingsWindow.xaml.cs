using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickLaunch.Models;
using QuickLaunch.Helpers;
using QuickLaunch.Services;
using QuickLaunch.ViewModels;

namespace QuickLaunch;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _main;
    private readonly SettingsViewModel _vm;
    private readonly string _originalTheme;
    private bool _saved;

    public SettingsWindow(MainViewModel main)
    {
        _main = main;
        _vm = new SettingsViewModel(main, main.GetConfigSnapshot());
        _originalTheme = _vm.Theme;
        DataContext = _vm;
        InitializeComponent();

        // 侧栏标题与右侧页头都可以拖动窗口；关闭按钮等交互控件会被自动跳过。
        WindowDragHelper.Attach(this, SidebarGrip);
        WindowDragHelper.Attach(this, HeaderGrip);

        Loaded += SettingsWindow_Loaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        HotkeyBox.Text = _vm.Hotkey;
        ShowPage(ScanPage, "扫描与索引");
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseWithoutSaving();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Save_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender is not System.Windows.Controls.RadioButton rb || rb.Tag is not string text) return;
        if (text == "扫描与索引") ShowPage(ScanPage, text);
        else if (text == "外观与行为") ShowPage(BehaviorPage, text);
        else if (text == "维护与数据") ShowPage(MaintenancePage, text);
        else if (text == "关于 QuickLaunch") ShowPage(AboutPage, text);
    }

    private void ShowPage(FrameworkElement page, string title)
    {
        ScanPage.Visibility = Visibility.Collapsed;
        BehaviorPage.Visibility = Visibility.Collapsed;
        MaintenancePage.Visibility = Visibility.Collapsed;
        AboutPage.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
        PageTitle.Text = title;
        UiAnimations.FadeSlideIn(page);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryValidateHotkey(_vm.Hotkey, out var error))
        {
            AppDialog.Warn(error, "快捷键无效");
            return;
        }

        _ = SaveAsync();
    }

    private async Task SaveAsync()
    {
        try
        {
            await _vm.SaveAsync();
            _saved = true;
            CloseWindow(true);
        }
        catch (Exception ex)
        {
            AppDialog.Error(ex.Message, "保存失败");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseWithoutSaving();

    private void CloseWithoutSaving()
    {
        // 主题是即时预览的，没有保存就退出时必须还原。
        if (!_saved && !string.Equals(_vm.Theme, _originalTheme, StringComparison.OrdinalIgnoreCase))
            ThemeService.Apply(_originalTheme);

        CloseWindow(false);
    }

    private void CloseWindow(bool? result)
    {
        try
        {
            DialogResult = result;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }

    /// <summary>完全退出程序：设置为不在托盘显示时，这里是唯一的退出入口。</summary>
    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Confirm("退出 QuickLaunch？\n\n程序会完全关闭，快捷键也会一并失效，需要重新启动才能再次呼出。", "退出 QuickLaunch"))
            return;

        var owner = Owner as MainWindow;
        CloseWithoutSaving();
        if (owner is not null)
            owner.Dispatcher.BeginInvoke(new Action(owner.ExitApplication));
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender is ComboBox combo && combo.SelectedValue is string theme)
            ThemeService.Apply(theme);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        using var d = new System.Windows.Forms.FolderBrowserDialog();
        if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) _vm.AddFolder(d.SelectedPath);
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is string path) _vm.RemoveFolder(path);
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Confirm("将删除已经生成的图标缓存文件，下次扫描时会自动重建。\n\n确定要继续吗？", "清理图标缓存")) return;

        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "cache", "icons");
            var removed = 0;
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    try { File.Delete(file); removed++; } catch { }
                }
            }

            AppDialog.Info($"已清理 {removed} 个缓存文件。");
        }
        catch (Exception ex)
        {
            AppDialog.Error(ex.Message, "清理失败");
        }
    }

    private void CleanBroken_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialog.Confirm("失效项目的目标文件已经不存在，清理会把它们从列表中删除。\n\n确定要继续吗？", "清理失效项目")) return;
        _main.CleanBrokenCommand.Execute(null);
        AppDialog.Info("已发起清理并重新扫描。");
    }

    private void RestoreHidden_Click(object sender, RoutedEventArgs e)
    {
        _main.RestoreHidden();
        AppDialog.Info("已恢复所有隐藏项目。");
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = AppContext.BaseDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppDialog.Error(ex.Message, "打开目录失败");
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.json", FileName = "config.json" };
        if (d.ShowDialog() != true) return;
        try
        {
            var json = await File.ReadAllTextAsync(d.FileName);
            var c = JsonSerializer.Deserialize<AppConfig>(json);
            if (c == null)
            {
                AppDialog.Warn("文件内容不是有效的配置。", "导入失败");
                return;
            }

            _vm.ScanFolders.Clear();
            foreach (var x in c.ScanFolders) if (!_vm.ScanFolders.Contains(x, StringComparer.OrdinalIgnoreCase)) _vm.ScanFolders.Add(x);
            _vm.Hotkey = c.Hotkey;
            HotkeyBox.Text = c.Hotkey;
            _vm.MaxDepth = c.MaxDepth;
            _vm.Recursive = c.Recursive;
            _vm.MaxRecent = c.MaxRecent;
            _vm.StartWithWindows = c.StartWithWindows;
            _vm.Theme = c.Theme;
            _vm.IncludeExtensionsText = string.Join(",", c.IncludeExtensions);
            _vm.ExcludeText = string.Join(",", c.ExcludeFolders);

            ThemeService.Apply(c.Theme);
            AppDialog.Info("配置已载入，点击“保存设置”后生效。", "导入完成");
        }
        catch (Exception ex)
        {
            AppDialog.Error(ex.Message, "导入失败");
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = "config.json" };
        if (d.ShowDialog() != true) return;
        try
        {
            var json = JsonSerializer.Serialize(_vm.BuildConfig(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(d.FileName, json);
            AppDialog.Info("配置已导出。", "导出完成");
        }
        catch (Exception ex)
        {
            AppDialog.Error(ex.Message, "导出失败");
        }
    }

    private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        HotkeyBox.SelectAll();
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        var modifiers = Keyboard.Modifiers;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows)) == ModifierKeys.None)
        {
            AppDialog.Info("快捷键至少需要一个 Ctrl、Alt、Shift 或 Win 修饰键。", "快捷键");
            return;
        }

        var keyName = HotkeyService.GetDisplayKey(key);
        if (keyName == null)
        {
            AppDialog.Info("这个按键暂不支持作为全局快捷键。", "快捷键");
            return;
        }

        var parts = new List<string>();
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(keyName);

        _vm.Hotkey = string.Join("+", parts);
        HotkeyBox.Text = _vm.Hotkey;
        HotkeyBox.CaretIndex = HotkeyBox.Text.Length;
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        _vm.Hotkey = "Alt+Space";
        HotkeyBox.Text = _vm.Hotkey;
        HotkeyBox.Focus();
    }

    private static bool TryValidateHotkey(string hotkey, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(hotkey)) { error = "快捷键不能为空。"; return false; }
        var hasModifier = hotkey.Split('+').Any(x => x.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || x.Equals("Alt", StringComparison.OrdinalIgnoreCase) || x.Equals("Shift", StringComparison.OrdinalIgnoreCase) || x.Equals("Win", StringComparison.OrdinalIgnoreCase));
        if (!hasModifier) { error = "快捷键至少需要一个 Ctrl、Alt、Shift 或 Win 修饰键。"; return false; }
        return true;
    }
}