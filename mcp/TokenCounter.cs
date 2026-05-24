using Microsoft.ML.Tokenizers;

namespace TokenSaver.Mcp;

internal static class TokenCounter
{
    private static readonly Lazy<Tokenizer?> _tokenizer =
        new(CreateTokenizer, LazyThreadSafetyMode.ExecutionAndPublication);

    public static int Count(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var tok = _tokenizer.Value;
        if (tok is null) return Math.Max(1, text.Length / 4);
        return Math.Max(1, tok.CountTokens(text));
    }

    private static Tokenizer? CreateTokenizer()
    {
        try { return TiktokenTokenizer.CreateForEncoding("cl100k_base"); }
        catch { return null; }
    }
}
