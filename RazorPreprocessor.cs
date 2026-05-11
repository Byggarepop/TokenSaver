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
            var brace = source.IndexOf('{', idx + directive.Length);
            if (brace < 0)
                yield break;

            int depth = 1;
            int pos = brace + 1;
            while (pos < source.Length && depth > 0)
            {
                var c = source[pos];
                if (c == '{') depth++;
                else if (c == '}') depth--;
                if (depth == 0) break;
                pos++;
            }
            if (depth != 0)
                yield break;

            yield return source.Substring(brace + 1, pos - brace - 1);
            i = pos + 1;
        }
    }
}
