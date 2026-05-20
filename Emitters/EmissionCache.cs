namespace RoslynLean;

/// <summary>
/// In-process cache for focused emission results, keyed by file path, emission
/// key, depth, and minify flag. Invalidated per-entry by file last-write timestamp.
/// Benefit is server-side only: Roslyn re-parse is skipped on a cache hit, but
/// the full output is always returned so callers never depend on prior context.
/// </summary>
internal static class EmissionCache
{
    private static readonly Dictionary<(string FilePath, string Key, int Depth, bool Minify), (DateTime LastModified, string Output, int BeforeTokens, int AfterTokens)> _cache = new();

    /// <summary>
    /// Returns true and sets <paramref name="output"/>, <paramref name="beforeTokens"/>,
    /// and <paramref name="afterTokens"/> when a valid cached result exists and the file
    /// has not changed since it was stored. The returned output is the full emission with
    /// "[re-parse skipped]" appended to the header line.
    /// </summary>
    public static bool TryGet(string filePath, string key, int depth, bool minify,
        out string output, out int beforeTokens, out int afterTokens)
    {
        output = ""; beforeTokens = 0; afterTokens = 0;
        if (!_cache.TryGetValue((filePath, key, depth, minify), out var entry))
            return false;
        if (File.GetLastWriteTimeUtc(filePath) != entry.LastModified)
            return false;
        var nl = entry.Output.IndexOf('\n');
        output = nl >= 0
            ? entry.Output[..nl] + " [re-parse skipped]" + entry.Output[nl..]
            : entry.Output;
        beforeTokens = entry.BeforeTokens;
        afterTokens  = entry.AfterTokens;
        return true;
    }

    /// <summary>
    /// Stores <paramref name="output"/> and its token counts alongside the file's current
    /// last-write timestamp so future calls can validate freshness and log accurate stats.
    /// </summary>
    public static void Set(string filePath, string key, int depth, bool minify,
        string output, int beforeTokens, int afterTokens) =>
        _cache[(filePath, key, depth, minify)] = (File.GetLastWriteTimeUtc(filePath), output, beforeTokens, afterTokens);

    public static void Clear() => _cache.Clear();
}
