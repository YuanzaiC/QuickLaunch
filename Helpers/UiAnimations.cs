using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace QuickLaunch.Helpers;

/// <summary>
/// 轻量动画工具。所有动画都会遵守系统的“显示动画”开关，
/// 关闭动画时直接落到终态，不做无谓的闪烁。
/// </summary>
public static class UiAnimations
{
    /// <summary>系统是否允许播放动画。</summary>
    private static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    /// <summary>淡入（面板呼出时使用）。</summary>
    public static void FadeIn(UIElement element, int milliseconds = 140)
    {
        if (element is null) return;
        if (!AnimationsEnabled) { element.Opacity = 1; return; }

        element.Opacity = 1;
        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            });
    }

    /// <summary>淡入 + 轻微上移，用于设置页切换分页。</summary>
    public static void FadeSlideIn(FrameworkElement element, int milliseconds = 180, double offsetY = 8d)
    {
        if (element is null) return;
        if (!AnimationsEnabled) { element.Opacity = 1; return; }

        var transform = element.RenderTransform as TranslateTransform;
        if (transform is null || transform.IsFrozen)
        {
            transform = new TranslateTransform();
            element.RenderTransform = transform;
        }

        element.Opacity = 1;
        transform.Y = 0;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(milliseconds);

        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0d, 1d, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        transform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(offsetY, 0d, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
    }
}