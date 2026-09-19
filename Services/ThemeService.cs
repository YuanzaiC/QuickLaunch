using Microsoft.Win32;
using System.Windows;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace QuickLaunch.Services;

/// <summary>
/// 主题令牌中心。
/// 这里保存唯一的调色板定义（浅色 / 深色成对出现），
/// 所有界面都只通过 DynamicResource 引用令牌，切换主题时无需重建窗口。
/// </summary>
public static class ThemeService
{
    private static readonly MediaColor FallbackAccent = MediaColor.FromRgb(0x00, 0x78, 0xD4);

    /// <summary>当前生效的主题字符串（system / light / dark）。</summary>
    public static string CurrentTheme { get; private set; } = "system";

    /// <summary>系统主题发生变化（仅在使用“跟随系统”时需要处理）。</summary>
    public static event Action? SystemThemeChanged;

    /// <summary>
    /// 主题令牌表：键、浅色值、深色值。
    /// 数值来自同一套层级规则 —— 外壳最浅、内容面最亮、内嵌控件再回退一档，
    /// 这样浅色下不会出现“白底白块”，深色下也不会糊成一片。
    /// </summary>
    private static readonly (string Key, string Light, string Dark)[] Palette =
    {
        // 表面层级
        ("WindowBrush",           "#F1F2F4", "#1B1C1F"),
        ("PanelBrush",            "#F2F3F5", "#202124"),
        ("SectionBrush",          "#FFFFFF", "#2B2C31"),
        ("SectionBrush2",         "#EDEFF3", "#313338"),
        ("SearchBrush",           "#FFFFFF", "#2C2D32"),
        ("ControlBrush",          "#EFF1F4", "#313238"),
        ("CardBrush",             "#F5F6F8", "#323337"),
        ("IconTileBrush",         "#E9ECF1", "#3B3D42"),

        // 文字
        ("TextBrush",             "#1A1B1E", "#F3F4F6"),
        ("SubTextBrush",          "#5F6368", "#A8ABB2"),

        // 描边
        ("BorderBrush2",          "#E2E5EA", "#3A3B40"),
        ("BorderStrongBrush",     "#CBD0D8", "#4B4D53"),

        // 语义色
        ("DangerBrush",           "#C42B1C", "#FF9A8F"),
        ("DangerSoftBrush",       "#1AC42B1C", "#2EFF9A8F"),
        ("WarningBrush",          "#9A6700", "#E8C55F"),
        ("WarningSoftBrush",      "#1F9A6700", "#2EE8C55F"),
        ("InfoBrush",             "#0F6CBD", "#7CC7FF"),
        ("InfoSoftBrush",         "#1A0F6CBD", "#2E7CC7FF"),

        // 滚动条
        ("ScrollTrackBrush",      "#00000000", "#00000000"),
        ("ScrollThumbBrush",      "#C2C7CE", "#4D4F55"),
        ("ScrollThumbHoverBrush", "#A5AAB3", "#64676E"),
        ("ScrollThumbPressedBrush", "#888E97", "#7C8088"),

        // 弹出层（菜单 / 提示）
        ("PopupBrush",            "#FFFFFF", "#2C2D31"),
        ("PopupBorderBrush",      "#DFE3E8", "#46484E"),
        ("MenuHoverBrush",        "#1C000000", "#2BFFFFFF"),

        // 其他
        ("TrackBrush",            "#E4E7EC", "#3B3C41"),
        ("KeyCapBrush",           "#E8EBEF", "#34363B"),
        ("KeyCapBorderBrush",     "#D9DEE5", "#45474D"),
        ("ShadowBrush",           "#38000000", "#73000000")
    };

    public static bool IsDark(string? theme)
    {
        if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var useLight = key?.GetValue("AppsUseLightTheme") as int? ?? 1;
            return useLight == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Apply(string? theme)
    {
        var app = System.Windows.Application.Current;
        if (app is null) return;

        CurrentTheme = string.IsNullOrWhiteSpace(theme) ? "system" : theme;
        var dark = IsDark(CurrentTheme);
        var r = app.Resources;

        foreach (var (key, light, darkValue) in Palette)
            SetBrush(r, key, dark ? darkValue : light);

        // 阴影与主题无关，只改透明度。
        r["ShadowColor"] = MediaColor.FromRgb(0x00, 0x00, 0x00);
        r["ShadowOpacity"] = 0.9d;

        // 强调色跟随系统，所有强调色相关令牌都从它推导，整体色调保持一致。
        // 浅色主题里压暗过浅的强调色，深色主题里提亮过暗的强调色，
        // 保证强调色始终能作为文字 / 图标 / 描边使用。
        var accent = EnsureContrast(ReadSystemAccent(), dark ? MediaColor.FromRgb(0x20, 0x21, 0x24) : MediaColor.FromRgb(0xFF, 0xFF, 0xFF), !dark);
        SetBrush(r, "AccentBrush", accent);
        SetBrush(r, "AccentForegroundBrush", MediaColor.FromRgb(0xFF, 0xFF, 0xFF));
        SetBrush(r, "AccentFillBrush", dark ? Mix(accent, MediaColor.FromRgb(255, 255, 255), 0.10) : Mix(accent, MediaColor.FromRgb(0, 0, 0), 0.06));
        SetBrush(r, "AccentBorderBrush", dark ? Mix(accent, MediaColor.FromRgb(255, 255, 255), 0.34) : Mix(accent, MediaColor.FromRgb(0, 0, 0), 0.22));
        SetBrush(r, "SoftAccentBrush", WithAlpha(accent, dark ? (byte)0x30 : (byte)0x1A));
        // 选中态要明显强于悬停态：过去两者叠在卡片上几乎一样亮，选中了也看不出来。
        SetBrush(r, "SelectedBrush", WithAlpha(accent, dark ? (byte)0x4D : (byte)0x33));
        SetBrush(r, "SelectedBorderBrush", WithAlpha(accent, dark ? (byte)0x78 : (byte)0x52));
        // 悬停色要比过去更明显一些：之前 8% 的强调色叠在面板上几乎看不出变化；
        // 但又要比选中态轻一档，形成“悬停 < 选中 < 按下”的层次。
        SetBrush(r, "HoverBrush", WithAlpha(accent, dark ? (byte)0x33 : (byte)0x1E));
        SetBrush(r, "ButtonHoverBrush", WithAlpha(accent, dark ? (byte)0x48 : (byte)0x2E));
        SetBrush(r, "SelectionBrush", WithAlpha(accent, dark ? (byte)0x70 : (byte)0x50));
    }

    /// <summary>系统主题变化时由 App 调用（内部会自行判断是否需要处理）。</summary>
    public static void OnSystemThemeChanged()
    {
        if (!string.Equals(CurrentTheme, "system", StringComparison.OrdinalIgnoreCase)) return;
        Apply("system");
        SystemThemeChanged?.Invoke();
    }

    private static MediaColor ReadSystemAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int raw)
            {
                // DWM 里存的是 0xAABBGGRR（ABGR），不是 RGB，取反了会得到错误的颜色。
                var color = MediaColor.FromRgb((byte)(raw & 0xFF), (byte)((raw >> 8) & 0xFF), (byte)((raw >> 16) & 0xFF));
                // 有些机器（或未设置强调色时）会报全白 / 全黑 / 无彩色，
                // 这类值会让强调色派生出来的悬停色、选中色、聚焦描边全部隐形，
                // 因此不能用，直接退回默认蓝。
                if (IsUsableAccent(color)) return color;
            }
        }
        catch
        {
            // 读不到就用默认蓝。
        }

        return FallbackAccent;
    }

    /// <summary>强调色需要有足够亮度和饱和度，否则派生的浅色叠层会看不清。</summary>
    private static bool IsUsableAccent(MediaColor color)
    {
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        var min = Math.Min(color.R, Math.Min(color.G, color.B));
        return max >= 0x60 && max - min >= 0x1E;
    }

    /// <summary>确保强调色在对应底色上足够显眼（用于文字、图标和描边）。</summary>
    private static MediaColor EnsureContrast(MediaColor color, MediaColor background, bool darken)
    {
        var toward = darken ? MediaColor.FromRgb(0x00, 0x00, 0x00) : MediaColor.FromRgb(0xFF, 0xFF, 0xFF);
        var result = color;
        for (var i = 0; i < 13 && ContrastRatio(result, background) < 3.0; i++)
            result = Mix(result, toward, 0.08);
        return result;
    }

    private static double ContrastRatio(MediaColor a, MediaColor b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var hi = Math.Max(la, lb);
        var lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double RelativeLuminance(MediaColor color)
        => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static double Linear(byte value)
    {
        var s = value / 255d;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    private static MediaColor Mix(MediaColor from, MediaColor to, double ratio)
    {
        ratio = Math.Clamp(ratio, 0d, 1d);
        return MediaColor.FromRgb(
            (byte)Math.Round(from.R + (to.R - from.R) * ratio),
            (byte)Math.Round(from.G + (to.G - from.G) * ratio),
            (byte)Math.Round(from.B + (to.B - from.B) * ratio));
    }

    private static MediaColor WithAlpha(MediaColor color, byte alpha)
        => MediaColor.FromArgb(alpha, color.R, color.G, color.B);

    private static void SetBrush(ResourceDictionary r, string key, string value)
    {
        var color = (MediaColor)MediaColorConverter.ConvertFromString(value)!;
        r[key] = new MediaSolidColorBrush(color);
    }

    private static void SetBrush(ResourceDictionary r, string key, MediaColor value)
        => r[key] = new MediaSolidColorBrush(value);
}