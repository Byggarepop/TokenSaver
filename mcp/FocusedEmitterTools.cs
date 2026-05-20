// Three MCP tools wrapping the RoslynLean emitter.
// Tool descriptions matter: they're how the host's model picks the right one.
// Each result starts with a one-line token-comparison header so the AI can
// surface "I used the focused emitter, saved ~X tokens" to the user.

using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynLean;

namespace TokenSaver.Mcp;

[McpServerToolType]
public static class FocusedEmitterTools
{
    [McpServerTool, Description(
        "Returns a focused subset of a C# or VB.NET file: the named method with full body, " +
        "plus the SIGNATURES of anything it references. Drops unrelated members " +
        "entirely. Use this when the user asks about a specific method — refactor, " +
        "translate, debug, optimize, or understand it. Far cheaper than reading " +
        "the whole file. Set depth=1 to also include the bodies of private helper " +
        "methods and properties that the focus method calls or accesses " +
        "(recommended for refactor/translate tasks where the AI needs to see real " +
        "helper logic, not just signatures). " +
        "Set minify=true for an additional ~15-25% token reduction (lossless). " +
        "Supports .cs, .razor.cs, .razor, and .vb files.")]
    public static string FocusMethod(
        [Description("Absolute path to a .cs, .razor.cs, .razor, or .vb file. For .razor, only the @code / @functions blocks are analyzed.")] string filePath,
        [Description("The method name to focus on. Overloads are all included.")] string methodName,
        [Description("0 = signatures only for callees (default). 1 = include private helper bodies. 2+ = recursive.")] int depth = 0,
        [Description("If true, strip comments and collapse whitespace for additional token savings.")] bool minify = false)
    {
        try
        {
            if (IsVbFile(filePath))
            {
                var vb = new VBFocusedEmitter(filePath);
                var vbResult = vb.Emit(methodName, depth);
                if (!vbResult.Found)
                {
                    var outline = vb.EmitOutline();
                    return $"ERROR: Method '{methodName}' not found in {Path.GetFileName(filePath)}.\n" +
                           $"Available members:\n{outline.Output}";
                }
                var vbOutput = minify ? VBFocusedEmitter.MinifyText(vbResult.Output) : vbResult.Output;
                return BuildHeader(vbResult.OriginalTokensEstimate, Math.Max(1, vbOutput.Length / 4), "Focused Emitter", "VB.NET", $"focus={methodName} depth={depth} minify={minify}")
                     + vbResult.Notes + "\n" + vbOutput;
            }

            if (TryGetCached(filePath, methodName, depth, minify, out var cached))
                return cached;

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.Emit(methodName, depth);

            if (!result.Found)
            {
                var outline = emitter.EmitOutline();
                LogInvocation("Focused Emitter", "C#", $"focus={methodName} depth={depth} NOT FOUND", outline.OriginalTokensEstimate, outline.OriginalTokensEstimate);
                return $"ERROR: Method '{methodName}' not found in {Path.GetFileName(filePath)}.\n" +
                       $"Available members:\n{outline.Output}";
            }

            var output = result.Output;
            if (minify)
                output = FocusedEmitter.MinifyText(output);

            var beforeTokens = result.OriginalTokensEstimate;
            var afterTokens = Math.Max(1, output.Length / 4);
            var fullOutput = BuildHeader(beforeTokens, afterTokens, "Focused Emitter", "C#", $"focus={methodName} depth={depth} minify={minify}")
                 + result.Notes
                 + "\n"
                 + output;
            SetCached(filePath, methodName, depth, minify, fullOutput, beforeTokens, afterTokens);
            return fullOutput;
        }
        catch (Exception ex)
        {
            LogInvocation("Focused Emitter", "C#", $"focus={methodName} EXCEPTION", 0, 0);
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Same as focus_method but focuses on MULTIPLE named methods in a single call. " +
        "The file is parsed once and referenced signatures are deduplicated across all " +
        "focus methods — so the combined output is smaller than N separate focus_method " +
        "calls, and you save N-1 round-trips. Use this when the user asks about two or " +
        "more specific methods together, or when a prior outline or NOT FOUND response " +
        "revealed a set of related methods to inspect. Provide method names as a " +
        "comma-separated list (e.g. 'ExecSql,ClearGrid,SetBusy'). depth=1 includes " +
        "private helper method and property bodies for ALL listed methods. " +
        "Supports .cs, .razor.cs, .razor, and .vb files.")]
    public static string FocusMultipleMethods(
        [Description("Absolute path to a .cs, .razor.cs, .razor, or .vb file.")] string filePath,
        [Description("Comma-separated method names, e.g. 'ExecSql,ClearGrid,SetBusy'.")] string methodNames,
        [Description("0 = signatures only for callees (default). 1 = include private helper bodies.")] int depth = 0,
        [Description("If true, strip comments and collapse whitespace for additional token savings.")] bool minify = false)
    {
        try
        {
            var names = methodNames
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();

            if (names.Count == 0)
                return "ERROR: No method names provided.";

            if (IsVbFile(filePath))
            {
                var vb = new VBFocusedEmitter(filePath);
                var vbResult = vb.EmitMultiple(names, depth);
                if (!vbResult.Found)
                {
                    var outline = vb.EmitOutline();
                    return $"ERROR: None of the requested methods found in {Path.GetFileName(filePath)}.\n" +
                           $"Available members:\n{outline.Output}";
                }
                var vbOutput = minify ? VBFocusedEmitter.MinifyText(vbResult.Output) : vbResult.Output;
                return BuildHeader(vbResult.OriginalTokensEstimate, Math.Max(1, vbOutput.Length / 4), "Focused Emitter (multi)", "VB.NET", $"focus=[{string.Join(",", names)}] depth={depth} minify={minify}")
                     + vbResult.Notes + "\n" + vbOutput;
            }

            var multiKey = string.Join("|", names.OrderBy(n => n));
            if (TryGetCached(filePath, multiKey, depth, minify, out var cached))
                return cached;

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitMultiple(names, depth);

            if (!result.Found)
            {
                var outline = emitter.EmitOutline();
                LogInvocation("Focused Emitter (multi)", "C#", $"focus=[{string.Join(",", names)}] depth={depth} NOT FOUND", outline.OriginalTokensEstimate, outline.OriginalTokensEstimate);
                return $"ERROR: None of the requested methods found in {Path.GetFileName(filePath)}.\n" +
                       $"Available members:\n{outline.Output}";
            }

            var output = minify ? FocusedEmitter.MinifyText(result.Output) : result.Output;
            var afterTokens = Math.Max(1, output.Length / 4);
            var fullOutput = BuildHeader(result.OriginalTokensEstimate, afterTokens, "Focused Emitter (multi)", "C#", $"focus=[{string.Join(",", names)}] depth={depth} minify={minify}")
                 + result.Notes
                 + "\n"
                 + output;
            SetCached(filePath, multiKey, depth, minify, fullOutput, result.OriginalTokensEstimate, afterTokens);
            return fullOutput;
        }
        catch (Exception ex)
        {
            LogInvocation("Focused Emitter (multi)", "C#", $"focus=[{methodNames}] EXCEPTION", 0, 0);
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

            var afterCharsCs = result.FocusedChars >= result.OriginalChars
                ? result.OriginalChars
                : result.FocusedChars;
            var bodyCs = result.FocusedChars >= result.OriginalChars
                ? File.ReadAllText(filePath)
                : result.Output;

            return BuildHeader(result.OriginalTokensEstimate, Math.Max(1, afterCharsCs / 4), "MinifyCSharpFile", "C#", "whole-file lossless minify")
                 + result.Notes
                 + "\n"
                 + bodyCs;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Auto-dispatch minifier for any supported file type. Detects format from " +
        "the file extension and applies a format-appropriate minifier. Currently " +
        "supports C# (.cs, .razor.cs), Razor components (.razor — markup + @code " +
        "combined), JavaScript (.js, .mjs, .cjs, .jsx), TypeScript (.ts, .tsx, " +
        ".mts, .cts), Python (.py, .pyi), HTML (.html, .htm), CSS/SCSS/LESS " +
        "(.css, .scss, .less), JSON/JSONC (.json, .jsonc), YAML (.yaml, .yml), " +
        "XML/.NET project files (.xml, .csproj, .props, .targets, .config, .resx), " +
        "C (.c, .h), C++ (.cpp, .cc, .cxx, .hpp, .hh, .hxx, .inl), " +
        "and VB.NET (.vb — Roslyn comment strip + blank-run collapse). " +
        "Code minifiers strip comments and collapse whitespace. " +
        "Indent-sensitive formats (Python, YAML) preserve leading indentation. " +
        "Use this when working in a polyglot codebase or when reading " +
        "config/project files.")]
    public static string MinifyFile(
        [Description("Absolute path to a source file. Language is detected by extension.")] string filePath)
    {
        try
        {
            var emitter = LanguageEmitterRegistry.Find(filePath);
            if (emitter is null)
                return $"ERROR: No minifier registered for extension '{Path.GetExtension(filePath)}'.";

            var result = emitter.Minify(filePath);

            var afterChars = result.OutputChars >= result.OriginalChars
                ? result.OriginalChars
                : result.OutputChars;
            var body = result.OutputChars >= result.OriginalChars
                ? File.ReadAllText(filePath)
                : result.Output;

            return BuildHeader(result.OriginalTokensEstimate, Math.Max(1, afterChars / 4), "MinifyFile", emitter.Language, $"{emitter.Language} minify")
                 + result.Notes
                 + "\n"
                 + body;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Returns a skeleton of a C# or VB.NET file: every type and every member as a " +
        "signature, with NO method/property bodies. Useful for codebase " +
        "navigation questions like 'what's in this file?' or 'where would I " +
        "add X?' where bodies aren't needed. Typical reduction: 70-95% on " +
        "large files. Much cheaper than MinifyCSharpFile when the task is " +
        "discovery rather than understanding implementation. " +
        "Supports .cs, .razor.cs, .razor, and .vb files.")]
    public static string OutlineCSharpFile(
        [Description("Absolute path to a .cs, .razor.cs, .razor, or .vb file.")] string filePath)
    {
        try
        {
            if (IsVbFile(filePath))
            {
                var vb = new VBFocusedEmitter(filePath);
                var vbResult = vb.EmitOutline();
                return BuildHeader(vbResult.OriginalTokensEstimate, vbResult.FocusedTokensEstimate, "OutlineCSharpFile", "VB.NET", "outline (signatures only)")
                     + vbResult.Notes + "\n" + vbResult.Output;
            }

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitOutline();
            return BuildHeader(result.OriginalTokensEstimate, result.FocusedTokensEstimate, "OutlineCSharpFile", "C#", "outline (signatures only)")
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

            return BuildHeader(result.OriginalTokensEstimate, result.FocusedTokensEstimate, "AliasCSharpFile", "C#", "aliased + minified")
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
        "Returns a focused view of a NAMED TYPE in a C# or VB.NET file: non-private members " +
        "(public, protected, internal/Friend) are shown with their full bodies; private " +
        "members are shown as signatures only. Sits between outline_c_sharp_file " +
        "(all signatures) and minify_c_sharp_file (everything) in terms of detail. " +
        "Best when: the file contains multiple types and you only need one; or you " +
        "want the full contract/behaviour of a class but can skip its private " +
        "implementation noise. Supply the simple class/record/interface name, not " +
        "the namespace-qualified form. Supports .cs, .razor.cs, and .vb files.")]
    public static string FocusType(
        [Description("Absolute path to a .cs, .razor.cs, or .vb file.")] string filePath,
        [Description("The simple type name to focus on (e.g. 'Calculator', not 'Fixtures.Calculator').")] string typeName,
        [Description("If true, strip comments and collapse whitespace for additional token savings.")] bool minify = false)
    {
        try
        {
            if (IsVbFile(filePath))
            {
                var vb = new VBFocusedEmitter(filePath);
                var vbResult = vb.EmitType(typeName);
                if (!vbResult.Found)
                {
                    var outline = vb.EmitOutline();
                    return $"ERROR: Type '{typeName}' not found in {Path.GetFileName(filePath)}.\n" +
                           $"Available types:\n{outline.Output}";
                }
                var vbOutput = minify ? VBFocusedEmitter.MinifyText(vbResult.Output) : vbResult.Output;
                return BuildHeader(vbResult.OriginalTokensEstimate, Math.Max(1, vbOutput.Length / 4), "FocusType", "VB.NET", $"type={typeName} minify={minify}")
                     + vbResult.Notes + "\n" + vbOutput;
            }

            if (TryGetCached(filePath, $"type:{typeName}", depth: 0, minify, out var cached))
                return cached;

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitType(typeName);

            if (!result.Found)
            {
                var outline = emitter.EmitOutline();
                LogInvocation("FocusType", "C#", $"type={typeName} NOT FOUND", outline.OriginalTokensEstimate, outline.OriginalTokensEstimate);
                return $"ERROR: Type '{typeName}' not found in {Path.GetFileName(filePath)}.\n" +
                       $"Available types:\n{outline.Output}";
            }

            var output = minify ? FocusedEmitter.MinifyText(result.Output) : result.Output;
            var afterTokens = Math.Max(1, output.Length / 4);
            var fullOutput = BuildHeader(result.OriginalTokensEstimate, afterTokens, "FocusType", "C#", $"type={typeName} minify={minify}")
                 + result.Notes
                 + "\n"
                 + output;
            SetCached(filePath, $"type:{typeName}", depth: 0, minify, fullOutput, result.OriginalTokensEstimate, afterTokens);
            return fullOutput;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Finds all methods in a C# or VB.NET file that CALL a given method name, then returns " +
        "them as a focused multi-method view (full bodies + shared signatures). " +
        "Answers 'what calls X?' or 'who uses this?' without loading the whole file. " +
        "Uses name-based matching, so it catches direct calls — calls through " +
        "delegates or interfaces may be missed. Set depth=1 to also include private " +
        "helper bodies of the found callers. Supports .cs, .razor.cs, and .vb files.")]
    public static string FocusCallers(
        [Description("Absolute path to a .cs, .razor.cs, or .vb file.")] string filePath,
        [Description("The method name to find callers of.")] string methodName,
        [Description("0 = signatures only for callees (default). 1 = include private helper bodies.")] int depth = 0,
        [Description("If true, strip comments and collapse whitespace for additional token savings.")] bool minify = false)
    {
        try
        {
            if (IsVbFile(filePath))
            {
                var vb = new VBFocusedEmitter(filePath);
                var vbResult = vb.EmitCallers(methodName, depth);
                if (!vbResult.Found)
                    return $"' No callers of '{methodName}' found in {Path.GetFileName(filePath)}.";
                var vbOutput = minify ? VBFocusedEmitter.MinifyText(vbResult.Output) : vbResult.Output;
                return BuildHeader(vbResult.OriginalTokensEstimate, Math.Max(1, vbOutput.Length / 4), "FocusCallers", "VB.NET", $"callers={methodName} depth={depth} minify={minify}")
                     + vbResult.Notes + "\n" + vbOutput;
            }

            if (TryGetCached(filePath, $"callers:{methodName}", depth, minify, out var cached))
                return cached;

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitCallers(methodName, depth);

            if (!result.Found)
            {
                LogInvocation("FocusCallers", "C#", $"callers={methodName} NOT FOUND", result.OriginalTokensEstimate, result.OriginalTokensEstimate);
                return $"// No callers of '{methodName}' found in {Path.GetFileName(filePath)}.";
            }

            var output = minify ? FocusedEmitter.MinifyText(result.Output) : result.Output;
            var afterTokens = Math.Max(1, output.Length / 4);
            var fullOutput = BuildHeader(result.OriginalTokensEstimate, afterTokens, "FocusCallers", "C#", $"callers={methodName} depth={depth} minify={minify}")
                 + result.Notes
                 + "\n"
                 + output;
            SetCached(filePath, $"callers:{methodName}", depth, minify, fullOutput, result.OriginalTokensEstimate, afterTokens);
            return fullOutput;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "PROJECT-WIDE version of focus_callers. Scans every .cs file in a project " +
        "directory and returns focused views of ALL methods that call the named method, " +
        "grouped by file. Answers 'what calls X across the whole codebase?' in one call. " +
        "Pass the project root folder or .csproj file path — obj/ and bin/ are excluded " +
        "automatically. Uses name-based matching (same as focus_callers). " +
        "Set depth=1 to include private helper bodies of found callers. " +
        "C# only (.cs files).")]
    public static string TraceCallers(
        [Description("Absolute path to a project folder or .csproj file. All .cs files under it (excluding obj/ and bin/) are scanned.")] string projectPath,
        [Description("The method name to find callers of across the project.")] string methodName,
        [Description("0 = signatures only for callees (default). 1 = include private helper bodies.")] int depth = 0,
        [Description("If true, strip comments and collapse whitespace for additional token savings.")] bool minify = false)
    {
        try
        {
            var traversal = new ProjectTraversal(projectPath);
            var callerFiles = traversal.FindCallerFiles(methodName);

            if (callerFiles.Count == 0)
                return $"// TraceCallers: no callers of '{methodName}' found in {traversal.FileCount} file(s) under {Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}.";

            var sb = new System.Text.StringBuilder();
            int totalBefore = 0, totalAfter = 0;

            foreach (var filePath in callerFiles)
            {
                var emitter = new FocusedEmitter(filePath);
                var result = emitter.EmitCallers(methodName, depth);
                if (!result.Found) continue;

                var output = minify ? FocusedEmitter.MinifyText(result.Output) : result.Output;
                totalBefore += result.OriginalTokensEstimate;
                totalAfter += Math.Max(1, output.Length / 4);

                sb.AppendLine($"// ── {Path.GetFileName(filePath)} ──────────────────────────");
                sb.AppendLine(result.Notes.TrimEnd());
                sb.AppendLine();
                sb.AppendLine(output);
            }

            var header = BuildHeader(totalBefore, totalAfter, "TraceCallers", "C#",
                $"callers={methodName} files={callerFiles.Count}/{traversal.FileCount} depth={depth} minify={minify}");

            return header + $"// {callerFiles.Count} file(s) with callers of '{methodName}' (scanned {traversal.FileCount} files)\n\n" + sb;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Finds all types (classes, structs, records) across a project that implement or " +
        "extend a named interface or base type, then returns a focused type view for each. " +
        "Answers 'what implements IFoo?' or 'what extends BaseBar?' in one call. " +
        "Pass the project root folder or .csproj file path — obj/ and bin/ are excluded. " +
        "Uses name-based base-list matching (same approach as focus_callers). " +
        "C# only (.cs files).")]
    public static string TraceImplementors(
        [Description("Absolute path to a project folder or .csproj file. All .cs files under it (excluding obj/ and bin/) are scanned.")] string projectPath,
        [Description("The interface or base type name to find implementors/subclasses of (simple name, e.g. 'ILanguageEmitter').")] string interfaceName,
        [Description("If true, strip comments and collapse whitespace for additional token savings.")] bool minify = false)
    {
        try
        {
            var traversal = new ProjectTraversal(projectPath);
            var implementors = traversal.FindImplementors(interfaceName);

            if (implementors.Count == 0)
                return $"// TraceImplementors: no types implementing '{interfaceName}' found in {traversal.FileCount} file(s) under {Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}.";

            var sb = new System.Text.StringBuilder();
            int totalBefore = 0, totalAfter = 0;

            foreach (var impl in implementors)
            {
                var emitter = new FocusedEmitter(impl.FilePath);
                var result = emitter.EmitType(impl.TypeName);
                if (!result.Found) continue;

                var output = minify ? FocusedEmitter.MinifyText(result.Output) : result.Output;
                totalBefore += result.OriginalTokensEstimate;
                totalAfter += Math.Max(1, output.Length / 4);

                sb.AppendLine($"// ── {impl.TypeName} ({Path.GetFileName(impl.FilePath)}) ──────────────────────────");
                sb.AppendLine(result.Notes.TrimEnd());
                sb.AppendLine();
                sb.AppendLine(output);
            }

            var header = BuildHeader(totalBefore, totalAfter, "TraceImplementors", "C#",
                $"implementors={interfaceName} found={implementors.Count} files={traversal.FileCount} minify={minify}");

            return header + $"// {implementors.Count} implementor(s) of '{interfaceName}' found (scanned {traversal.FileCount} files)\n\n" + sb;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static bool TryGetCached(string filePath, string key, int depth, bool minify, out string output)
    {
        if (!EmissionCache.TryGet(filePath, key, depth, minify, out output, out var before, out var after))
            return false;
        LogInvocation("Cache", "C#", $"{key} depth={depth} minify={minify} [re-parse skipped]", before, after);
        return true;
    }

    private static void SetCached(string filePath, string key, int depth, bool minify, string output, int beforeTokens, int afterTokens) =>
        EmissionCache.Set(filePath, key, depth, minify, output, beforeTokens, afterTokens);
    private static bool IsVbFile(string filePath) =>
        Path.GetExtension(filePath).Equals(".vb", StringComparison.OrdinalIgnoreCase);

    private static string BuildHeader(int before, int after, string toolName, string language, string mode)
    {
        after = Math.Min(after, before); // never log that the tool increased token count
        var saved = Math.Max(0, before - after);
        var pct = before == 0 ? 0 : saved * 100 / before;
        LogInvocation(toolName, language, mode, before, after);
        return $"// [Focused Emitter] Tokens without tool: {before:N0}  →  with tool: {after:N0}  ({pct}% saved) — mode: {mode}\n";
    }

    // Every invocation is appended to the shared report JSON at
    // %USERPROFILE%\.tokensaver\report.json so the Blazor viewer (and any
    // future surface) sees CLI and MCP traffic in one place.
    private static void LogInvocation(string toolName, string language, string mode, int beforeTokens, int afterTokens)
    {
        var saved = beforeTokens - afterTokens;
        var pct = beforeTokens == 0 ? 0 : saved * 100 / beforeTokens;

        // Stderr so VS Output → GitHub Copilot shows it live.
        Console.Error.WriteLine(
            $"[tokensaver] {DateTime.Now:yyyy-MM-dd HH:mm:ss}  {mode}  " +
            $"before={beforeTokens} after={afterTokens} saved={saved} ({pct}%)");

        try
        {
            TokenSaver.ReportWriter.Append(
                toolName: toolName,
                language: language,
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
}
