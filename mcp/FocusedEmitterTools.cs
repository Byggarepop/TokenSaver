// Three MCP tools wrapping the RoslynLean emitter.
// Tool descriptions matter: they're how the host's model picks the right one.
// Each result starts with a one-line token-comparison header so the AI can
// surface "I used the focused emitter, saved ~X tokens" to the user.

using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynLean;

namespace RoslynLean.Mcp;

[McpServerToolType]
public static class FocusedEmitterTools
{
    [McpServerTool, Description(
        "Returns a focused subset of a C# file: the named method with full body, " +
        "plus the SIGNATURES of anything it references. Drops unrelated members " +
        "entirely. Use this when the user asks about a specific method — refactor, " +
        "translate, debug, optimize, or understand it. Far cheaper than reading " +
        "the whole file. Set depth=1 to also include the bodies of private helper " +
        "methods that the focus method calls (recommended for refactor/translate " +
        "tasks where the AI needs to see real helper logic, not just signatures). " +
        "Set minify=true for an additional ~15-25% token reduction (lossless).")]
    public static string FocusMethod(
        [Description("Absolute path to a .cs, .razor.cs, or .razor file. For .razor, only the @code / @functions blocks are analyzed.")] string filePath,
        [Description("The method name to focus on. Overloads are all included.")] string methodName,
        [Description("0 = signatures only for callees (default). 1 = include private helper bodies. 2+ = recursive.")] int depth = 0,
        [Description("If true, strip comments and collapse whitespace for additional token savings.")] bool minify = false)
    {
        try
        {
            var emitter = new FocusedEmitter(filePath);
            var result = emitter.Emit(methodName, depth);

            if (!result.Found)
                return $"ERROR: {result.Output}";

            var output = result.Output;
            if (minify)
                output = FocusedEmitter.MinifyText(output);

            var beforeTokens = result.OriginalTokensEstimate;
            var afterTokens = Math.Max(1, output.Length / 4);
            return BuildHeader(beforeTokens, afterTokens, $"focus={methodName} depth={depth} minify={minify}")
                 + result.Notes
                 + "\n"
                 + output;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Returns a lossless minified copy of a C# file: comments and XML docs " +
        "are stripped, whitespace is collapsed, but every line of LOGIC is " +
        "preserved verbatim (Roslyn parses and re-emits the syntax tree). Use " +
        "this when the AI genuinely needs the entire file — for cross-cutting " +
        "questions, multi-method analysis, or when you don't yet know which " +
        "method matters. Typical reduction: 20-50% depending on comment density.")]
    public static string MinifyCSharpFile(
        [Description("Absolute path to a .cs, .razor.cs, or .razor file. For .razor, only the @code / @functions blocks are analyzed.")] string filePath)
    {
        try
        {
            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitMinified();

            return BuildHeader(result.OriginalTokensEstimate, result.FocusedTokensEstimate, "whole-file lossless minify")
                 + result.Notes
                 + "\n"
                 + result.Output;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Auto-dispatch minifier for any supported language. Detects the language " +
        "from the file extension and applies a language-appropriate minifier. " +
        "Currently supports C# (.cs, .razor.cs, .razor), JavaScript (.js, .mjs, " +
        ".cjs, .jsx), TypeScript (.ts, .tsx, .mts, .cts), and Python (.py, .pyi). " +
        "Non-C# minifiers are simpler (no semantic model, no symbol resolution) " +
        "but still strip comments and collapse whitespace for typical 30-50% " +
        "reductions. Python preserves indentation since the language is " +
        "indent-sensitive. Use this when you don't yet know what language a " +
        "file is, or when working in a polyglot codebase.")]
    public static string MinifyFile(
        [Description("Absolute path to a source file. Language is detected by extension.")] string filePath)
    {
        try
        {
            var emitter = LanguageEmitterRegistry.Find(filePath);
            if (emitter is null)
                return $"ERROR: No minifier registered for extension '{Path.GetExtension(filePath)}'.";

            var result = emitter.Minify(filePath);
            return BuildHeader(result.OriginalTokensEstimate, result.OutputTokensEstimate, $"{emitter.Language} minify")
                 + result.Notes
                 + "\n"
                 + result.Output;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Returns a minified C# file with PRIVATE methods, properties, fields, " +
        "and events renamed to short codes (M1, P1, F1, E1...). A symbol ledger " +
        "is prepended so the AI can map back. Public/internal/protected names are " +
        "left alone — they may be called from other files we can't see. Best on " +
        "files with many long private symbol names. On files with few or short " +
        "private members, the ledger overhead can outweigh savings — use the " +
        "plain minify tool instead in that case.")]
    public static string AliasCSharpFile(
        [Description("Absolute path to a .cs, .razor.cs, or .razor file. For .razor, only the @code / @functions blocks are analyzed.")] string filePath)
    {
        try
        {
            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitAliased();

            return BuildHeader(result.OriginalTokensEstimate, result.FocusedTokensEstimate, "aliased + minified")
                 + result.Notes
                 + "\n"
                 + result.Output;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static string BuildHeader(int before, int after, string mode)
    {
        var saved = Math.Max(0, before - after);
        var pct = before == 0 ? 0 : saved * 100 / before;
        LogInvocation(mode, before, after);
        return $"// [Focused Emitter] Tokens without tool: {before:N0}  →  with tool: {after:N0}  ({pct}% saved) — mode: {mode}\n";
    }

    // Every invocation is appended to the shared report JSON at
    // %USERPROFILE%\token-saver-report.json so the Blazor viewer (and any
    // future surface) sees CLI and MCP traffic in one place.
    private static void LogInvocation(string mode, int beforeTokens, int afterTokens)
    {
        var saved = beforeTokens - afterTokens;
        var pct = beforeTokens == 0 ? 0 : saved * 100 / beforeTokens;

        // Stderr so VS Output → GitHub Copilot shows it live.
        Console.Error.WriteLine(
            $"[roslyn-lean] {DateTime.Now:yyyy-MM-dd HH:mm:ss}  {mode}  " +
            $"before={beforeTokens} after={afterTokens} saved={saved} ({pct}%)");

        try
        {
            TokenSaver.ReportWriter.Append(
                toolName: ToolNameForMode(mode),
                tokensWithoutTool: beforeTokens,
                tokensWithTool: afterTokens,
                notes: $"mode: {mode}",
                source: "mcp");
        }
        catch
        {
            // Report writing must never break the tool response.
        }
    }

    private static string ToolNameForMode(string mode) => mode switch
    {
        "focus" => "Focused Emitter",
        "minified" => "Minify",
        "aliased + minified" => "Alias",
        _ => mode,
    };
}
