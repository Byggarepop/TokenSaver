using System.Text;

namespace RoslynLean;

/// <summary>
/// CSS minifier. Strips /* ... */ comments (the only comment form in CSS),
/// collapses runs of whitespace outside string literals to single spaces.
/// Lossless for standard CSS, SCSS, and LESS at the lexical level.
/// </summary>
public sealed class CssEmitter : ILanguageEmitter
{
    public string Language => "CSS";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".css",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".scss", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".less", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = Strip(source);
        var notes =
            $"/* CSS minify of {Path.GetFileName(filePath)} — comments stripped, whitespace collapsed */\n";

        return new LanguageEmitResult(
            Found: true,
            Output: output,
            OriginalChars: source.Length,
            OutputChars: output.Length,
            Notes: notes);
    }

    private static string Strip(string src)
    {
        var sb = new StringBuilder(src.Length);
        int i = 0;
        bool prevSpace = true;
        while (i < src.Length)
        {
            char c = src[i];
            char next = i + 1 < src.Length ? src[i + 1] : '\0';

            if (c == '/' && next == '*')
            {
                i += 2;
                while (i < src.Length - 1 && !(src[i] == '*' && src[i + 1] == '/')) i++;
                i = Math.Min(src.Length, i + 2);
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
                continue;
            }
            if (c == '"' || c == '\'')
            {
                char quote = c;
                sb.Append(c);
                i++;
                while (i < src.Length)
                {
                    char sc = src[i];
                    sb.Append(sc);
                    if (sc == '\\' && i + 1 < src.Length)
                    {
                        sb.Append(src[i + 1]);
                        i += 2;
                        continue;
                    }
                    i++;
                    if (sc == quote) break;
                }
                prevSpace = false;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
                i++;
                continue;
            }
            sb.Append(c);
            prevSpace = false;
            i++;
        }
        return sb.ToString().Trim();
    }
}
