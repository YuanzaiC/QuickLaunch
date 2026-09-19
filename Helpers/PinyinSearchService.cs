using System.Reflection;

namespace QuickLaunch.Helpers;

public sealed class PinyinSearchService
{
    private static readonly Type? PinyinType = FindPinyinType();
    private static Type? FindPinyinType()
    {
        try
        {
            var asm = Assembly.Load("Net-Pinyin");
            return asm.GetType("NetPinyin") ?? asm.GetTypes().FirstOrDefault(t => t.Name == "NetPinyin");
        }
        catch { return null; }
    }

    public string GetPinyin(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        try
        {
            var method = PinyinType?.GetMethod("PinYin", new[] { typeof(string) });
            var value = method?.Invoke(null, new object[] { text }) as string;
            return RemoveTone(value ?? text).Replace(",", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }
        catch { return text.ToLowerInvariant(); }
    }

    public string GetInitials(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        try
        {
            var method = PinyinType?.GetMethod("PinYin", new[] { typeof(string) });
            var value = method?.Invoke(null, new object[] { text }) as string;
            if (string.IsNullOrWhiteSpace(value)) return text.ToLowerInvariant();
            var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(x => RemoveTone(x).Trim()).Where(x => x.Length > 0).Select(x => x[0])).ToLowerInvariant();
        }
        catch { return text.ToLowerInvariant(); }
    }

    private static string RemoveTone(string value)
    {
        var result = value;
        var map = new Dictionary<char,char>
        { ['ā']='a',['á']='a',['ǎ']='a',['à']='a', ['ē']='e',['é']='e',['ě']='e',['è']='e', ['ī']='i',['í']='i',['ǐ']='i',['ì']='i', ['ō']='o',['ó']='o',['ǒ']='o',['ò']='o', ['ū']='u',['ú']='u',['ǔ']='u',['ù']='u', ['ǖ']='v',['ǘ']='v',['ǚ']='v',['ǜ']='v', ['Ā']='A',['Á']='A',['Ǎ']='A',['À']='A', ['Ē']='E',['É']='E',['Ě']='E',['È']='E', ['Ī']='I',['Í']='I',['Ǐ']='I',['Ì']='I', ['Ō']='O',['Ó']='O',['Ǒ']='O',['Ò']='O', ['Ū']='U',['Ú']='U',['Ǔ']='U',['Ù']='U', ['Ǖ']='V',['Ǘ']='V',['Ǚ']='V',['Ǜ']='V' };
        foreach (var kv in map) result = result.Replace(kv.Key, kv.Value);
        return result;
    }
}
