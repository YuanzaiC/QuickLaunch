using Microsoft.Win32;

namespace QuickLaunch.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickLaunch";
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is string;
    }
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (!enabled) key.DeleteValue(ValueName, false);
        else key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
    }
}
