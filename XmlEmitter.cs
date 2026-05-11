using System.Text;

namespace RoslynLean;

/// <summary>
/// XML minifier. Strips <!-- ... --> comments, trims trailing whitespace,
/// collapses runs of blank lines. Conservative — does not collapse inter-tag
/// whitespace because in general XML it can be semantically significant
/// (mixed content, xml:space="preserve", etc.).
///
/// Handles .csproj, .props, .targets, .config, .xml, .resx — the typical
/// .NET project XML formats — but is safe for arbitrary XML.
/// </summary>
public sealed class XmlEmitter : ILanguageEmitter
{
    public string Language => "XML";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".xml",     StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".csproj",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".props",   StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".targets", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".config",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".resx",    StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = Strip(source);
        var notes =
            $"<!-- XML minify of {Path.GetFileName(filePath)} -->\n" +
            $"<!-- <!-- --> comments stripped, trailing whitespace removed, blank runs collapsed -->\n";

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
            // <!-- ... -->
            if (i + 3 < src.Length && src[i] == '<' && src[i + 1] == '!' && src[i + 2] == '-' && src[i + 3] == '-')
            {
                int end = src.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? src.Length : end + 3;
                continue;
            }
            sb.Append(src[i]);
            i++;
        }

        // Line-level cleanup
        var noComments = sb.ToString();
        var lines = new StringBuilder(noComments.Length);
        bool prevBlank = false;
        bool atStart = true;
        foreach (var raw in noComments.Split('\n'))
        {
            var line = raw.TrimEnd('\r', ' ', '\t');
            if (line.Length == 0)
            {
                if (prevBlank || atStart) continue;
                lines.Append('\n');
                prevBlank = true;
                continue;
            }
            lines.Append(line).Append('\n');
            prevBlank = false;
            atStart = false;
        }
        return lines.ToString().TrimEnd('\n');
    }
}
