using System.Text;

namespace RoslynLean;

/// <summary>
/// YAML minifier. YAML is indentation-sensitive — leading whitespace is
/// load-bearing — so we cannot collapse whitespace the way JSON does.
///
/// What this does:
/// - Strips '#' line comments (string-aware: '#' inside "..." or '...' is preserved).
/// - Trims trailing whitespace on every line.
/// - Collapses runs of blank lines to a single blank line.
/// - Preserves all leading indentation verbatim.
///
/// Limitations:
/// - Block scalars (| and >) are not specially detected. Their bodies happen
///   to be preserved because we only strip trailing whitespace and never
///   touch leading whitespace, which is what those scalars rely on.
/// - Multi-document YAML (--- separators) is preserved as-is.
/// </summary>
public sealed class YamlEmitter : ILanguageEmitter
{
    public string Language => "YAML";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".yml",  StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = Strip(source);
        var notes =
            $"# YAML minify of {Path.GetFileName(filePath)}\n" +
            $"# '#' comments stripped, trailing whitespace removed, blank runs collapsed\n" +
            $"# Indentation preserved (YAML is indent-sensitive)\n";

        return new LanguageEmitResult(
            Found: true,
            Output: output,
            OriginalChars: source.Length,
            OutputChars: output.Length,
            Notes: notes);
    }

    private static string Strip(string src)
    {
        var noComments = StripHashComments(src);
        return TrimAndCollapseBlankRuns(noComments);
    }

    private static string StripHashComments(string src)
    {
        var sb = new StringBuilder(src.Length);
        int i = 0;
        while (i < src.Length)
        {
            char c = src[i];
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
                    if (sc == '\n') break;
                }
                continue;
            }
            if (c == '#')
            {
                while (i < src.Length && src[i] != '\n') i++;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static string TrimAndCollapseBlankRuns(string src)
    {
        var sb = new StringBuilder(src.Length);
        bool prevBlank = false;
        bool atStart = true;
        foreach (var raw in src.Split('\n'))
        {
            var line = raw.TrimEnd('\r', ' ', '\t');
            if (line.Length == 0)
            {
                if (prevBlank || atStart) continue;
                sb.Append('\n');
                prevBlank = true;
                continue;
            }
            sb.Append(line).Append('\n');
            prevBlank = false;
            atStart = false;
        }
        return sb.ToString().TrimEnd('\n');
    }
}
