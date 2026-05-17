using System.Text;

namespace RoslynLean;

internal static class CppMinifier
{
    public static string StripAndCollapse(string src)
    {
        var sb = new StringBuilder(src.Length);
        int i = 0;
        bool prevWasSpace = true;
        bool atInputLineStart = true;

        while (i < src.Length)
        {
            char c = src[i];
            char next = i + 1 < src.Length ? src[i + 1] : '\0';

            // Line comment
            if (c == '/' && next == '/')
            {
                while (i < src.Length && src[i] != '\n') i++;
                EmitSpace(sb, ref prevWasSpace);
                continue;
            }

            // Block comment
            if (c == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                i = Math.Min(src.Length, i + 2);
                EmitSpace(sb, ref prevWasSpace);
                continue;
            }

            // Preprocessor directive — preserve as its own line (handles \ continuation)
            if (c == '#' && atInputLineStart)
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                    sb.Append('\n');
                while (i < src.Length)
                {
                    char dc = src[i];
                    if (dc == '\n')
                    {
                        sb.Append('\n');
                        i++;
                        // Line continuation: \ immediately before \n means the directive continues
                        if (sb.Length >= 2 && sb[sb.Length - 2] == '\\')
                            continue;
                        break;
                    }
                    sb.Append(dc);
                    i++;
                }
                prevWasSpace = true;
                atInputLineStart = true;
                continue;
            }

            // String literal
            if (c == '"')
            {
                i = CopyQuotedLiteral(src, i, '"', sb);
                prevWasSpace = false;
                atInputLineStart = false;
                continue;
            }

            // Character literal
            if (c == '\'')
            {
                i = CopyQuotedLiteral(src, i, '\'', sb);
                prevWasSpace = false;
                atInputLineStart = false;
                continue;
            }

            // Newline resets line-start tracking
            if (c == '\n')
            {
                atInputLineStart = true;
                EmitSpace(sb, ref prevWasSpace);
                i++;
                continue;
            }

            // Other whitespace (space, tab, \r)
            if (char.IsWhiteSpace(c))
            {
                EmitSpace(sb, ref prevWasSpace);
                i++;
                continue;
            }

            sb.Append(c);
            prevWasSpace = false;
            atInputLineStart = false;
            i++;
        }

        return sb.ToString().Trim();
    }

    private static int CopyQuotedLiteral(string src, int start, char quote, StringBuilder sb)
    {
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
