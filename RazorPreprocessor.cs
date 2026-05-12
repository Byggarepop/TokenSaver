using System.Text;

namespace RoslynLean;

internal static class RazorPreprocessor
{
    public static bool IsRazor(string path) =>
        path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

    public static string ExtractCSharp(string razorSource)
    {
        var sb = new StringBuilder();

        foreach (var ns in FindLines(razorSource, "@using"))
            sb.Append("using ").Append(ns).AppendLine(";");
        sb.AppendLine();

        sb.AppendLine("internal sealed class _RazorComponent");
        sb.AppendLine("{");

        foreach (var body in ExtractBlocks(razorSource, "@code"))
            sb.AppendLine(body);
        foreach (var body in ExtractBlocks(razorSource, "@functions"))
            sb.AppendLine(body);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<string> FindLines(string source, string directive)
    {
        foreach (var line in source.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith(directive + " ", StringComparison.Ordinal))
                continue;
            var rest = trimmed.Substring(directive.Length + 1).Trim().TrimEnd(';');
            if (rest.Length > 0)
                yield return rest;
        }
    }

    private static IEnumerable<string> ExtractBlocks(string source, string directive)
    {
        int i = 0;
        while (true)
        {
            var idx = source.IndexOf(directive, i, StringComparison.Ordinal);
            if (idx < 0)
                yield break;

            // Don't match "@codeblock", "@functions2", etc.
            var after = idx + directive.Length;
            if (after < source.Length && (char.IsLetterOrDigit(source[after]) || source[after] == '_'))
            {
                i = idx + 1;
                continue;
            }

            var brace = source.IndexOf('{', after);
            if (brace < 0)
                yield break;

            int depth = 1;
            int pos = brace + 1;

            while (pos < source.Length && depth > 0)
            {
                char c = source[pos];
                char next = pos + 1 < source.Length ? source[pos + 1] : '\0';

                if (c == '/' && next == '/')
                {
                    // line comment — skip to end of line
                    pos += 2;
                    while (pos < source.Length && source[pos] != '\n') pos++;
                }
                else if (c == '/' && next == '*')
                {
                    // block comment — skip to */
                    pos += 2;
                    while (pos + 1 < source.Length && !(source[pos] == '*' && source[pos + 1] == '/')) pos++;
                    pos += 2;
                }
                else if (c == '@' && next == '"')
                {
                    // verbatim string @"..." — "" is escaped quote inside
                    pos += 2;
                    while (pos < source.Length)
                    {
                        if (source[pos] == '"')
                        {
                            pos++;
                            if (pos < source.Length && source[pos] == '"') pos++; // escaped ""
                            else break;
                        }
                        else pos++;
                    }
                }
                else if (c == '"')
                {
                    // regular string — backslash escaping
                    pos++;
                    while (pos < source.Length && source[pos] != '"')
                    {
                        if (source[pos] == '\\') pos++;
                        pos++;
                    }
                    pos++; // closing "
                }
                else if (c == '\'')
                {
                    // character literal
                    pos++;
                    while (pos < source.Length && source[pos] != '\'')
                    {
                        if (source[pos] == '\\') pos++;
                        pos++;
                    }
                    pos++; // closing '
                }
                else if (c == '{') { depth++; pos++; }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) break;
                    pos++;
                }
                else pos++;
            }

            if (depth != 0)
                yield break;

            yield return source.Substring(brace + 1, pos - brace - 1);
            i = pos + 1;
        }
    }
}
