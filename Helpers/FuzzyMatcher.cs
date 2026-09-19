namespace QuickLaunch.Helpers;

public static class FuzzyMatcher
{
    public static double Score(string query, string candidate, string? pinyin = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return 0;
        query = query.Trim().ToLowerInvariant();
        var text = candidate.ToLowerInvariant();
        var py = pinyin?.ToLowerInvariant() ?? "";
        var path = text;
        if (text.Contains(query)) return 1000 - Math.Abs(text.Length - query.Length);
        if (!string.IsNullOrEmpty(py) && py.Contains(query)) return 850 - Math.Abs(py.Length - query.Length);
        var subseq = SubsequenceScore(query, text);
        var pysub = string.IsNullOrEmpty(py) ? 0 : SubsequenceScore(query, py);
        var pathsub = SubsequenceScore(query, path);
        return Math.Max(subseq, Math.Max(pysub + 30, pathsub - 20));
    }

    private static double SubsequenceScore(string q, string t)
    {
        int qi = 0, first = -1, last = -1;
        for (int i = 0; i < t.Length && qi < q.Length; i++)
        {
            if (t[i] == q[qi]) { if (first < 0) first = i; last = i; qi++; }
        }
        if (qi != q.Length) return 0;
        return 500 - (last - first) - (t.Length - q.Length) * 0.25;
    }
}
