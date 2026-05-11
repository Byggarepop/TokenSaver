using System.Text.Json;
using System.Text.Json.Nodes;
using RoslynLean;

if (args.Length >= 1 && args[0] == "install-hooks")
{
    return InstallHooks(args);
}

if (args.Length < 2 || args[0] != "focus")
{
    PrintUsage();
    return 1;
}

var csharpFilePath = args.Skip(1)
    .FirstOrDefault(a => a.StartsWith("--csharpfile="))
    ?.Substring("--csharpfile=".Length);

var methodName = args.Skip(1)
    .FirstOrDefault(a => a.StartsWith("--method="))
    ?.Substring("--method=".Length);

var showStats = args.Contains("--stats");
var writeReport = args.Contains("--report");
var aliasMode = args.Contains("--alias");
var minifyFlag = args.Contains("--minify");
var depthArg = args.Skip(1)
    .FirstOrDefault(a => a.StartsWith("--depth="))
    ?.Substring("--depth=".Length);
var depth = int.TryParse(depthArg, out var d) ? Math.Max(0, d) : 0;

// Positional path is whatever non-flag arg sits after "focus"
var positionalPath = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));

string sourcePath;
bool minifyMode;

if (!string.IsNullOrEmpty(csharpFilePath))
{
    sourcePath = csharpFilePath;
    minifyMode = true;
}
else if (!string.IsNullOrEmpty(methodName) && !string.IsNullOrEmpty(positionalPath))
{
    sourcePath = positionalPath;
    minifyMode = false;
}
else
{
    PrintUsage();
    return 1;
}

try
{
    var emitter = new FocusedEmitter(sourcePath);
    var result = minifyMode
        ? (aliasMode ? emitter.EmitAliased() : emitter.EmitMinified())
        : emitter.Emit(methodName!, depth);

    if (!result.Found)
    {
        Console.Error.WriteLine(result.Output);
        return 2;
    }

    // --minify post-processor for --method mode (no-op for --csharpfile, already minified)
    if (minifyFlag && !minifyMode)
    {
        var minified = FocusedEmitter.MinifyText(result.Output);
        result = result with { Output = minified, FocusedChars = minified.Length };
    }

    Console.Write(result.Notes);
    Console.WriteLine();
    Console.Write(result.Output);

    if (showStats)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Original:  {result.OriginalChars,6} chars  (~{result.OriginalTokensEstimate} tokens)");
        Console.Error.WriteLine($"Focused:   {result.FocusedChars,6} chars  (~{result.FocusedTokensEstimate} tokens)");
        Console.Error.WriteLine($"Reduction: {result.ReductionPercent:F1}%");
    }

    if (writeReport)
    {
        var toolName = minifyMode
            ? (aliasMode ? "Alias" : "Minify")
            : "Focused Emitter";
        var notes = minifyMode
            ? Path.GetFileName(sourcePath)
            : $"{Path.GetFileName(sourcePath)} / {methodName} (depth={depth})";
        TokenSaver.ReportWriter.Append(
            toolName: toolName,
            tokensWithoutTool: result.OriginalTokensEstimate,
            tokensWithTool: result.FocusedTokensEstimate,
            notes: notes,
            source: "cli");
        Console.Error.WriteLine($"Appended report entry to {TokenSaver.ReportWriter.DefaultPath}");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 3;
}

static int InstallHooks(string[] args)
{
    var targetArg = args.Skip(1)
        .FirstOrDefault(a => a.StartsWith("--target="))
        ?.Substring("--target=".Length);
    var target = string.IsNullOrEmpty(targetArg)
        ? Directory.GetCurrentDirectory()
        : Path.GetFullPath(targetArg);

    if (!Directory.Exists(target))
    {
        Console.Error.WriteLine($"Target directory does not exist: {target}");
        return 1;
    }

    var claudeDir = Path.Combine(target, ".claude");
    var hooksDir = Path.Combine(claudeDir, "hooks");
    Directory.CreateDirectory(hooksDir);

    const string remindHookScript = """
$ErrorActionPreference = 'SilentlyContinue'

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    exit 0
}

if ($payload.tool_name -ne 'Read') { exit 0 }

$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }
if ($path -notmatch '\.(cs|razor\.cs)$') { exit 0 }

if (Test-Path -LiteralPath $path) {
    $lineCount = (Get-Content -LiteralPath $path -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
    if ($lineCount -lt 50) { exit 0 }
}

$reminder = @"
You are about to Read a C# file ($path) with the built-in Read tool.

Prefer the roslyn-lean MCP server (registered for this project):
  - minify_c_sharp_file : lossless ~20-50% reduction for whole-file reads
  - focus_method        : when you need a specific method (use depth=1)
  - alias_c_sharp_file  : files dominated by long private symbol names

ONLY use Read directly when you need exact on-disk text for an Edit call,
or when the user explicitly asked for the raw file. Otherwise, cancel
this Read and call the appropriate MCP tool instead.
"@

$out = @{
    hookSpecificOutput = @{
        hookEventName     = 'PreToolUse'
        additionalContext = $reminder
    }
} | ConvertTo-Json -Depth 5 -Compress

Write-Output $out
exit 0
""";

    var scriptPath = Path.Combine(hooksDir, "remind-csharp-mcp.ps1");
    File.WriteAllText(scriptPath, remindHookScript);
    Console.Error.WriteLine($"Wrote {scriptPath}");

    var settingsPath = Path.Combine(claudeDir, "settings.json");
    JsonObject root;
    if (File.Exists(settingsPath))
    {
        try
        {
            root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            File.Move(settingsPath, settingsPath + ".bak", overwrite: true);
            Console.Error.WriteLine($"Existing settings.json was unparseable; backed up to {settingsPath}.bak");
            root = new JsonObject();
        }
    }
    else
    {
        root = new JsonObject();
    }

    if (root["hooks"] is not JsonObject hooks)
    {
        hooks = new JsonObject();
        root["hooks"] = hooks;
    }

    if (hooks["PreToolUse"] is not JsonArray preToolUse)
    {
        preToolUse = new JsonArray();
        hooks["PreToolUse"] = preToolUse;
    }

    const string command =
        "powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/remind-csharp-mcp.ps1";

    var alreadyInstalled = preToolUse
        .OfType<JsonObject>()
        .Any(entry =>
            entry["matcher"]?.GetValue<string>() == "Read" &&
            entry["hooks"] is JsonArray inner &&
            inner.OfType<JsonObject>().Any(h => h["command"]?.GetValue<string>() == command));

    if (alreadyInstalled)
    {
        Console.Error.WriteLine($"Hook already present in {settingsPath} — no change");
    }
    else
    {
        preToolUse.Add(new JsonObject
        {
            ["matcher"] = "Read",
            ["hooks"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = command,
                },
            },
        });

        File.WriteAllText(
            settingsPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.Error.WriteLine($"Updated {settingsPath}");
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine("Done. Restart Claude Code in this directory for the hook to take effect.");
    return 0;
}

static void PrintUsage() => Console.Error.WriteLine(
    """
    roslyn-lean — emit a token-reduced view of a C# file for LLM consumption.

    USAGE:
      roslyn-lean focus <path-to-file.cs> --method=<MethodName> [--stats]
      roslyn-lean focus --csharpfile=<path-to-file.cs> [--stats]
      roslyn-lean install-hooks [--target=<dir>]

    install-hooks     Writes a Claude Code PreToolUse hook into <dir>/.claude/
                      (defaults to the current directory). The hook reminds the
                      AI to use the roslyn-lean MCP tools instead of the raw
                      Read tool when opening .cs files >= 50 lines. Idempotent
                      and merges with an existing settings.json. Windows /
                      PowerShell only.

    --method=<Name>   Focused-method mode: emits the named method with full body,
                      every other member of its type reduced to a signature.
                      Best when you know which method the AI should reason about.

    --csharpfile=<P>  Lossless minify mode: strips comments, XML docs, and extra
                      whitespace from the whole file. Logic is preserved verbatim
                      (Roslyn parses and re-emits the syntax tree).
                      Best when the AI needs the full file but you want fewer tokens.

    --minify          (with --method) Strip comments and collapse whitespace
                      from the focused output. Lossless, same transform as
                      --csharpfile uses by default. No-op with --csharpfile.

    --depth=<N>       (with --method) Also include the FULL BODIES of private
                      helper methods called from the focus method, up to N
                      transitive levels. Default 0 (signatures only).
                      Use 1 for "translate this method" / refactor tasks where
                      the AI needs to see what helpers actually do, not guess.

    --alias           (with --csharpfile) Also rename PRIVATE methods/properties/
                      fields/events to short codes (M1, P1, F1, E1...). A symbol
                      ledger is prepended so the LLM can map back. Public API is
                      left alone — we can't see callers from a single-file view.

    --stats           Print before/after token estimate to stderr.

    --report          Append this run's before/after stats as a JSON entry to
                      %USERPROFILE%\token-saver-report.json. The TokenSaverViewer
                      Blazor app reads that file.

    OUTPUT:
      The transformed source goes to stdout. Stats (if --stats) go to stderr.
    """);

