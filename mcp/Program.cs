// MCP server entry point. Speaks JSON-RPC over stdio — Visual Studio (and any
// other MCP host) launches this exe and communicates via stdin/stdout.
//
// CRITICAL: Nothing else may write to stdout. Logs go to stderr. The host
// parses every stdout byte as protocol traffic.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string ServerInstructions = """
This server (tokensaver) gives a model a cheap STRUCTURAL WARM START in an
unfamiliar codebase, then gets out of the way. It exposes three tools. Prefer
them for the cases below; for everything else your own Grep + a narrow Read
(offset+limit) is the leanest path — do not reach for a tool when a targeted
Read will do.

TOOL SELECTION RULES — follow by default, no need to ask the user:

1. Orient in a C#/VB file ("what's in this file?", "where do I add X?", or
   before editing any file >=50 lines) -> OutlineCSharpFile. Returns every type
   and member as a signature, NO bodies (typical 70-95% smaller), each tagged
   with its source line range (// L31-44). To then read a body, Read that exact
   range (offset+limit) — do NOT re-read the whole file. C#/VB only.

2. Read or compress a whole file of a supported type -> MinifyFile.
   Auto-dispatches by extension; strips comments/whitespace losslessly. Use it
   for non-C# files, or when you genuinely need a whole C# file rather than its
   skeleton. For C#, prefer OutlineCSharpFile — it saves far more.

3. Where is a type wired in DI, and to what / what lifetime? ->
   TraceDiRegistrations with the project directory or .csproj path and the type
   name (interface OR concrete). Returns a compact table: file:line, method,
   ServiceType -> ImplType, lifetime, keyed key. C# only — this is the one thing
   Grep cannot answer cleanly.

AFTER any tool call on a file, Read ONLY the lines you need to change
(offset+limit, ~5 lines around the match) — never the whole file. Do NOT call
any tool on a file under 50 lines; use Read. SKIP these tools for unsupported
types (.txt, .sql, binary) or when the user asks for the raw file.

SUPPORTED TYPES (MinifyFile, by extension): C#/Razor, JavaScript, TypeScript,
Python, HTML, CSS/SCSS/LESS, JSON/JSONC, YAML, XML/.NET project files (.csproj,
.props, .targets, .config, .resx), C/C++, X++, VB.NET. OutlineCSharpFile: .cs,
.razor.cs, .razor, .vb.

OUTPUT IS A SUMMARY VIEW, NOT THE SOURCE OF TRUTH: comments, XML docs and
#regions are stripped; field initializers omitted; whitespace collapsed — the
real file is conventionally formatted. When suggesting code, format it
idiomatically and preserve existing comments.

Each result starts with a token-comparison header: a whole-file baseline (a
best case), a targeted-read baseline when available, and a running session
total net of the one-time overhead. Mention savings in one short sentence;
don't present the whole-file figure as guaranteed.
""";

var startupVersion = System.Reflection.Assembly.GetExecutingAssembly()
    .GetName().Version?.ToString(3) ?? "0.0.0";

TokenSaver.Mcp.StartupLog.Initialize();
TokenSaver.Mcp.StartupLog.Write($"starting v{startupVersion} args=[{string.Join(' ', args)}]");

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    TokenSaver.Mcp.StartupLog.Write($"unhandled exception: {e.ExceptionObject}");

// `tokensaver-mcp print-instructions` emits the copilot-instructions content
// to stdout, so users can pipe it into their repo's .github/ folder during setup:
//   tokensaver-mcp print-instructions > .github/copilot-instructions.md
if (args.Length > 0 && args[0] == "print-instructions")
{
    Console.WriteLine(ServerInstructions);
    return;
}

// `tokensaver-mcp print-overhead` prints the token cost of the MCP overhead
// (server instructions + all tool descriptions). This is the one-time per-session
// cost the running session total subtracts once to show the net break-even point.
if (args.Length > 0 && args[0] == "print-overhead")
{
    var total = TokenSaver.Mcp.FocusedEmitterTools.ComputeOverheadTokens(ServerInstructions);
    var schemaOnly = TokenSaver.Mcp.FocusedEmitterTools.ComputeOverheadTokens("");
    var instructionsOnly = total - schemaOnly;
    Console.WriteLine($"MCP overhead breakdown:");
    Console.WriteLine($"  Server instructions : {instructionsOnly,6} tokens");
    Console.WriteLine($"  Tool descriptions   : {schemaOnly,6} tokens");
    Console.WriteLine($"  Total               : {total,6} tokens");
    return;
}

// `tokensaver-mcp print-version` prints the running NuGet package version and exits.
// The background self-update runs this on a freshly-resolved copy to discover the
// latest version available on the configured feeds. Must stay above
// AutoUpdateRegistrations and the host build so the child exits immediately.
if (args.Length > 0 && args[0] == "print-version")
{
    Console.WriteLine(TokenSaver.Mcp.RegisterCommand.CurrentPackageVersion());
    return;
}

// `tokensaver-mcp self-update` runs the background update check synchronously and
// exits — a manual "update now" trigger, and the deterministic hook used to test
// the update cycle. Ignores the time throttle.
if (args.Length > 0 && args[0] == "self-update")
{
    TokenSaver.Mcp.SelfUpdate.RunInBackgroundAsync(force: true).GetAwaiter().GetResult();
    return;
}

// `tokensaver-mcp register [--local] [--claude-desktop] [--vs]`
// Injects the server entry into Claude Desktop and/or VS 2026 MCP config files.
if (args.Length > 0 && args[0] == "register")
{
    int exitCode = TokenSaver.Mcp.RegisterCommand.Run(args[1..]);
    Environment.Exit(exitCode);
    return;
}

// Silently update TOKENSAVER_API_URL in any already-registered config files
// if it is missing or stale. Gated by a version sentinel — runs once per version.
TokenSaver.Mcp.StartupLog.Write("AutoUpdateRegistrations: begin");
TokenSaver.Mcp.RegisterCommand.AutoUpdateRegistrations();
TokenSaver.Mcp.StartupLog.Write("AutoUpdateRegistrations: done");

TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens =
    TokenSaver.Mcp.FocusedEmitterTools.ComputeOverheadTokens(ServerInstructions);

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var version = System.Reflection.Assembly.GetExecutingAssembly()
    .GetName().Version?.ToString(3) ?? "0.0.0";

builder.Services
    .AddMcpServer(opts =>
    {
        opts.ServerInstructions = ServerInstructions;
        opts.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "tokensaver",
            Version = version,
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

TokenSaver.Mcp.StartupLog.Write("MCP host built, entering RunAsync");

// Fire-and-forget: check the feed for a newer version, prefetch it into the dnx
// cache, and re-pin the registered configs — all off the request critical path.
_ = TokenSaver.Mcp.SelfUpdate.RunInBackgroundAsync();

// Resend any locally-recorded telemetry rows whose upload was never confirmed
// (dropped by a transient failure, a non-2xx response, or a prior exit mid-flight).
TokenSaver.ReportUploader.ResendPendingInBackground();

await builder.Build().RunAsync();
TokenSaver.Mcp.StartupLog.Write("RunAsync returned (server stopped)");
