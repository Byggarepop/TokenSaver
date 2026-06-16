namespace RoslynLean;

internal static class RazorPreprocessor
{
    public static bool IsRazor(string path) =>
        path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts the C# from a .razor file's <c>@code</c> / <c>@functions</c> blocks into a
    /// single synthetic component class, <b>preserving original line numbers</b>: every
    /// extracted line stays on the line it occupied in the .razor file, markup lines become
    /// blank, and the class braces reuse the first block's <c>{</c> and the last block's
    /// <c>}</c>. As a result the syntax-tree line spans — and the <c>// L..</c> ranges the
    /// outline prints — map straight back to the real file, so a narrow Read of a printed
    /// range lands on the right lines.
    /// </summary>
    public static string ExtractCSharp(string razorSource)
    {
        var src = razorSource.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = src.Split('\n');
        var outLines = new string[lines.Length];
        for (int i = 0; i < outLines.Length; i++)
            outLines[i] = "";

        var spans = ExtractBlockSpans(src);

        if (spans.Count == 0)
            // No @code/@functions — emit an empty component so parsing yields no members.
            return "internal sealed class _RazorComponent\n{\n}\n";

        int firstOpenLine = LineOf(src, spans[0].Open);
        int lastCloseLine = LineOf(src, spans[^1].Close);

        // @using directives before the first code block become using statements, kept on
        // their own lines so nothing downstream shifts. (A using after the class would be
        // invalid C#, so anything at/after the first block is left as markup.)
        for (int i = 0; i < lines.Length && i < firstOpenLine; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("@using ", StringComparison.Ordinal))
                continue;
            var rest = trimmed.Substring("@using ".Length).Trim().TrimEnd(';');
            if (rest.Length > 0)
                outLines[i] = "using " + rest + ";";
        }

        // Each block's body, placed verbatim at its original line numbers. A block's body is
        // a contiguous substring of the source, so its internal newlines keep every line in
        // place. Multiple blocks all flow into the one class.
        foreach (var (open, close) in spans)
        {
            int openLine = LineOf(src, open);
            var body = src.Substring(open + 1, close - open - 1);
            var bodyLines = body.Split('\n');
            for (int k = 0; k < bodyLines.Length; k++)
            {
                int target = openLine + k;
                outLines[target] = outLines[target].Length == 0
                    ? bodyLines[k]
                    : outLines[target] + " " + bodyLines[k];
            }
        }

        // Reuse the first block's '{' to open the class and the last block's '}' to close it;
        // every intermediate block's own braces are simply absorbed.
        outLines[firstOpenLine] = "internal sealed class _RazorComponent { " + outLines[firstOpenLine];
        outLines[lastCloseLine] = outLines[lastCloseLine] + " }";

        return string.Join("\n", outLines);
    }

    private static int LineOf(string source, int index)
    {
        int line = 0;
        int max = Math.Min(index, source.Length);
        for (int k = 0; k < max; k++)
            if (source[k] == '\n')
                line++;
        return line;
    }

    // Yields the (Open, Close) char indices of the '{' and its matching '}' for every
    // @code / @functions block, in document order. String/char literals and comments are
    // skipped so a brace inside them never miscounts depth.
    private static List<(int Open, int Close)> ExtractBlockSpans(string source)
    {
        var spans = new List<(int Open, int Close)>();

        foreach (var directive in new[] { "@code", "@functions" })
        {
            int i = 0;
            while (true)
            {
                var idx = source.IndexOf(directive, i, StringComparison.Ordinal);
                if (idx < 0)
                    break;

                // Don't match "@codeblock", "@functions2", etc.
                var after = idx + directive.Length;
                if (after < source.Length && (char.IsLetterOrDigit(source[after]) || source[after] == '_'))
                {
                    i = idx + 1;
                    continue;
                }

                var brace = source.IndexOf('{', after);
                if (brace < 0)
                    break;

                var close = FindMatchingBrace(source, brace);
                if (close < 0)
                    break;

                spans.Add((brace, close));
                i = close + 1;
            }
        }

        spans.Sort((a, b) => a.Open.CompareTo(b.Open));
        return spans;
    }

    // Returns the index of the '}' that matches the '{' at <paramref name="openBrace"/>,
    // or -1 if unbalanced. Skips line/block comments and string/char/verbatim literals.
    private static int FindMatchingBrace(string source, int openBrace)
    {
        int depth = 1;
        int pos = openBrace + 1;

        while (pos < source.Length && depth > 0)
        {
            char c = source[pos];
            char next = pos + 1 < source.Length ? source[pos + 1] : '\0';

            if (c == '/' && next == '/')
            {
                pos += 2;
                while (pos < source.Length && source[pos] != '\n') pos++;
            }
            else if (c == '/' && next == '*')
            {
                pos += 2;
                while (pos + 1 < source.Length && !(source[pos] == '*' && source[pos + 1] == '/')) pos++;
                pos += 2;
            }
            else if (c == '@' && next == '"')
            {
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
                if (depth == 0) return pos;
                pos++;
            }
            else pos++;
        }

        return -1;
    }
}
