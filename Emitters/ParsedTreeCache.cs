using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RoslynLean;

/// <summary>
/// Process-wide cache of parsed C# syntax trees, keyed by absolute file path and
/// invalidated by last-write time — the same mtime mechanism as <see cref="EmissionCache"/>.
///
/// Purpose: the project-wide traversal tools (trace_callers / trace_implementors /
/// trace_di_registrations / map_project) each construct a fresh <see cref="ProjectTraversal"/>,
/// which would otherwise re-read and re-parse every .cs file on every call. In an agentic
/// loop that chains several such calls, this caches the expensive read+parse so only files
/// whose mtime changed are re-parsed. Discovery (directory enumeration) is NOT cached and
/// still runs each call, so newly created and deleted files are always picked up.
/// </summary>
internal static class ParsedTreeCache
{
    private static readonly ConcurrentDictionary<string, (DateTime Mtime, SyntaxTree Tree)> _cache = new();

    // Observable counters so reuse can be asserted in tests. Not used in production logic.
    internal static long Hits;
    internal static long Misses;

    /// <summary>
    /// Returns the cached syntax tree for <paramref name="filePath"/> when its on-disk
    /// last-write time is unchanged, otherwise reads and parses the file and caches the result.
    /// </summary>
    public static SyntaxTree GetOrParse(string filePath)
    {
        var mtime = File.GetLastWriteTimeUtc(filePath);
        if (_cache.TryGetValue(filePath, out var entry) && entry.Mtime == mtime)
        {
            Interlocked.Increment(ref Hits);
            return entry.Tree;
        }

        Interlocked.Increment(ref Misses);
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
        _cache[filePath] = (mtime, tree);
        return tree;
    }

    internal static void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref Hits, 0);
        Interlocked.Exchange(ref Misses, 0);
    }
}
