namespace OperatorSample;

public class TokenBuffer
{
    private readonly List<string> _tokens = new();

    public int Count => _tokens.Count;

    // Indexer — expression-bodied (get-only)
    public string this[int index] => _tokens[index];

    // Indexer with explicit accessor list (get + set)
    public string this[string key]
    {
        get => _tokens.Find(t => t.StartsWith(key)) ?? "";
        set => _tokens.Add($"{key}={value}");
    }

    // Binary operator overload
    public static TokenBuffer operator +(TokenBuffer a, TokenBuffer b)
    {
        var result = new TokenBuffer();
        result._tokens.AddRange(a._tokens);
        result._tokens.AddRange(b._tokens);
        return result;
    }

    // Implicit conversion operator
    public static implicit operator string[](TokenBuffer b) => b._tokens.ToArray();

    // Explicit conversion operator
    public static explicit operator int(TokenBuffer b) => b._tokens.Count;

    public void Add(string token) => _tokens.Add(token);

    public string Describe()
    {
        _ = Count;
        _ = this[0];
        var arr = (string[])this;
        var n = (int)this;
        return $"{n} tokens";
    }
}
