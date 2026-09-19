using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using QuickLaunch.Models;

namespace QuickLaunch.Services;

public sealed class ConfigService
{
    private readonly string _path;

    /// <summary>
    /// 与仓库里原本的 config.json 保持一致：小写驼峰、缩进、
    /// 并且不要把 '+' 之类字符转义成 \u002B，方便用户手动编辑。
    /// </summary>
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public ConfigService(string baseDir) { _path = Path.Combine(baseDir, "config.json"); }
    public async Task<AppConfig> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_path))
            {
                var d = new AppConfig();
                await SaveAsync(d, ct);
                return d;
            }
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<AppConfig>(stream, _options, ct) ?? new AppConfig();
        }
        catch
        {
            // 读不出来也不要丢掉用户原来的配置：先留一份 .bak，
            // 否则下一次“保存设置”就会用默认值把用户配置覆盖掉。
            TryBackupBrokenFile();
            return new AppConfig();
        }
    }

    private void TryBackupBrokenFile()
    {
        try
        {
            if (File.Exists(_path)) File.Copy(_path, _path + ".bak", overwrite: true);
        }
        catch
        {
            // 备份失败不影响启动。
        }
    }
    public async Task SaveAsync(AppConfig config, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, config, _options, ct);
    }
}
