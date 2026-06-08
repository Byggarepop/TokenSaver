// Three MCP tools wrapping the RoslynLean emitter.
// Tool descriptions matter: they're how the host's model picks the right one.
// Each result starts with a one-line token-comparison header so the AI can
// surface "I used the focused emitter, saved ~X tokens" to the user.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;
using RoslynLean;

namespace TokenSaver.Mcp;

[McpServerToolType]
public static class FocusedEmitterTools
{
    public static int OverheadTokens
    {
        get => _overheadTokens;
        set
        {
            _overheadTokens = value;
            // A new overhead value marks a fresh server session — reset running totals.
            Interlocked.Exchange(ref _callCount, 0);
            Interlocked.Exchange(ref _sessionBefore, 0);
            Interlocked.Exchange(ref _sessionAfter, 0);
            _sessionSourcesCounted.Clear();
        }
    }
    private static int _overheadTokens;
    private static int _callCount;
    private static long _sessionBefore;
    private static long _sessionAfter;
    // Sources (files, or a synthetic key for project-wide traces) whose whole-file
    // baseline has already been added to _sessionBefore this session. Reading a
    // source costs its tokens once; a second distinct view of it does not save the
    // whole file again, so we count `before` only on first sighting. OrdinalIgnoreCase
    // because Windows paths are case-insensitive.
    private static readonly ConcurrentDictionary<string, byte> _sessionSourcesCounted =
        new(StringComparer.OrdinalIgnoreCase);

    public static int ComputeOverheadTokens(string serverInstructions)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(serverInstructions);
        var flags = BindingFlags.Public | BindingFlags.Static;
        foreach (var type in typeof(FocusedEmitterTools).Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null) continue;
            foreach (var method in type.GetMethods(flags))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is null) continue;
                sb.Append(method.Name);
                if (method.GetCustomAttribute<DescriptionAttribute>() is { Description: var md })
                    sb.Append(md);
                foreach (var param in method.GetParameters())
                {
                    sb.Append(param.Name);
                    if (param.GetCustomAttribute<DescriptionAttribute>() is { Description: var pd })
                        sb.Append(pd);
                }
            }
        }
        return TokenCounter.Count(sb.ToString());
    }
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
        "If methodName contains a comma (e.g. 'Foo,Bar'), the call is routed to " +
        "focus_multiple_methods automatically — but prefer calling that tool " +
        "directly when you already know you want several methods. " +
        "Supports .cs, .razor.cs, .razor, and .vb files.")]
    public static string FocusMethod(
        [Description("Absolute path to a .cs, .razor.cs, .razor, or .vb file. For .razor, only the @code / @functions blocks are analyzed.")] string filePath,
        [Description("The method name to focus on. Overloads are all included.")] string methodName,
        [Description("0 = signatures only for callees (default). 1 = include private helper bodies. 2+ = recursive.")] int depth = 0,
        [Description("If true, strip comments and collapse whitespace for additional token savings.")] bool minify = false)
    {
        try
        {
            // A comma in methodName means the caller wanted several methods. Route to
            // the multi-method tool instead of treating "A,B" as one (missing) name
            // and dumping the entire outline as an expensive "not found" response.
            if (methodName.Contains(','))
                return FocusMultipleMethods(filePath, methodName, depth, minify);

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
                return BuildHeader(TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(vbOutput), "Focused Emitter", "VB.NET", $"focus={methodName} depth={depth} minify={minify}", RelevantBaseline(vbResult), sessionKey: filePath)
                     + vbResult.Notes + "\n" + vbOutput;
            }

            if (TryGetCached(filePath, methodName, depth, minify, "Focused Emitter", out var cached))
                return cached;

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.Emit(methodName, depth);

            if (!result.Found)
            {
                var outline = emitter.EmitOutline();
                var hint = result.NotFoundHint is { } h ? h + "\n" : "";
                var response = $"ERROR: Method '{methodName}' not found in {Path.GetFileName(filePath)}.\n" +
                       hint +
                       $"Available members:\n{outline.Output}";
                // A miss returns a small outline + hint, not the whole file — so log it as
                // whole-file -> response, the real saving versus the model reading the file
                // to discover the member isn't here, not whole -> whole (a bogus 0%).
                LogInvocation("Focused Emitter", "C#", $"focus={methodName} depth={depth} NOT FOUND",
                    TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(response));
                return response;
            }

            var output = result.Output;
            if (minify)
                output = FocusedEmitter.MinifyText(output);

            var beforeTokens = TokenCounter.Count(File.ReadAllText(filePath));
            var afterTokens = TokenCounter.Count(output);
            var relevantBaseline = RelevantBaseline(result);
            var fullOutput = BuildHeader(beforeTokens, afterTokens, "Focused Emitter", "C#", $"focus={methodName} depth={depth} minify={minify}", relevantBaseline, sessionKey: filePath)
                 + result.Notes
                 + "\n"
                 + output;
            // Cache the conservative telemetry baseline, not the raw whole-file count, so a
            // cache-hit re-serve logs the same "without" figure as this first call instead of
            // re-crediting the whole-file saving the session ledger never re-credits.
            SetCached(filePath, methodName, depth, minify, fullOutput, TelemetryBaseline(beforeTokens, relevantBaseline), afterTokens);
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
                return BuildHeader(TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(vbOutput), "Focused Emitter (multi)", "VB.NET", $"focus=[{string.Join(",", names)}] depth={depth} minify={minify}", RelevantBaseline(vbResult), sessionKey: filePath)
                     + vbResult.Notes + "\n" + vbOutput;
            }

            var multiKey = string.Join("|", names.OrderBy(n => n));
            if (TryGetCached(filePath, multiKey, depth, minify, "Focused Emitter (multi)", out var cached))
                return cached;

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitMultiple(names, depth);

            if (!result.Found)
            {
                var outline = emitter.EmitOutline();
                var hint = result.NotFoundHint is { } h ? h + "\n" : "";
                var response = $"ERROR: None of the requested methods found in {Path.GetFileName(filePath)}.\n" +
                       hint +
                       $"Available members:\n{outline.Output}";
                LogInvocation("Focused Emitter (multi)", "C#", $"focus=[{string.Join(",", names)}] depth={depth} NOT FOUND",
                    TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(response));
                return response;
            }

            var output = minify ? FocusedEmitter.MinifyText(result.Output) : result.Output;
            var beforeTokens = TokenCounter.Count(File.ReadAllText(filePath));
            var afterTokens = TokenCounter.Count(output);
            var relevantBaseline = RelevantBaseline(result);
            var fullOutput = BuildHeader(beforeTokens, afterTokens, "Focused Emitter (multi)", "C#", $"focus=[{string.Join(",", names)}] depth={depth} minify={minify}", relevantBaseline, sessionKey: filePath)
                 + result.Notes
                 + "\n"
                 + output;
            // Cache the conservative telemetry baseline, not the raw whole-file count, so a
            // cache-hit re-serve logs the same "without" figure as this first call instead of
            // re-crediting the whole-file saving the session ledger never re-credits.
            SetCached(filePath, multiKey, depth, minify, fullOutput, TelemetryBaseline(beforeTokens, relevantBaseline), afterTokens);
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
            var originalText = File.ReadAllText(filePath);
            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitMinified();

            var bodyCs = result.FocusedChars >= result.OriginalChars
                ? originalText
                : result.Output;

            return BuildHeader(TokenCounter.Count(originalText), TokenCounter.Count(bodyCs), "MinifyCSharpFile", "C#", "whole-file lossless minify", sessionKey: filePath)
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
        "X++ (.xpp — C-style comment strip + whitespace collapse), " +
        "VB.NET (.vb — Roslyn comment strip + blank-run collapse), " +
        "and Markdown (.md, .markdown — HTML comments stripped, blank-run collapse). " +
        "Code minifiers strip comments and collapse whitespace. " +
        "Indent-sensitive formats (Python, YAML, Markdown) preserve leading indentation. " +
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

            var originalText = File.ReadAllText(filePath);
            var result = emitter.Minify(filePath);

            var body = result.OutputChars >= result.OriginalChars
                ? originalText
                : result.Output;

            return BuildHeader(TokenCounter.Count(originalText), TokenCounter.Count(body), "MinifyFile", emitter.Language, $"{emitter.Language} minify", sessionKey: filePath)
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
                return BuildHeader(TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(vbResult.Output), "OutlineCSharpFile", "VB.NET", "outline (signatures only)", sessionKey: filePath)
                     + vbResult.Notes + "\n" + vbResult.Output;
            }

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitOutline();
            return BuildHeader(TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(result.Output), "OutlineCSharpFile", "C#", "outline (signatures only)", sessionKey: filePath)
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

            return BuildHeader(TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(result.Output), "AliasCSharpFile", "C#", "aliased + minified", sessionKey: filePath)
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
                return BuildHeader(TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(vbOutput), "FocusType", "VB.NET", $"type={typeName} minify={minify}", RelevantBaseline(vbResult), sessionKey: filePath)
                     + vbResult.Notes + "\n" + vbOutput;
            }

            if (TryGetCached(filePath, $"type:{typeName}", depth: 0, minify, "FocusType", out var cached))
                return cached;

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitType(typeName);

            if (!result.Found)
            {
                var outline = emitter.EmitOutline();
                var response = $"ERROR: Type '{typeName}' not found in {Path.GetFileName(filePath)}.\n" +
                       $"Available types:\n{outline.Output}";
                LogInvocation("FocusType", "C#", $"type={typeName} NOT FOUND",
                    TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(response));
                return response;
            }

            var output = minify ? FocusedEmitter.MinifyText(result.Output) : result.Output;
            var beforeTokens = TokenCounter.Count(File.ReadAllText(filePath));
            var afterTokens = TokenCounter.Count(output);
            var relevantBaseline = RelevantBaseline(result);
            var fullOutput = BuildHeader(beforeTokens, afterTokens, "FocusType", "C#", $"type={typeName} minify={minify}", relevantBaseline, sessionKey: filePath)
                 + result.Notes
                 + "\n"
                 + output;
            // Cache the conservative telemetry baseline, not the raw whole-file count, so a
            // cache-hit re-serve logs the same "without" figure as this first call instead of
            // re-crediting the whole-file saving the session ledger never re-credits.
            SetCached(filePath, $"type:{typeName}", depth: 0, minify, fullOutput, TelemetryBaseline(beforeTokens, relevantBaseline), afterTokens);
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
        "USE FOR DISCOVERY ONLY — call this when you do not yet know which methods call the target. " +
        "Once you know the caller names, use focus_multiple_methods instead (cheaper). " +
        "Do NOT call this if the callers are already in context. " +
        "Avoid when callers are large methods — the tool emits their full bodies, so output can " +
        "approach the whole file size (0% savings). In that case prefer focus_multiple_methods or " +
        "a narrow Read of the relevant lines. " +
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
                return BuildHeader(TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(vbOutput), "FocusCallers", "VB.NET", $"callers={methodName} depth={depth} minify={minify}", RelevantBaseline(vbResult), sessionKey: filePath)
                     + vbResult.Notes + "\n" + vbOutput;
            }

            if (TryGetCached(filePath, $"callers:{methodName}", depth, minify, "FocusCallers", out var cached))
                return cached;

            var emitter = new FocusedEmitter(filePath);
            var result = emitter.EmitCallers(methodName, depth);

            if (!result.Found)
            {
                var response = $"// No callers of '{methodName}' found in {Path.GetFileName(filePath)}.";
                LogInvocation("FocusCallers", "C#", $"callers={methodName} NOT FOUND",
                    TokenCounter.Count(File.ReadAllText(filePath)), TokenCounter.Count(response));
                return response;
            }

            var output = minify ? FocusedEmitter.MinifyText(result.Output) : result.Output;
            var beforeTokens = TokenCounter.Count(File.ReadAllText(filePath));
            var afterTokens = TokenCounter.Count(output);
            var relevantBaseline = RelevantBaseline(result);
            var fullOutput = BuildHeader(beforeTokens, afterTokens, "FocusCallers", "C#", $"callers={methodName} depth={depth} minify={minify}", relevantBaseline, sessionKey: filePath)
                 + result.Notes
                 + "\n"
                 + output;
            // Cache the conservative telemetry baseline, not the raw whole-file count, so a
            // cache-hit re-serve logs the same "without" figure as this first call instead of
            // re-crediting the whole-file saving the session ledger never re-credits.
            SetCached(filePath, $"callers:{methodName}", depth, minify, fullOutput, TelemetryBaseline(beforeTokens, relevantBaseline), afterTokens);
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
        "C# only (.cs files). " +
        "CAUTION: for existence-only questions ('is X used?', 'is X called anywhere?') " +
        "use Grep instead — a method with many callers can cost 100K+ tokens here.")]
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
                totalBefore += TokenCounter.Count(File.ReadAllText(filePath));
                totalAfter += TokenCounter.Count(output);

                sb.AppendLine($"// ── {Path.GetFileName(filePath)} ──────────────────────────");
                sb.AppendLine(result.Notes.TrimEnd());
                sb.AppendLine();
                sb.AppendLine(output);
            }

            var header = BuildHeader(totalBefore, totalAfter, "TraceCallers", "C#",
                $"callers={methodName} files={callerFiles.Count}/{traversal.FileCount} depth={depth} minify={minify}",
                sessionKey: $"TraceCallers:{methodName}");

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
                totalBefore += TokenCounter.Count(File.ReadAllText(impl.FilePath));
                totalAfter += TokenCounter.Count(output);

                sb.AppendLine($"// ── {impl.TypeName} ({Path.GetFileName(impl.FilePath)}) ──────────────────────────");
                sb.AppendLine(result.Notes.TrimEnd());
                sb.AppendLine();
                sb.AppendLine(output);
            }

            var header = BuildHeader(totalBefore, totalAfter, "TraceImplementors", "C#",
                $"implementors={interfaceName} found={implementors.Count} files={traversal.FileCount} minify={minify}",
                sessionKey: $"TraceImplementors:{interfaceName}");

            return header + $"// {implementors.Count} implementor(s) of '{interfaceName}' found (scanned {traversal.FileCount} files)\n\n" + sb;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Finds every Dependency-Injection registration across a project that references a " +
        "named type (interface OR concrete), answering 'where is IFoo wired, and to what " +
        "implementation?' — the question a constructor caller-trace CANNOT answer because " +
        "DI-constructed types are never created with 'new'. Returns a compact table: " +
        "file:line, registration method, ServiceType -> ImplType, lifetime, and keyed key. " +
        "Detects Add/TryAdd{Scoped,Singleton,Transient}, AddKeyed*, in generic, typeof(), " +
        "and factory-lambda forms. Pass the project root or .csproj path; obj/ and bin/ are " +
        "excluded. Syntactic name matching (same approach as trace_implementors). C# only.")]
    public static string TraceDiRegistrations(
        [Description("Absolute path to a project folder or .csproj file. All .cs files under it (excluding obj/ and bin/) are scanned.")] string projectPath,
        [Description("The service or implementation type name to find registrations for (simple name, e.g. 'IFoo').")] string typeName)
    {
        try
        {
            var traversal = new ProjectTraversal(projectPath);
            var registrations = traversal.FindDiRegistrations(typeName);

            if (registrations.Count == 0)
                return $"// TraceDiRegistrations: no DI registrations referencing '{typeName}' found in {traversal.FileCount} file(s) under {Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}.";

            // Column widths for an aligned, scannable table.
            var locations = registrations
                .Select(r => $"{Path.GetFileName(r.FilePath)}:{r.Line}")
                .ToList();
            int locWidth = locations.Max(l => l.Length);
            int methodWidth = registrations.Max(r => r.Method.Length);

            var sb = new System.Text.StringBuilder();
            var lineCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            int totalBefore = 0;

            for (int i = 0; i < registrations.Count; i++)
            {
                var r = registrations[i];
                var arrow = $"{r.ServiceType} -> {r.ImplType}";
                var keyPart = r.Key is null ? "" : $"   key=\"{r.Key}\"";
                sb.AppendLine($"{locations[i].PadRight(locWidth)}  {r.Method.PadRight(methodWidth)}  {arrow}{keyPart}");

                // Baseline = the raw source line a grep-and-read user would have to read instead.
                if (!lineCache.TryGetValue(r.FilePath, out var fileLines))
                    lineCache[r.FilePath] = fileLines = File.ReadAllLines(r.FilePath);
                if (r.Line >= 1 && r.Line <= fileLines.Length)
                    totalBefore += TokenCounter.Count(fileLines[r.Line - 1]);
            }

            var table = sb.ToString();
            var header = BuildHeader(totalBefore, TokenCounter.Count(table), "TraceDiRegistrations", "C#",
                $"type={typeName} found={registrations.Count} files={traversal.FileCount}",
                sessionKey: $"TraceDiRegistrations:{typeName}");

            return header + $"// {registrations.Count} registration(s) referencing '{typeName}' (scanned {traversal.FileCount} files)\n\n" + table;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Maps every type (class/struct/record/interface/enum) in a project to its file:line, " +
        "kind, and base list — a compact index for locating types when you don't know which " +
        "file they're in. Prefer over Grep for type discovery, then drill in with focus_method/" +
        "focus_type. Pass nameFilter (case-insensitive substring) to narrow on large repos. " +
        "Project root or .csproj path; obj/ and bin/ excluded. C# only.")]
    public static string MapProject(
        [Description("Absolute path to a project folder or .csproj file. All .cs files under it (excluding obj/ and bin/) are scanned.")] string projectPath,
        [Description("Optional case-insensitive substring; only types whose name contains it are returned. Omit to list all types.")] string? nameFilter = null)
    {
        try
        {
            var traversal = new ProjectTraversal(projectPath);
            var types = traversal.MapTypes(nameFilter);

            var filterNote = string.IsNullOrWhiteSpace(nameFilter) ? "none" : $"\"{nameFilter}\"";
            if (types.Count == 0)
                return $"// MapProject: no types{(string.IsNullOrWhiteSpace(nameFilter) ? "" : $" matching {filterNote}")} found in {traversal.FileCount} file(s) under {Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}.";

            var ordered = types
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Render columns: Name | (modifiers + kind) | relpath:line | : bases.
            var rows = ordered.Select(t =>
            {
                var kind = string.IsNullOrEmpty(t.Modifiers) ? t.Kind : $"{t.Modifiers} {t.Kind}";
                var loc = $"{Path.GetRelativePath(traversal.Root, t.FilePath).Replace('\\', '/')}:{t.Line}";
                return (t.Name, Kind: kind, Loc: loc, Bases: t.Bases);
            }).ToList();

            int nameWidth = rows.Max(r => r.Name.Length);
            int kindWidth = rows.Max(r => r.Kind.Length);
            int locWidth = rows.Max(r => r.Loc.Length);

            var sb = new System.Text.StringBuilder();
            int before = 0;
            foreach (var r in rows)
            {
                var basePart = r.Bases is null ? "" : $"   : {r.Bases}";
                sb.AppendLine($"{r.Name.PadRight(nameWidth)}  {r.Kind.PadRight(kindWidth)}  {r.Loc.PadRight(locWidth)}{basePart}".TrimEnd());
                // Baseline = the bare declaration a reader would otherwise scan to find each type.
                before += TokenCounter.Count($"{r.Kind} {r.Name}{(r.Bases is null ? "" : " : " + r.Bases)}");
            }

            var table = sb.ToString();
            var header = BuildHeader(before, TokenCounter.Count(table), "MapProject", "C#",
                $"types={types.Count} files={traversal.FileCount} filter={filterNote}",
                sessionKey: $"MapProject:{traversal.Root}:{nameFilter}");

            return header + $"// {types.Count} type(s) across {traversal.FileCount} file(s)  (filter: {filterNote})\n\n" + table;
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static bool TryGetCached(string filePath, string key, int depth, bool minify, string toolName, out string output)
    {
        if (!EmissionCache.TryGet(filePath, key, depth, minify, out output, out var before, out var after))
            return false;
        // Tag the cache hit with the tool that produced the cached entry (e.g.
        // "Focused Emitter (multi) Cache") so the dashboard shows what was re-served.
        LogInvocation($"{toolName} Cache", "C#", $"{key} depth={depth} minify={minify} [re-parse skipped]", before, after);
        return true;
    }

    private static void SetCached(string filePath, string key, int depth, bool minify, string output, int beforeTokens, int afterTokens) =>
        EmissionCache.Set(filePath, key, depth, minify, output, beforeTokens, afterTokens);
    private static bool IsVbFile(string filePath) =>
        Path.GetExtension(filePath).Equals(".vb", StringComparison.OrdinalIgnoreCase);

    // Token count of the raw "relevant code" a targeted reader would need (the focus
    // method plus expanded helpers), or null when the emit has no such subset.
    private static int? RelevantBaseline(RoslynLean.FocusResult result) =>
        result.RelevantSourceText is { Length: > 0 } rel ? TokenCounter.Count(rel) : null;

    // The baseline we record locally and upload to the dashboard. We deliberately use
    // the conservative (lower-bound) figure so the public numbers never overstate
    // savings: the relevant-code count when the tool has one (the focused tools), else
    // the whole file (whole-file tools, where reading all of it is the real alternative).
    public static int TelemetryBaseline(int wholeFileTokens, int? relevantBaseline) =>
        relevantBaseline is { } r && r > 0 ? r : wholeFileTokens;

    private static string BuildHeader(int before, int after, string toolName, string language, string mode, int? relevantBaseline = null, string? sessionKey = null)
    {
        // Telemetry/dashboard records the conservative baseline (see TelemetryBaseline)
        // so we never exaggerate savings. Counts are raw — unclamped and with no
        // overhead folded in — so the stored data reflects what the tokenizer saw.
        LogInvocation(toolName, language, mode, TelemetryBaseline(before, relevantBaseline), after);

        // Per-call display: clamp so we never present the tool as increasing token
        // count, and keep it overhead-free. The overhead is a per-session cost, not
        // this call's cost, so attributing it here would be misleading.
        var displayAfter = Math.Min(after, before);
        var saved = before - displayAfter;
        var pct = before == 0 ? 0 : saved * 100 / before;

        // Per-session dedupe of the whole-file baseline. Reading a file costs its
        // whole-file token count ONCE; a second, distinct view of the same source (a
        // different method, or outline-then-minify) does not save the whole file
        // again — only its own output adds to context. So `before` is added to the
        // session total the first time we see a source and never again. Identical
        // repeat calls never reach here — they are served from EmissionCache without
        // touching the ledger. A null key (shouldn't happen) is treated as first-view.
        var firstView = sessionKey is null || _sessionSourcesCounted.TryAdd(sessionKey, 0);

        // Session running total: the MCP overhead (server instructions + tool schemas)
        // is a single per-session cost, so it is subtracted exactly once against the
        // cumulative savings — never once per call. Early on, net may be negative,
        // which honestly signals the server hasn't paid for its context cost yet.
        var calls = Interlocked.Increment(ref _callCount);
        var sessionBefore = Interlocked.Add(ref _sessionBefore, firstView ? before : 0);
        var sessionAfter = Interlocked.Add(ref _sessionAfter, displayAfter);
        var sessionSaved = sessionBefore - sessionAfter;
        var sessionNet = sessionSaved - OverheadTokens;

        // On a repeat view we deliberately do NOT print the "% saved" headline: the
        // whole-file saving was already credited on the first view, and repeating the
        // headline is exactly what inflates a summed ~50% into an apparent ~90%. We
        // state plainly that this view only adds its own output to the context.
        var callLine = firstView
            ? $"// [Focused Emitter] Tokens without tool: {before:N0}  →  with tool: {displayAfter:N0}  ({pct}% saved) — mode: {mode}\n"
            : $"// [Focused Emitter] repeat view of this file this session — whole-file baseline already counted; this view adds {displayAfter:N0} tokens, no new whole-file saving — mode: {mode}\n";

        // Lower-bound baseline for the focused tools: the whole-file "without tool"
        // figure assumes the alternative was reading the entire file, which is a best
        // case. A careful reader could instead read just the relevant code. This line
        // compares against exactly that — so the true saving sits between this and the
        // whole-file number. The tool output can be smaller than the relevant code
        // (minified) or larger (it adds related signatures); both are reported plainly.
        var targetedLine = "";
        if (relevantBaseline is { } relevant && relevant > 0)
        {
            var diff = relevant - displayAfter;
            var relevantPct = Math.Abs(diff) * 100 / relevant;
            var verb = diff >= 0 ? "saved" : "larger";
            targetedLine = $"// vs a targeted read of just the relevant code ({relevant:N0} tokens): {relevantPct}% {verb}\n";
        }

        var sessionLine = $"// session: {calls} call{(calls == 1 ? "" : "s")} · raw saved {sessionSaved:N0} · net of {OverheadTokens:N0} one-time MCP overhead = {sessionNet:N0}\n";
        return callLine + targetedLine + sessionLine;
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
