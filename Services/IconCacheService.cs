using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLaunch.Helpers;

namespace QuickLaunch.Services;

public sealed class IconCacheService
{
    private readonly string _dir;

    public IconCacheService(string baseDir)
    {
        _dir = Path.Combine(baseDir, "cache", "icons");
        Directory.CreateDirectory(_dir);
    }

    public Task<ImageSource> GetIconAsync(string path, CancellationToken ct = default)
        => Task.Run(() => GetIcon(path), ct);

    private ImageSource GetIcon(string path)
    {
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(path.ToLowerInvariant())));
        var file = Path.Combine(_dir, key + ".png");

        try
        {
            if (File.Exists(file))
                return LoadBitmap(file);

            if (NativeMethods.ExtractLargeIcon(path, out var hIcon))
            {
                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    source.Freeze();

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    using (var output = File.Create(file))
                        encoder.Save(output);

                    return LoadBitmap(file);
                }
                finally
                {
                    NativeMethods.DestroyIcon(hIcon);
                }
            }
        }
        catch
        {
        }

        var fallback = new BitmapImage();
        fallback.BeginInit();
        fallback.UriSource = new Uri(
            "pack://application:,,,/QuickLaunch;component/Assets/default-icon.png",
            UriKind.Absolute);
        fallback.CacheOption = BitmapCacheOption.OnLoad;
        fallback.EndInit();
        fallback.Freeze();
        return fallback;
    }

    private static BitmapImage LoadBitmap(string file)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(file, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
