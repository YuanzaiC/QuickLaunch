using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace QuickLaunch.Helpers;

public enum AppDialogKind
{
    Info,
    Warning,
    Error,
    Question
}

/// <summary>
/// 应用内统一风格的消息框。
/// 系统 MessageBox 在深色主题下是一块刺眼的白色，而且字体会与整体设计脱节，
/// 因此这里用自绘窗口替代，颜色全部来自当前主题令牌。
/// </summary>
public static class AppDialog
{
    /// <summary>正常提示。</summary>
    public static void Info(string message, string title = "QuickLaunch")
        => ShowCore(message, title, AppDialogKind.Info, false);

    /// <summary>警告提示。</summary>
    public static void Warn(string message, string title = "QuickLaunch")
        => ShowCore(message, title, AppDialogKind.Warning, false);

    /// <summary>错误提示。</summary>
    public static void Error(string message, string title = "QuickLaunch")
        => ShowCore(message, title, AppDialogKind.Error, false);

    /// <summary>确认对话框，返回用户是否点了确定。</summary>
    public static bool Confirm(string message, string title = "QuickLaunch")
        => ShowCore(message, title, AppDialogKind.Question, true);

    /// <summary>文本输入对话框，取消时返回 null。</summary>
    public static string? Input(string title, string prompt, string initialValue)
    {
        using var shell = new DialogShell(title, "\uE70F", "AccentBrush", "SoftAccentBrush");
        var box = new TextBox
        {
            Text = initialValue,
            MinWidth = 360,
            Margin = new Thickness(0, 0, 0, 18),
            Style = ResolveStyle("InputBox")
        };

        shell.Body.Children.Add(new TextBlock
        {
            Text = prompt,
            Style = ResolveStyle("DialogMessage"),
            Margin = new Thickness(0, 0, 0, 10)
        });
        shell.Body.Children.Add(box);

        string? result = null;
        var buttons = ButtonRow();
        var ok = DialogButton("确定", primary: true, isDefault: true);
        var cancel = DialogButton("取消", primary: false, isCancel: true);
        ok.Click += (_, _) => { result = box.Text; shell.Close(true); };
        cancel.Click += (_, _) => shell.Close(false);

        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        shell.Body.Children.Add(buttons);

        shell.Window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        shell.Show();
        return result;
    }

    private static bool ShowCore(string message, string title, AppDialogKind kind, bool withCancel)
    {
        var (glyph, brushKey, softKey) = kind switch
        {
            AppDialogKind.Warning => ("\uE7BA", "WarningBrush", "WarningSoftBrush"),
            AppDialogKind.Error => ("\uEA39", "DangerBrush", "DangerSoftBrush"),
            AppDialogKind.Question => ("\uE897", "AccentBrush", "SoftAccentBrush"),
            _ => ("\uE946", "InfoBrush", "InfoSoftBrush")
        };

        using var shell = new DialogShell(title, glyph, brushKey, softKey);
        shell.Body.Children.Add(new TextBlock
        {
            Text = message,
            Style = ResolveStyle("DialogMessage")
        });

        var buttons = ButtonRow();
        var ok = DialogButton(withCancel ? "确定" : "知道了", primary: true, isDefault: true, isCancel: !withCancel);
        ok.Click += (_, _) => shell.Close(true);
        buttons.Children.Add(ok);

        if (withCancel)
        {
            var cancel = DialogButton("取消", primary: false, isCancel: true);
            cancel.Click += (_, _) => shell.Close(false);
            buttons.Children.Insert(0, cancel);
        }

        shell.Body.Children.Add(buttons);
        return shell.Show() == true;
    }

    private static StackPanel ButtonRow() => new()
    {
        Orientation = WpfOrientation.Horizontal,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        Margin = new Thickness(0, 4, 0, 0)
    };

    internal static WpfButton DialogButton(string text, bool primary, bool isDefault = false, bool isCancel = false)
        => new()
        {
            Content = text,
            MinWidth = primary ? 96 : 84,
            Height = 38,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
            IsCancel = isCancel,
            Style = ResolveStyle(primary ? "PrimaryButton" : "SecondaryButton")
        };

    internal static Style? ResolveStyle(string key) => WpfApplication.Current?.TryFindResource(key) as Style;

    internal static Brush? ResolveBrush(string key) => WpfApplication.Current?.TryFindResource(key) as Brush;

    internal static Color ResolveColor(string key, Color fallback)
        => WpfApplication.Current?.TryFindResource(key) is Color c ? c : fallback;

    internal static double ResolveDouble(string key, double fallback)
        => WpfApplication.Current?.TryFindResource(key) is double d ? d : fallback;
}

/// <summary>
/// 对话框外壳：圆角卡片 + 柔和阴影 + 图标标题栏。
/// 阴影画在独立的兄弟元素上，避免给内容加 Effect 导致文字失去 ClearType。
/// </summary>
internal sealed class DialogShell : IDisposable
{
    private const double Padding = 22d;
    private const double ShadowPadding = 18d;

    public Window Window { get; }
    public StackPanel Body { get; }

    public DialogShell(string title, string glyph, string glyphBrushKey, string softBrushKey)
    {
        Body = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };

        var content = new StackPanel();
        content.Children.Add(BuildHeader(title, glyph, glyphBrushKey, softBrushKey));
        content.Children.Add(Body);

        var card = new Border
        {
            Margin = new Thickness(ShadowPadding),
            Padding = new Thickness(Padding, Padding - 2, Padding, Padding - 2),
            CornerRadius = new CornerRadius(18),
            Background = AppDialog.ResolveBrush("PanelBrush") ?? Brushes.White,
            BorderBrush = AppDialog.ResolveBrush("BorderBrush2") ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = content
        };

        // 阴影层与卡片同形，被卡片完全遮住，只露出模糊后的外晕。
        var shadow = new Border
        {
            Margin = new Thickness(ShadowPadding, ShadowPadding + 4, ShadowPadding, ShadowPadding - 2),
            CornerRadius = new CornerRadius(18),
            Background = AppDialog.ResolveBrush("ShadowBrush") ?? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x80, 0, 0, 0)),
            Effect = new DropShadowEffect
            {
                Color = AppDialog.ResolveColor("ShadowColor", Colors.Black),
                BlurRadius = 22,
                ShadowDepth = 3,
                Direction = 270,
                Opacity = 0.9
            },
            IsHitTestVisible = false
        };

        var root = new Grid();
        root.Children.Add(shadow);
        root.Children.Add(card);

        Window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Icon = WpfApplication.Current?.TryFindResource("AppIcon") as ImageSource,
            FontFamily = WpfApplication.Current?.TryFindResource("AppFont") as FontFamily ?? new FontFamily("Segoe UI"),
            Content = root
        };

        var owner = ResolveOwner();
        if (owner is not null)
        {
            Window.Owner = owner;
            Window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            Window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        WindowDragHelper.Attach(Window, root);
    }

    public bool? Show() => Window.ShowDialog();

    public void Close(bool? result)
    {
        try
        {
            Window.DialogResult = result;
        }
        catch (InvalidOperationException)
        {
            Window.Close();
        }
    }

    public void Dispose()
    {
        if (Window.IsVisible) Window.Close();
    }

    private static UIElement BuildHeader(string title, string glyph, string glyphBrushKey, string softBrushKey)
    {
        var iconTile = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(12),
            Background = AppDialog.ResolveBrush(softBrushKey) ?? Brushes.Transparent,
            Child = new TextBlock
            {
                Text = glyph,
                FontFamily = WpfApplication.Current?.TryFindResource("FluentFont") as FontFamily ?? new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = AppDialog.ResolveBrush(glyphBrushKey) ?? Brushes.Black,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var text = new TextBlock
        {
            Text = title,
            FontSize = 15.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AppDialog.ResolveBrush("TextBrush") ?? Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var row = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 14)
        };
        row.Children.Add(iconTile);
        row.Children.Add(text);
        return row;
    }

    private static Window? ResolveOwner()
    {
        var main = WpfApplication.Current?.MainWindow;
        if (main is { IsVisible: true }) return main;

        // 面板隐藏时退回到任何一个可见窗口，保证模态关系正确。
        return WpfApplication.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsVisible);
    }
}