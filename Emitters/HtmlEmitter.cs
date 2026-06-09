using System.Text;

namespace RoslynLean;

/// <summary>
/// HTML minifier. Strips <!-- ... --> comments and collapses runs of inter-tag
/// whitespace to a single space. Element text content is left intact — for
/// general HTML, inner-text whitespace can be significant (think &lt;pre&gt;,
/// or formatted email templates), so we err on the safe side.
///
/// Used directly for .html / .htm, and indirectly by RazorEmitter for the
/// markup portion of .razor components.
/// </summary>
public sealed class HtmlEmitter : ILanguageEmitter
{
    public string Language => "HTML";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".htm",  StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = Strip(source);
        var notes =
            $"<!-- HTML minify of {Path.GetFileName(filePath)} — comments stripped, inter-tag whitespace collapsed -->\n";

        return new LanguageEmitResult(
            Found: true,
            Output: output,
            OriginalChars: source.Length,
            OutputChars: output.Length,
            Notes: notes);
    }

    /// <summary>
    /// Exposed for <see cref="RazorEmitter"/> so it can minify the markup
    /// portion of a .razor file without re-reading from disk.
    /// </summary>
    internal static string Strip(string src)
    {
        // Strip <!-- ... --> comments first.
        var sb = new StringBuilder(src.Length);
        int i = 0;
        while (i < src.Length)
        {
            if (i + 3 < src.Length && src[i] == '<' && src[i + 1] == '!' && src[i + 2] == '-' && src[i + 3] == '-')
            {
                int end = src.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? src.Length : end + 3;
                continue;
            }
            sb.Append(src[i]);
            i++;
        }

        // Collapse whitespace runs to single spaces, but preserve newlines as
        // newlines so the output is still readable. Trim trailing whitespace
        // per line and drop blank-line runs.
        var step1 = sb.ToString();
        var output = new StringBuilder(step1.Length);
        bool prevBlank = false;
        bool atStart = true;
        foreach (var raw in step1.Split('\n'))
        {
            var trimmed = raw.TrimEnd('\r', ' ', '\t');
            // Collapse internal whitespace runs to single spaces.
            var collapsed = CollapseInternalWhitespace(trimmed);
            if (collapsed.Length == 0)
            {
                if (prevBlank || atStart) continue;
                output.Append('\n');
                prevBlank = true;
                continue;
            }
            output.Append(collapsed).Append('\n');
            prevBlank = false;
            atStart = false;
        }
        return output.ToString().TrimEnd('\n');
    }

    private static string CollapseInternalWhitespace(string line)
    {
        var sb = new StringBuilder(line.Length);
        bool inAttrValue = false;
        char attrQuote = '\0';
        bool prevSpace = false;
        foreach (var c in line)
        {
            if (inAttrValue)
            {
                sb.Append(c);
                if (c == attrQuote) inAttrValue = false;
                continue;
            }
            if (c == '"' || c == '\'')
            {
                sb.Append(c);
                inAttrValue = true;
                attrQuote = c;
                prevSpace = false;
                continue;
            }
            if (c == ' ' || c == '\t')
            {
                if (prevSpace) continue;
                sb.Append(' ');
                prevSpace = true;
                continue;
            }
            sb.Append(c);
            prevSpace = false;
        }
        // Trim leading/trailing space we may have introduced.
        return sb.ToString().Trim();
    }
}
