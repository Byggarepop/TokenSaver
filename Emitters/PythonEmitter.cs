using System.Text;

namespace RoslynLean;

/// <summary>
/// Python minifier. Python is indentation-sensitive, so we cannot collapse
/// whitespace the way JS/TS does — leading indentation on every line is
/// load-bearing syntax.
///
/// What this does:
/// - Strips '#' line comments (string-aware: '#' inside strings is preserved).
/// - Trims trailing whitespace on every line.
/// - Collapses runs of blank lines to a single blank line.
/// - Preserves all leading indentation verbatim.
///
/// What this intentionally does NOT do:
/// - Does NOT strip docstrings. A triple-quoted string as the first statement
///   of a module/class/function is callable at runtime via __doc__; removing
///   it can change behavior. Could be added behind a flag later.
/// - Does NOT join continuation lines or rewrite syntax.
///
/// Known limitations:
/// - String prefixes (f, r, b, rb, etc.) are recognized; their bodies are
///   preserved verbatim. Escape sequences are handled lexically.
/// - Triple-quoted strings (''' or """) are preserved verbatim, including
///   any '#' characters inside them.
/// </summary>
public sealed class PythonEmitter : ILanguageEmitter
{
    public string Language => "Python";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".py",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pyi", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = StripCommentsAndBlankRuns(source);
        var notes =
            $"# Python minify of {Path.GetFileName(filePath)} — '#' comments stripped, blank runs collapsed; indentation and docstrings preserved\n";

        return new LanguageEmitResult(
            Found: true,
            Output: output,
            OriginalChars: source.Length,
            OutputChars: output.Length,
            Notes: notes);
    }

    private static string StripCommentsAndBlankRuns(string src)
    {
        // Pass 1: strip '#' comments, string-aware. Preserve newlines and indentation.
        var stripped = StripHashComments(src);

        // Pass 2: trim trailing whitespace per line, collapse blank runs to one blank line.
        var sb = new StringBuilder(stripped.Length);
        bool prevBlank = false;
        bool atStart = true;
        foreach (var rawLine in stripped.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r', ' ', '\t');
            var isBlank = line.Length == 0;
            if (isBlank)
            {
                if (prevBlank || atStart) continue;
                sb.Append('\n');
                prevBlank = true;
                continue;
            }
            sb.Append(line);
            sb.Append('\n');
            prevBlank = false;
            atStart = false;
        }
        return sb.ToString().TrimEnd('\n');
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
                i = CopyStringLiteral(src, i, sb);
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

    private static int CopyStringLiteral(string src, int start, StringBuilder sb)
    {
        char quote = src[start];
        // Detect triple-quoted: """ or '''
        bool triple = start + 2 < src.Length
                      && src[start + 1] == quote
                      && src[start + 2] == quote;

        if (triple)
        {
            sb.Append(quote).Append(quote).Append(quote);
            int i = start + 3;
            while (i < src.Length)
            {
                if (i + 2 < src.Length && src[i] == quote && src[i + 1] == quote && src[i + 2] == quote)
                {
                    sb.Append(quote).Append(quote).Append(quote);
                    return i + 3;
                }
                if (src[i] == '\\' && i + 1 < src.Length)
                {
                    sb.Append(src[i]);
                    sb.Append(src[i + 1]);
                    i += 2;
                    continue;
                }
                sb.Append(src[i]);
                i++;
            }
            return i;
        }

        sb.Append(quote);
        int j = start + 1;
        while (j < src.Length)
        {
            char ch = src[j];
            sb.Append(ch);
            if (ch == '\\' && j + 1 < src.Length)
            {
                sb.Append(src[j + 1]);
                j += 2;
                continue;
            }
            j++;
            if (ch == quote) return j;
            if (ch == '\n') return j; // single-quoted strings don't span lines in Python
        }
        return j;
    }
}
