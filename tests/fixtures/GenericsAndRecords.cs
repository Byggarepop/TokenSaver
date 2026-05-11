using System.Collections.Generic;

namespace Fixtures;

public sealed record Pair<TKey, TValue>(TKey Key, TValue Value)
{
    public string Render() => $"{Key}={Value}";
}

public sealed class Bag<T> where T : notnull
{
    private readonly Dictionary<T, int> _counts = new();

    public int Increment(T item) => _counts.TryGetValue(item, out var n) ? _counts[item] = n + 1 : _counts[item] = 1;

    public string Classify(T item) => item switch
    {
        null => "null",
        int i when i < 0 => "negative",
        int => "non-negative-int",
        string s when s.Length == 0 => "empty-string",
        string => "non-empty-string",
        _ => "other"
    };
}
