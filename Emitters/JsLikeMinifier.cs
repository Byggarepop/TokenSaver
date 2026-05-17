using System.Text;

namespace RoslynLean;

/// <summary>
/// Shared lexical minifier for C-style languages: JavaScript, TypeScript, JSX.
/// Strips line and block comments, collapses whitespace to single spaces,
/// preserves string literals (single, double, backtick) verbatim.
/// </summary>
internal static class JsLikeMinifier
{
    public static string StripAndCollapse(string src)
    {
        var sb = new StringBuilder(src.Length);
        int i = 0;
        bool prevWasSpace = true;

        while (i < src.Length)
        {
            char c = src[i];
            char next = i + 1 < src.Length ? src[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                while (i < src.Length && src[i] != '\n') i++;
                EmitSpace(sb, ref prevWasSpace);
                continue;
            }

            if (c == '/' && next == '*')
            {
                i += 2;
                while (i < src.Length - 1 && !(src[i] == '*' && src[i + 1] == '/')) i++;
                i = Math.Min(src.Length, i + 2);
                EmitSpace(sb, ref prevWasSpace);
                continue;
            }

            if (c == '"' || c == '\'' || c == '`')
            {
                i = CopyStringLiteral(src, i, sb);
                prevWasSpace = false;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                EmitSpace(sb, ref prevWasSpace);
                i++;
                continue;
            }

            sb.Append(c);
            prevWasSpace = false;
            i++;
        }

        return sb.ToString().Trim();
    }

    private static int CopyStringLiteral(string src, int start, StringBuilder sb)
    {
        char quote = src[start];
        sb.Append(quote);
        int i = start + 1;
        while (i < src.Length)
        {
            char c = src[i];
            sb.Append(c);
            if (c == '\\' && i + 1 < src.Length)
            {
                sb.Append(src[i + 1]);
                i += 2;
                continue;
            }
            i++;
            if (c == quote) return i;
        }
        return i;
    }

    private static void EmitSpace(StringBuilder sb, ref bool prevWasSpace)
    {
        if (!prevWasSpace)
        {
            sb.Append(' ');
            prevWasSpace = true;
        }
    }
}
