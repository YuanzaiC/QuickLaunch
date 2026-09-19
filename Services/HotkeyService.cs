using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Interop;
using QuickLaunch.Helpers;

namespace QuickLaunch.Services;

public sealed class HotkeyService : IDisposable
{
    private HwndSource? _source;
    private const int Id = 0x2A71;

    public event Action? Pressed;

    public bool Register(WindowInteropHelper helper, string hotkey)
    {
        if (_source is null)
        {
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(WndProc);
        }

        var (mods, vk) = Parse(hotkey);
        if (vk == 0) return false;
        return NativeMethods.RegisterHotKey(helper.Handle, Id, mods, vk);
    }

    public void Unregister(WindowInteropHelper helper)
    {
        if (helper.Handle != IntPtr.Zero)
            NativeMethods.UnregisterHotKey(helper.Handle, Id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Id)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static (uint Mods, uint Vk) Parse(string hotkey)
    {
        uint mods = 0;
        uint key = 0;

        foreach (var raw in Regex.Split(hotkey ?? string.Empty, @"\s*\+\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var part = raw.Trim().ToUpperInvariant();
            if (part == "ALT") mods |= NativeMethods.MOD_ALT;
            else if (part == "CTRL" || part == "CONTROL") mods |= NativeMethods.MOD_CONTROL;
            else if (part == "SHIFT") mods |= NativeMethods.MOD_SHIFT;
            else if (part == "WIN" || part == "WINDOWS") mods |= NativeMethods.MOD_WIN;
            else key = GetVirtualKey(part);
        }

        return (mods, key);
    }

    private static uint GetVirtualKey(string part)
    {
        if (part == "SPACE") return 0x20;
        if (part == "TAB") return 0x09;
        if (part == "ENTER" || part == "RETURN") return 0x0D;
        if (part == "ESC" || part == "ESCAPE") return 0x1B;
        if (part == "BACKSPACE") return 0x08;
        if (part == "DELETE" || part == "DEL") return 0x2E;
        if (part == "INSERT" || part == "INS") return 0x2D;
        if (part == "HOME") return 0x24;
        if (part == "END") return 0x23;
        if (part == "PAGEUP" || part == "PGUP") return 0x21;
        if (part == "PAGEDOWN" || part == "PGDN") return 0x22;
        if (part == "LEFT") return 0x25;
        if (part == "UP") return 0x26;
        if (part == "RIGHT") return 0x27;
        if (part == "DOWN") return 0x28;

        if (part.Length == 1)
        {
            var c = part[0];
            if (c is >= 'A' and <= 'Z') return c;
            if (c is >= '0' and <= '9') return c;
        }

        if (part.StartsWith('F') && int.TryParse(part[1..], out var f) && f is >= 1 and <= 24)
            return (uint)(0x70 + f - 1);

        return 0;
    }

    public static string? GetDisplayKey(Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return null;

        if (key == Key.Space) return "Space";
        if (key == Key.Tab) return "Tab";
        if (key == Key.Enter) return "Enter";
        if (key == Key.Escape) return "Esc";
        if (key == Key.Back) return "Backspace";
        if (key == Key.Delete) return "Delete";
        if (key == Key.Insert) return "Insert";
        if (key == Key.Home) return "Home";
        if (key == Key.End) return "End";
        if (key == Key.PageUp) return "PageUp";
        if (key == Key.PageDown) return "PageDown";
        if (key == Key.Left) return "Left";
        if (key == Key.Up) return "Up";
        if (key == Key.Right) return "Right";
        if (key == Key.Down) return "Down";

        if (key is >= Key.A and <= Key.Z) return key.ToString();
        if (key is >= Key.D0 and <= Key.D9) return key.ToString()[1..];
        if (key is >= Key.F1 and <= Key.F24) return key.ToString();

        return key switch
        {
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemMinus => "-",
            Key.OemPlus => "+",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemBackslash => "\\",
            Key.OemPipe => "\\",
            Key.OemTilde => "`",
            _ => null
        };
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }
}
