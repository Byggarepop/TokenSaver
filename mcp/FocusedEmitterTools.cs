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
        [Description("Absolute path to the .cs or .razor.cs file")] string filePath,
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
        [Description("Absolute path to the .cs or .razor.cs file")] string filePath)
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
        "Returns a minified C# file with PRIVATE methods, properties, fields, " +
        "and events renamed to short codes (M1, P1, F1, E1...). A symbol ledger " +
        "is prepended so the AI can map back. Public/internal/protected names are " +
        "left alone — they may be called from other files we can't see. Best on " +
        "files with many long private symbol names. On files with few or short " +
        "private members, the ledger overhead can outweigh savings — use the " +
        "plain minify tool instead in that case.")]
    public static string AliasCSharpFile(
        [Description("Absolute path to the .cs or .razor.cs file")] string filePath)
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

    // Writes one line per tool invocation to %LOCALAPPDATA%\RoslynLeanMcp\invocations.log
    // so you can tail the file to confirm the AI is actually using these tools.
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RoslynLeanMcp", "invocations.log");

    private static void LogInvocation(string mode, int beforeTokens, int afterTokens)
    {
        var saved = beforeTokens - afterTokens;
        var pct = beforeTokens == 0 ? 0 : saved * 100 / beforeTokens;
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {mode}  before={beforeTokens} after={afterTokens} saved={saved} ({pct}%)";

        // Stderr so VS Output → GitHub Copilot shows it live.
        Console.Error.WriteLine($"[roslyn-lean] {line}");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, line + "\n");
        }
        catch
        {
            // File logging must never break the tool response.
        }
    }
}
