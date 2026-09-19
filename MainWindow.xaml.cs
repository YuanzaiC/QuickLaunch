using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickLaunch.Helpers;
using QuickLaunch.Models;
using QuickLaunch.Services;
using QuickLaunch.ViewModels;

namespace QuickLaunch;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private Point _dragStart;
    private AppEntry? _dragItem;
    private bool _dragging;
    private bool _forceClose;
    private bool _initialized;
    private System.Windows.Forms.NotifyIcon? _tray;
    /// <summary>当前打开的列表项右键菜单（开着时不清除临时高亮）。</summary>
    private ContextMenu? _openMenu;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.RequestShow += ShowPanel;
        _vm.RequestHide += HidePanel;
        Loaded += MainWindow_Loaded;
        Deactivated += OnDeactivated;
        StateChanged += OnStateChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
        CreateTray();

        // 顶栏空白处可拖动窗口：移动逻辑与边界夹取统一由 helper 负责。
        WindowDragHelper.Attach(this, DragArea);

        // 搜索框获得焦点时高亮外框；用资源引用而不是固定画刷，主题切换后依然生效。
        SearchBox.GotKeyboardFocus += (_, _) => SearchHost.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
        SearchBox.LostKeyboardFocus += (_, _) => SearchHost.SetResourceReference(Border.BorderBrushProperty, "BorderBrush2");

        // 点击面板外的透明区域（阴影留白）收起面板。
        MouseLeftButtonDown += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, this)) HidePanel();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Query))
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ContentScroll.ScrollToTop();
                    UpdateEmptyState();
                }));

            if (e.PropertyName == nameof(MainViewModel.MinimizeToTray))
                Dispatcher.BeginInvoke(new Action(ApplyTrayVisibility));

            if (e.PropertyName == nameof(MainViewModel.FilteredItems))
                Dispatcher.BeginInvoke(new Action(UpdateEmptyState));
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var helper = new WindowInteropHelperAdapter(this);
            await _vm.InitializeAsync(helper);
            ApplyTrayVisibility();
            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            LogService.Error("初始化失败", ex);
            AppDialog.Error(ex.Message, "QuickLaunch 启动失败");
        }
    }

    public void ShowPanel()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var area = SystemParameters.WorkArea;
            if (!IsVisible)
            {
                Left = area.Left + Math.Max(0, (area.Width - ActualWidth) / 2);
                Top = area.Top + Math.Max(24, (area.Height - ActualHeight) * 0.16);
                Show();
                UiAnimations.FadeIn(RootPanel, 130);
            }
            WindowState = WindowState.Normal;
            Activate();

            // 面板每次呼出都从中性状态开始：没有任何项被选中，只有鼠标悬停才有反馈。
            ClearTransientSelection();

            FocusSearch();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    public void HidePanel()
    {
        if (IsVisible) Hide();
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // 右键菜单、模态对话框（设置页、确认框、重命名框）打开时不要收起面板。
        if (IsAnyContextMenuOpen() || HasActiveOwnedWindow()) return;
        // 已经最小化时由 OnStateChanged 决定去托盘还是留在原处，这里不要再收起窗口。
        if (WindowState == WindowState.Minimized) return;
        Dispatcher.BeginInvoke(new Action(HidePanel), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// 最小化时的去向：勾选“最小化时在托盘中显示”就真正收进托盘（窗口隐藏、托盘图标保留），
    /// 取消勾选则窗口只是最小化，之后仍可用快捷键正常呼出。
    /// </summary>
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) return;
        if (_vm.MinimizeToTray && IsVisible) Hide();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePanel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FocusSearch();
            e.Handled = true;
            return;
        }

        // 回车打开键盘光标所在项；没动过键盘光标（或光标已不在当前结果里）时打开第一条，
        // 也就是“打字 → 回车”永远打开第一个结果，不受鼠标停在哪影响。
        if (e.Key == Key.Enter)
        {
            var target = _vm.Selected is AppEntry cursor && _vm.FilteredItems.Contains(cursor)
                ? cursor
                : _vm.FilteredItems.FirstOrDefault();

            if (target is not null)
            {
                _vm.LaunchCommand.Execute(target);
                e.Handled = true;
            }
            return;
        }

        // 在搜索框里也能用上下键挑选结果。
        if (e.Key is Key.Down or Key.Up && SearchBox.IsKeyboardFocusWithin)
        {
            MoveSelection(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Down or Key.Up or Key.PageDown or Key.PageUp or Key.Home or Key.End)
            Dispatcher.BeginInvoke(new Action(ScrollSelectionIntoView), System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>移动键盘光标并保证它落在可视区域内（只有按键盘时才会显示出高亮）。</summary>
    private void MoveSelection(int delta)
    {
        var items = _vm.FilteredItems;
        if (items.Count == 0) return;

        var index = _vm.Selected is null ? -1 : items.IndexOf(_vm.Selected);
        var next = index < 0
            ? (delta > 0 ? 0 : items.Count - 1)
            : Math.Clamp(index + delta, 0, items.Count - 1);

        _vm.Selected = items[next];
        ScrollSelectionIntoView();
    }

    private void ScrollSelectionIntoView()
    {
        if (_vm.Selected is not AppEntry selected) return;

        AppList.UpdateLayout();
        if (AppList.ItemContainerGenerator.ContainerFromItem(selected) is ListBoxItem container && container.IsVisible)
        {
            container.BringIntoView();
            return;
        }

        // 容器还没生成（虚拟化）时先让列表滚到该项，再重新取容器。
        AppList.ScrollIntoView(selected);
        AppList.UpdateLayout();
        (AppList.ItemContainerGenerator.ContainerFromItem(selected) as ListBoxItem)?.BringIntoView();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            HidePanel();
        }
        else
        {
            _tray?.Dispose();
        }
    }

    public void ExitApplication()
    {
        _forceClose = true;
        _tray?.Dispose();
        _tray = null;
        System.Windows.Application.Current.Shutdown();
    }

    private void CreateTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "QuickLaunch",
            Icon = LoadTrayIcon(),
            // 先不显示：等配置读出来后按“最小化时在托盘中显示”决定是否驻留托盘。
            Visible = false
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开", null, (_, _) => ShowPanel());
        menu.Items.Add("设置", null, (_, _) => Settings_Click(null, new RoutedEventArgs()));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowPanel();
    }

    /// <summary>按当前设置同步托盘图标的去留。</summary>
    private void ApplyTrayVisibility()
    {
        if (_tray is not null) _tray.Visible = _vm.MinimizeToTray;
    }

    /// <summary>
    /// 托盘图标用程序自带的多尺寸 ico，并按系统托盘尺寸取对应那一帧，避免缩放糊掉；
    /// 取不到时退回系统默认图标，保证托盘功能本身不会因为缺文件而失效。
    /// </summary>
    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "QuickLaunch.ico");
            if (System.IO.File.Exists(path))
            {
                var size = System.Windows.Forms.SystemInformation.SmallIconSize;
                return new System.Drawing.Icon(path, size.Width, size.Height);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("托盘图标加载失败，改用系统默认图标", ex);
        }
        return System.Drawing.SystemIcons.Application;
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var wasVisible = IsVisible;
        HidePanel();
        var w = new SettingsWindow(_vm) { Owner = this };
        w.ShowDialog();
        if (wasVisible) ShowPanel();
    }

    private void Hide_Click(object? sender, RoutedEventArgs e) => HidePanel();

    private void ContentScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        ContentScroll.ScrollToVerticalOffset(Math.Max(0, ContentScroll.VerticalOffset - e.Delta / 2.5));
        e.Handled = true;
    }

    private void CancelScan_Click(object? sender, RoutedEventArgs e) => _vm.CancelScanCommand.Execute(null);

    private void AppList_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(AppList);
        _dragItem = FindDataContext<AppEntry>(e.OriginalSource as DependencyObject);
        _dragging = false;
        // 这里不再写 _vm.Selected：列表项按下时会由 ListBox 原生点亮一下作为按压反馈，
        // 松开或移动鼠标后立刻清掉，不留下“一直有个软件被选中”的状态。
    }

    private void AppList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // 鼠标一动就回到“悬停”语义：清掉键盘光标 / 右键临时高亮。
        // 但右键菜单开着时要保留那一项的高亮（菜单弹出时鼠标会从列表上路过）。
        if (_openMenu is not { IsOpen: true }) ClearTransientSelection();

        if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null) return;
        var p = e.GetPosition(AppList);
        if (Math.Abs(p.X - _dragStart.X) < 8 && Math.Abs(p.Y - _dragStart.Y) < 8) return;
        _dragging = true;
        var source = _dragItem;
        _dragItem = null;
        System.Windows.DragDrop.DoDragDrop(AppList, source, System.Windows.DragDropEffects.Move);
    }

    /// <summary>
    /// 只负责结束拖拽状态：真正的单击启动由列表项的
    /// <c>AppCard_MouseLeftButtonUp</c>（Preview 阶段）处理，避免启动两次。
    /// </summary>
    private void AppList_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragging = false;
        _dragItem = null;
    }

    private void AppList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(AppEntry))) return;
        var source = e.Data.GetData(typeof(AppEntry)) as AppEntry;
        var target = FindDataContext<AppEntry>(e.OriginalSource as DependencyObject);
        if (source != null && target != null) _vm.MoveItemToGroup(source, target);
    }

    private void AppCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging) return;
        if (sender is ListBoxItem item && item.DataContext is AppEntry entry)
        {
            // 单击只负责启动，不保留任何选中状态。
            ClearTransientSelection();
            _vm.LaunchCommand.Execute(entry);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 列表项一被创建就挂好右键菜单。菜单必须在右键之前就存在：
    /// WPF 是先决定“这一项有没有菜单”再触发 ContextMenuOpening，
    /// 在事件里临时 new 出来的菜单只能等下一次右键才生效（这就是“要右键两次”的原因）。
    /// </summary>
    private void AppCard_Loaded(object sender, RoutedEventArgs e) => AttachCardMenu(sender as FrameworkElement);

    private void AppCard_ContextMenuOpening(object sender, ContextMenuEventArgs e) => OpenCardMenu(sender);

    /// <summary>收藏 / 最近使用 的小卡片：与列表项共用同一套右键菜单。</summary>
    private void Tile_Loaded(object sender, RoutedEventArgs e) => AttachCardMenu(sender as FrameworkElement);

    private void Tile_ContextMenuOpening(object sender, ContextMenuEventArgs e) => OpenCardMenu(sender);

    /// <summary>给列表项 / 收藏·最近卡片挂上右键菜单（挂一次即可，容器被回收后继续复用）。</summary>
    private void AttachCardMenu(FrameworkElement? element)
    {
        if (element is null || element.ContextMenu is not null) return;

        var menu = BuildCardMenu();
        // 菜单关掉就撤掉临时高亮，不留“选中”痕迹。
        menu.Closed += (_, _) => { _openMenu = null; ClearTransientSelection(); };
        element.ContextMenu = menu;
    }

    /// <summary>菜单弹出前同步状态（收藏 / 最近可用性），并按位置决定是否临时高亮。</summary>
    private void OpenCardMenu(object sender)
    {
        if (sender is not FrameworkElement element || element.DataContext is not AppEntry entry) return;

        if (element.ContextMenu is ContextMenu menu) UpdateMenuState(menu, entry);

        // 只在菜单打开期间高亮被右键的那一项，方便看清菜单属于谁；关闭时由 Closed 清掉。
        _openMenu = element.ContextMenu;
        // 列表项用选中态高亮；小卡片由 TileButton 里的 ContextMenu.IsOpen 触发器自己高亮。
        if (element is ListBoxItem) _vm.Selected = entry;
    }

    private ContextMenu BuildCardMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("launch", "启动", "\uE768", MenuLaunch_Click));
        menu.Items.Add(CreateMenuItem("admin", "以管理员身份运行", "\uE7EF", MenuAdmin_Click));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("fav", "添加到收藏", "\uE734", MenuFavorite_Click));
        menu.Items.Add(CreateMenuItem("recent", "从最近打开中移除", "\uE74D", MenuRemoveRecent_Click));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("folder", "打开所在文件夹", "\uE8B7", MenuOpenFolder_Click));
        menu.Items.Add(CreateMenuItem("copy", "复制完整路径", "\uE8C8", MenuCopyPath_Click));
        menu.Items.Add(CreateMenuItem("rename", "重命名", "\uE70F", MenuRename_Click));
        menu.Items.Add(CreateMenuItem("hide", "在列表中隐藏", "\uED1A", MenuHide_Click));
        return menu;
    }

    /// <summary>清掉临时高亮（键盘光标 / 右键菜单 / 按压反馈），回到纯悬停状态。</summary>
    private void ClearTransientSelection() => _vm.Selected = null;

    private static MenuItem CreateMenuItem(string key, string header, string glyph, RoutedEventHandler handler)
    {
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = System.Windows.Application.Current.TryFindResource("FluentFont") as FontFamily
                         ?? new FontFamily("Segoe MDL2 Assets"),
            FontSize = 13,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var item = new MenuItem { Header = header, Icon = icon, Tag = key };
        item.Click += handler;
        return item;
    }

    /// <summary>每次弹出菜单时同步收藏状态与可用性。</summary>
    private static void UpdateMenuState(ContextMenu menu, AppEntry entry)
    {
        foreach (var obj in menu.Items)
        {
            if (obj is not MenuItem mi) continue;
            switch (mi.Tag as string)
            {
                case "fav":
                    mi.Header = entry.IsFavorite ? "取消收藏" : "添加到收藏";
                    if (mi.Icon is TextBlock fav) fav.Text = entry.IsFavorite ? "\uE735" : "\uE734";
                    break;
                case "recent":
                    mi.IsEnabled = entry.LastUsedUtc.HasValue;
                    break;
            }
        }
    }

    private static AppEntry? ItemFrom(object sender)
    {
        if (sender is not MenuItem mi || mi.Parent is not ContextMenu cm) return null;
        return (cm.PlacementTarget as FrameworkElement)?.DataContext as AppEntry;
    }

    private void MenuLaunch_Click(object sender, RoutedEventArgs e) => _vm.LaunchCommand.Execute(ItemFrom(sender));
    private void MenuAdmin_Click(object sender, RoutedEventArgs e) => _vm.AdminLaunchCommand.Execute(ItemFrom(sender));
    private void MenuFavorite_Click(object sender, RoutedEventArgs e) => _vm.ToggleFavoriteCommand.Execute(ItemFrom(sender));
    private void MenuRemoveRecent_Click(object sender, RoutedEventArgs e) => _vm.RemoveRecentCommand.Execute(ItemFrom(sender));
    private void MenuOpenFolder_Click(object sender, RoutedEventArgs e) => _vm.OpenFolderCommand.Execute(ItemFrom(sender));
    private void MenuCopyPath_Click(object sender, RoutedEventArgs e) => _vm.CopyPathCommand.Execute(ItemFrom(sender));
    private void MenuRename_Click(object sender, RoutedEventArgs e) => _vm.RenameCommand.Execute(ItemFrom(sender));
    private void MenuHide_Click(object sender, RoutedEventArgs e) => _vm.HideCommand.Execute(ItemFrom(sender));

    /// <summary>右键菜单是否正开着（菜单挂在列表项 / 收藏·最近卡片上，必须沿可视树向上找）。</summary>
    private bool IsAnyContextMenuOpen()
    {
        // 收藏 / 最近卡片不在 AppList 里，用当前打开的那份菜单判断最可靠。
        if (_openMenu is { IsOpen: true }) return true;

        var node = AppList.InputHitTest(Mouse.GetPosition(AppList)) as DependencyObject;
        while (node != null)
        {
            if (node is FrameworkElement fe && fe.ContextMenu is { IsOpen: true }) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    /// <summary>是否有属于本窗口的可见子窗口（设置页 / 对话框）正开着。</summary>
    private bool HasActiveOwnedWindow()
        => System.Windows.Application.Current.Windows
            .OfType<Window>()
            .Any(w => !ReferenceEquals(w, this) && w.IsVisible && (w.IsActive || ReferenceEquals(w.Owner, this)));

    private void UpdateEmptyState()
    {
        if (EmptyState == null) return;

        var hasQuery = !string.IsNullOrWhiteSpace(_vm.Query);
        var isEmpty = _vm.FilteredItems.Count == 0;

        PinnedSections.Visibility = hasQuery ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;

        if (isEmpty)
        {
            EmptyTitle.Text = hasQuery ? "没有找到匹配的应用" : "还没有可用的项目";
            EmptyHint.Text = hasQuery ? "换个关键词试试，或检查名称与路径" : "在设置里添加扫描目录后会自动建立索引";
        }
    }

    private static T? FindDataContext<T>(DependencyObject? d) where T : class
    {
        while (d != null)
        {
            if (d is FrameworkElement fe && fe.DataContext is T x) return x;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}