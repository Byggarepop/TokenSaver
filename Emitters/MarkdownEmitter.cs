using System.Text;

namespace RoslynLean;

public sealed class MarkdownEmitter : ILanguageEmitter
{
    public string Language => "Markdown";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    public LanguageEmitResult Minify(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var source = File.ReadAllText(filePath);
        var output = Strip(source);
        var notes = $"<!-- Markdown minify of {Path.GetFileName(filePath)} -->\n"
                  + $"<!-- HTML comments stripped, consecutive blank lines collapsed -->\n";
        return new LanguageEmitResult(Found: true, Output: output,
            OriginalChars: source.Length, OutputChars: output.Length, Notes: notes);
    }

    internal static string Strip(string src)
    {
        // Remove HTML comments (<!-- ... -->) which are valid in Markdown but invisible in output
        var sb = new StringBuilder(src.Length);
        int i = 0;
        while (i < src.Length)
        {
            if (i + 3 < src.Length && src[i] == '<' && src[i + 1] == '!'
                && src[i + 2] == '-' && src[i + 3] == '-')
            {
                int end = src.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? src.Length : end + 3;
                // Skip any trailing newline that belonged solely to the comment line
                if (i < src.Length && src[i] == '\n') i++;
                else if (i + 1 < src.Length && src[i] == '\r' && src[i + 1] == '\n') i += 2;
                continue;
            }

            sb.Append(src[i]);
            i++;
        }

        // Collapse runs of more than one consecutive blank line into a single blank line.
        // Indentation is preserved — Markdown is indent-sensitive (code blocks, nested lists).
        var result = new StringBuilder(sb.Length);
        int blankRun = 0;
        bool atStart = true;
        foreach (var raw in sb.ToString().Split('\n'))
        {
            var trimmed = raw.TrimEnd('\r');
            if (trimmed.Trim().Length == 0)
            {
                if (!atStart) blankRun++;
                continue;
            }

            if (blankRun > 0)
            {
                result.Append('\n'); // emit exactly one blank line
                blankRun = 0;
            }

            result.Append(trimmed).Append('\n');
            atStart = false;
        }

        return result.ToString().TrimEnd('\n');
    }
}
