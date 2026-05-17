using System.Text;

namespace RoslynLean;

/// <summary>
/// JSON / JSONC minifier. Strips comments (JSONC allows // and /* */),
/// collapses all whitespace outside string literals. Produces near-canonical
/// minified JSON. Lossless for both JSON and JSONC.
/// </summary>
public sealed class JsonEmitter : ILanguageEmitter
{
    public string Language => "JSON";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".json",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jsonc", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = Strip(source);
        var notes =
            $"// JSON minify of {Path.GetFileName(filePath)}\n" +
            $"// Whitespace removed outside strings; // and /* */ comments stripped (JSONC)\n";

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
        while (i < src.Length)
        {
            char c = src[i];
            char next = i + 1 < src.Length ? src[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                while (i < src.Length && src[i] != '\n') i++;
                continue;
            }
            if (c == '/' && next == '*')
            {
                i += 2;
                while (i < src.Length - 1 && !(src[i] == '*' && src[i + 1] == '/')) i++;
                i = Math.Min(src.Length, i + 2);
                continue;
            }
            if (c == '"')
            {
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
                    if (sc == '"') break;
                }
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }
}
