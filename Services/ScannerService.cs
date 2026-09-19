using QuickLaunch.Models;
using QuickLaunch.Helpers;

namespace QuickLaunch.Services;

public sealed class ScannerService
{
    private readonly IconCacheService _iconCache;
    private readonly LaunchService _launcher;
    public ScannerService(IconCacheService iconCache, LaunchService launcher) { _iconCache = iconCache; _launcher = launcher; }
    public async Task<List<AppEntry>> ScanToListAsync(AppConfig config, IProgress<(int Current,int Total,string Status)>? progress = null, CancellationToken ct = default)
    {
        var files = new List<string>();
        foreach (var root in config.ScanFolders.Where(Directory.Exists))
            files.AddRange(EnumerateFilesSafe(root, config, 0, ct));
        var results = new List<AppEntry>();
        var gate = new object();
        var total = files.Count; var current = 0;
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2), CancellationToken = ct }, async (path, token) =>
        {
            var item = await BuildEntryAsync(path, token);
            if (item != null)
            {
                lock (gate) results.Add(item);
            }
            var c = Interlocked.Increment(ref current);
            progress?.Report((c, total, $"正在扫描 {c}/{total}"));
        });
        return results;
    }

    private IEnumerable<string> EnumerateFilesSafe(string root, AppConfig config, int depth, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IEnumerable<string> files = Array.Empty<string>();
        try { files = Directory.EnumerateFiles(root); } catch { }
        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(f);
            if (config.IncludeExtensions.Any(x => string.Equals(x, ext, StringComparison.OrdinalIgnoreCase))) yield return f;
        }
        if (!config.Recursive || depth >= config.MaxDepth) yield break;
        IEnumerable<string> dirs = Array.Empty<string>();
        try { dirs = Directory.EnumerateDirectories(root); } catch { }
        foreach (var dir in dirs)
        {
            var name = Path.GetFileName(dir);
            if (config.ExcludeFolders.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase))) continue;
            foreach (var f in EnumerateFilesSafe(dir, config, depth + 1, ct)) yield return f;
        }
    }

    private async Task<AppEntry?> BuildEntryAsync(string path, CancellationToken ct)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return null;
            var ext = info.Extension.ToLowerInvariant();
            string launchPath = path;
            string workDir = info.DirectoryName ?? "";
            bool broken = false;
            if (ext == ".lnk")
            {
                launchPath = ResolveShortcut(path) ?? "";
                broken = string.IsNullOrWhiteSpace(launchPath) || !File.Exists(launchPath);
                if (!string.IsNullOrWhiteSpace(launchPath)) workDir = Path.GetDirectoryName(launchPath) ?? workDir;
            }
            var item = new AppEntry
            {
                Id = path,
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                LaunchPath = launchPath,
                WorkingDirectory = workDir,
                GroupPath = info.DirectoryName ?? "",
                Extension = ext,
                IsShortcut = ext == ".lnk",
                IsBroken = broken,
                Size = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                IsHidden = (info.Attributes & FileAttributes.Hidden) != 0
            };
            item.Icon = await _iconCache.GetIconAsync(broken ? path : launchPath, ct);
            return item;
        }
        catch { return null; }
    }

    private static string? ResolveShortcut(string path)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return null;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(path);
            string target = shortcut.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch { return null; }
    }
}
