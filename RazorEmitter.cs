using System.Text;

namespace RoslynLean;

/// <summary>
/// Razor component (.razor) minifier. A .razor file mixes HTML-style markup
/// with C# inside @code / @functions blocks and inline @expression sites.
/// Previously the C# path (via RazorPreprocessor) preserved only the @code
/// portion and dropped all markup. This emitter closes that gap by producing
/// two sections back-to-back: the minified markup and the minified C# code.
///
/// Both halves carry their own headers so an LLM reading the output knows
/// which is which.
/// </summary>
public sealed class RazorEmitter : ILanguageEmitter
{
    public string Language => "Razor";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".razor", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);

        // Split: markup = the .razor source with @code/@functions blocks
        // removed. The C# portion is handled by the existing Razor → C#
        // pipeline (FocusedEmitter sees the synthetic class).
        var markupOnly = RemoveCodeBlocks(source);
        var minifiedMarkup = HtmlEmitter.Strip(markupOnly);

        // C# side: write source to a temp path won't help — FocusedEmitter
        // reads from disk. Just call the existing emitter for the .razor file
        // directly; RazorPreprocessor will extract @code and produce C#.
        var codeResult = new FocusedEmitter(filePath).EmitMinified();

        var sb = new StringBuilder();
        sb.Append("<!-- === RAZOR MARKUP (minified) === -->\n");
        sb.Append(minifiedMarkup);
        sb.Append("\n\n// === RAZOR @code (minified C#) ===\n");
        sb.Append(codeResult.Output);

        var output = sb.ToString();
        var notes =
            $"// Razor minify of {Path.GetFileName(filePath)}\n" +
            $"// Markup processed by HtmlEmitter; @code by Roslyn\n";

        return new LanguageEmitResult(
            Found: true,
            Output: output,
            OriginalChars: source.Length,
            OutputChars: output.Length,
            Notes: notes);
    }

    private static string RemoveCodeBlocks(string source)
    {
        var sb = new StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            int codeIdx = FindDirective(source, i, "@code");
            int funcIdx = FindDirective(source, i, "@functions");
            int next = MinPositive(codeIdx, funcIdx);
            if (next < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }
            sb.Append(source, i, next - i);
            int afterBlock = SkipBalancedBlock(source, next);
            if (afterBlock < 0)
            {
                // unbalanced — bail and append the rest verbatim
                sb.Append(source, next, source.Length - next);
                break;
            }
            i = afterBlock;
        }
        return sb.ToString();
    }

    private static int FindDirective(string src, int from, string directive)
    {
        int idx = src.IndexOf(directive, from, StringComparison.Ordinal);
        if (idx < 0) return -1;
        // Must be followed by whitespace or '{' to count as a directive (not
        // e.g. '@codepoint' as a random identifier).
        int after = idx + directive.Length;
        if (after >= src.Length) return -1;
        char c = src[after];
        if (!char.IsWhiteSpace(c) && c != '{') return FindDirective(src, after, directive);
        return idx;
    }

    private static int SkipBalancedBlock(string src, int start)
    {
        int brace = src.IndexOf('{', start);
        if (brace < 0) return -1;
        int depth = 1;
        int pos = brace + 1;
        while (pos < src.Length && depth > 0)
        {
            char c = src[pos];
            if (c == '{') depth++;
            else if (c == '}') depth--;
            if (depth == 0) return pos + 1;
            pos++;
        }
        return -1;
    }

    private static int MinPositive(int a, int b)
    {
        if (a < 0) return b;
        if (b < 0) return a;
        return Math.Min(a, b);
    }
}
