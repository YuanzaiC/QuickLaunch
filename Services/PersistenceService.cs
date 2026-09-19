using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using QuickLaunch.Models;

namespace QuickLaunch.Services;

public sealed class PersistenceService
{
    private readonly string _baseDir;

    /// <summary>小写驼峰 + 不转义中文，缓存文件保持可读。</summary>
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private string FavoritesPath => Path.Combine(_baseDir, "favorites.json");
    private string RecentPath => Path.Combine(_baseDir, "recent.json");
    private string CachePath => Path.Combine(_baseDir, "catalog.cache.json");
    private string HiddenPath => Path.Combine(_baseDir, "hidden.json");
    public PersistenceService(string baseDir) { _baseDir = baseDir; }

    public async Task<HashSet<string>> LoadFavoritesAsync(CancellationToken ct = default)
        => new((await LoadRecordsAsync(FavoritesPath, ct)).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);

    public async Task<List<PersistedItemRecord>> LoadRecentsAsync(CancellationToken ct = default)
        => await LoadRecordsAsync(RecentPath, ct);

    public async Task SaveFavoritesAsync(IEnumerable<AppEntry> items, CancellationToken ct = default)
        => await SaveRecordsAsync(FavoritesPath, items.Select(ToRecord).ToList(), ct);

    public async Task SaveRecentsAsync(IEnumerable<AppEntry> items, CancellationToken ct = default)
        => await SaveRecordsAsync(RecentPath, items.Select(ToRecord).ToList(), ct);

    public async Task<HashSet<string>> LoadHiddenAsync(CancellationToken ct = default)
    {
        try
        {
            if(!File.Exists(HiddenPath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var stream=File.OpenRead(HiddenPath);
            var list=await JsonSerializer.DeserializeAsync<List<string>>(stream,_options,ct) ?? new();
            return new HashSet<string>(list,StringComparer.OrdinalIgnoreCase);
        }
        catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    }

    public async Task SaveHiddenAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        await using var stream=File.Create(HiddenPath);
        await JsonSerializer.SerializeAsync(stream,ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),_options,ct);
    }

    public async Task<List<AppEntry>> LoadCatalogCacheAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(CachePath)) return new();
            await using var stream = File.OpenRead(CachePath);
            return await JsonSerializer.DeserializeAsync<List<AppEntry>>(stream, _options, ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task SaveCatalogCacheAsync(IEnumerable<AppEntry> items, CancellationToken ct = default)
    {
        var safe = items.Select(x => new AppEntry
        {
            Id=x.Id, Name=x.Name, Path=x.Path, LaunchPath=x.LaunchPath, WorkingDirectory=x.WorkingDirectory,
            GroupPath=x.GroupPath, CustomGroup=x.CustomGroup, SortOrder=x.SortOrder, Extension=x.Extension, IsShortcut=x.IsShortcut, IsBroken=x.IsBroken,
            Size=x.Size, LastWriteTimeUtc=x.LastWriteTimeUtc, LaunchCount=x.LaunchCount, LastUsedUtc=x.LastUsedUtc,
            IsHidden=x.IsHidden, IsFavorite=x.IsFavorite
        }).ToList();
        Directory.CreateDirectory(_baseDir);
        await using var stream = File.Create(CachePath);
        await JsonSerializer.SerializeAsync(stream, safe, _options, ct);
    }

    private static PersistedItemRecord ToRecord(AppEntry x) => new() { Id=x.Id, LaunchCount=x.LaunchCount, LastUsedUtc=x.LastUsedUtc, IsFavorite=x.IsFavorite, Hidden=x.IsHidden };

    private async Task<List<PersistedItemRecord>> LoadRecordsAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path)) return new();
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<PersistedItemRecord>>(stream, _options, ct) ?? new();
        }
        catch { return new(); }
    }

    private async Task SaveRecordsAsync(string path, List<PersistedItemRecord> records, CancellationToken ct)
    {
        Directory.CreateDirectory(_baseDir);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, records, _options, ct);
    }
}
