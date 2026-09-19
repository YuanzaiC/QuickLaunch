using System.Diagnostics;
using System.IO;
using QuickLaunch.Helpers;
using QuickLaunch.Models;

namespace QuickLaunch.Services;

public sealed class LaunchService
{
    public bool Exists(AppEntry item)
    {
        if (!item.IsShortcut) return File.Exists(item.Path);
        return !string.IsNullOrWhiteSpace(item.LaunchPath) && File.Exists(item.LaunchPath);
    }

    public bool Launch(AppEntry item, bool asAdmin = false)
    {
        try
        {
            var launch = item.LaunchPath;
            if (string.IsNullOrWhiteSpace(launch)) launch = item.Path;
            var psi = new ProcessStartInfo
            {
                FileName = launch,
                UseShellExecute = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(item.WorkingDirectory) ? Path.GetDirectoryName(launch) ?? Environment.CurrentDirectory : item.WorkingDirectory
            };
            if (asAdmin) psi.Verb = "runas";
            Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return false; }
        catch
        {
            // 用应用内弹窗代替系统 MessageBox：深色主题下不会突然弹出一块白框。
            try { AppDialog.Error($"无法启动“{item.Name}”。\n\n路径：{item.Path}", "启动失败"); }
            catch { /* 弹窗失败时不再抛出，避免二次崩溃 */ }
            return false;
        }
    }

    public void OpenContainingFolder(AppEntry item)
    {
        var path = File.Exists(item.Path) ? item.Path : item.LaunchPath;
        if (!string.IsNullOrWhiteSpace(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }
}
