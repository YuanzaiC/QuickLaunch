namespace QuickLaunch.Helpers;

/// <summary>统一的文本输入对话框（主题化外观，见 <see cref="AppDialog"/>）。</summary>
public static class SimpleInputDialog
{
    public static string? Show(string title, string initial)
        => AppDialog.Input(title, "输入新的名称（不含扩展名）", initial);
}