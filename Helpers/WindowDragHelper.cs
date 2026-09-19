using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickLaunch.Helpers;

/// <summary>
/// DPI 正确的无边框窗口拖动实现。
///
/// 这里没有使用 <c>WM_NCLBUTTONDOWN + HTCAPTION</c> 交给系统去拖动：
/// 交给系统拖动依赖窗口已经激活（主面板是 ShowActivated=False），
/// 在非整数缩放和多显示器下还容易出现偏移、跳变，并且会把标题栏区域内的
/// 点击全部吞掉（设置页的关闭按钮就是这样失效的）。
///
/// 由我们自己移动窗口可以做到平滑、可夹取边界、并且能精确跳过交互控件。
/// 位置以 Win32 光标坐标（物理像素）为准，并在渲染帧里轮询更新，
/// 这样即使鼠标移动消息被系统合并、或光标移出窗口，拖动也不会掉帧或半路停住。
/// </summary>
public static class WindowDragHelper
{
    /// <summary>拖动时至少留在屏幕内的宽度。</summary>
    private const double MinVisibleWidth = 160;

    /// <summary>拖动时至少留在屏幕内的高度（保证标题栏还能抓得住）。</summary>
    private const double MinVisibleHeight = 56;

    /// <summary>为窗口的某个区域绑定拖动能力。</summary>
    public static void Attach(Window window, FrameworkElement grip)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(grip);

        var state = new DragState(window);
        grip.PreviewMouseLeftButtonDown += (_, e) => state.OnMouseDown(grip, e);
        grip.PreviewMouseMove += (_, e) => state.OnMouseMove(e);
        grip.PreviewMouseLeftButtonUp += (_, e) => state.OnMouseUp(e);
        grip.LostMouseCapture += (_, _) => state.Reset();
        window.Deactivated += (_, _) => state.Reset();
        window.Closed += (_, _) => state.Reset();
    }

    /// <summary>
    /// 判断命中的元素是否属于需要保留默认鼠标行为的交互控件
    /// （按钮、输入框、下拉框、滚动条……），是则不应该触发窗口拖动。
    /// </summary>
    private static bool IsInteractive(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBoxBase or ComboBox or ScrollBar or Thumb or ListBoxItem or MenuItem or PasswordBox or Expander)
                return true;
            // 标记为 no-drag 的区域（例如搜索框外框）不参与拖动。
            if (source is FrameworkElement { Tag: "no-drag" })
                return true;
            if (source is Window)
                return false;

            DependencyObject? parent = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
            parent ??= (source as FrameworkElement)?.Parent;
            source = parent;
        }

        return false;
    }

    private sealed class DragState
    {
        private readonly Window _window;
        private FrameworkElement? _grip;
        private bool _dragging;
        private bool _hooked;
        private int _startCursorX;       // 物理像素（屏幕坐标）
        private int _startCursorY;
        private double _startLeft;       // DIP
        private double _startTop;        // DIP
        private double _scaleX = 1d;
        private double _scaleY = 1d;

        public DragState(Window window) => _window = window;

        public void OnMouseDown(FrameworkElement grip, MouseButtonEventArgs e)
        {
            if (_dragging || e.ChangedButton != MouseButton.Left) return;
            if (_window.WindowState != WindowState.Normal) return;
            if (IsInteractive(e.OriginalSource as DependencyObject)) return;
            if (!NativeMethods.TryGetCursorPos(out var cursorX, out var cursorY)) return;
            if (!grip.CaptureMouse()) return;

            var dpi = VisualTreeHelper.GetDpi(grip);
            _scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1d;
            _scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1d;

            _grip = grip;
            _startCursorX = cursorX;
            _startCursorY = cursorY;
            _startLeft = _window.Left;
            _startTop = _window.Top;
            _dragging = true;
            Hook();
            e.Handled = true;
        }

        public void OnMouseMove(MouseEventArgs e)
        {
            if (!_dragging) return;
            if (e.LeftButton != MouseButtonState.Pressed) { Reset(); return; }
            Follow();
            e.Handled = true;
        }

        public void OnMouseUp(MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            Reset();
            e.Handled = true;
        }

        public void Reset()
        {
            if (!_dragging && !_hooked) return;
            _dragging = false;
            Unhook();
            if (_grip is { IsMouseCaptured: true }) _grip.ReleaseMouseCapture();
            _grip = null;
        }

        /// <summary>每帧跟随光标，避免鼠标消息被合并导致的拖动滞后。</summary>
        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_dragging) { Unhook(); return; }
            if (Mouse.LeftButton != MouseButtonState.Pressed) { Reset(); return; }
            Follow();
        }

        private void Follow()
        {
            if (!NativeMethods.TryGetCursorPos(out var cursorX, out var cursorY)) return;

            var left = SnapToPixel(_startLeft + (cursorX - _startCursorX) / _scaleX, _scaleX);
            var top = SnapToPixel(_startTop + (cursorY - _startCursorY) / _scaleY, _scaleY);
            var bounds = Clamp(left, top);

            // 相同位置不重复调用 SetWindowPos，避免无谓的重绘。
            if (Math.Abs(bounds.Left - _window.Left) < 0.01 && Math.Abs(bounds.Top - _window.Top) < 0.01) return;
            _window.Left = bounds.Left;
            _window.Top = bounds.Top;
        }

        private void Hook()
        {
            if (_hooked) return;
            CompositionTarget.Rendering += OnRendering;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked) return;
            CompositionTarget.Rendering -= OnRendering;
            _hooked = false;
        }

        /// <summary>对齐到整数设备像素，避免缩放后文字发虚。</summary>
        private static double SnapToPixel(double value, double scale)
            => scale <= 0 ? value : Math.Round(value * scale) / scale;

        /// <summary>夹取到虚拟桌面范围内，同时保证窗口始终有一部分可见。</summary>
        private (double Left, double Top) Clamp(double left, double top)
        {
            var screen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            var width = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
            var height = _window.ActualHeight > 0 ? _window.ActualHeight : _window.Height;

            var minLeft = screen.Left - Math.Max(0, width - MinVisibleWidth);
            var maxLeft = screen.Right - MinVisibleWidth;
            var minTop = screen.Top;
            var maxTop = screen.Bottom - Math.Min(MinVisibleHeight, height);

            if (maxLeft < minLeft) (minLeft, maxLeft) = (maxLeft, minLeft);
            if (maxTop < minTop) (minTop, maxTop) = (maxTop, minTop);

            return (Math.Clamp(left, minLeft, maxLeft), Math.Clamp(top, minTop, maxTop));
        }
    }
}