// MCP server entry point. Speaks JSON-RPC over stdio — Visual Studio (and any
// other MCP host) launches this exe and communicates via stdin/stdout.
//
// CRITICAL: Nothing else may write to stdout. Logs go to stderr. The host
// parses every stdout byte as protocol traffic.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string ServerInstructions = """
This server (tokensaver) exposes twelve tools that produce TOKEN-REDUCED views
of source files. PREFER these tools over reading whole files whenever the
task involves a supported file type — they save 30-95% of tokens with no
loss of logic. Supported types are listed after the rules.

TOOL SELECTION RULES — follow by default, no need to ask the user:

1. Codebase navigation ("what's in this file?", "list the methods on Foo")
   → OutlineCSharpFile. Signatures only, no bodies, typical 70-95% reduction.
   C# only.

2. User references a specific C# method ("look at Foo in Bar.cs", "speed up X")
   → FocusMethod with methodName set, depth=1, and minify=true. depth=1
   includes the bodies of private helpers; without those, your suggestions
   will hallucinate helper logic. methodName also accepts a CLASS NAME to
   target a constructor. C# only.
   TWO OR MORE methods at once → FocusMultipleMethods with a comma-separated
   methodNames list (class names allowed too); one parse, deduplicated
   signatures — smaller than N separate FocusMethod calls.
   On a NOT FOUND, act on any hint in the response: a partial type's member
   may be in a sibling file (glob the folder for the type's other parts); a
   type with a base list may inherit the member (focus the file declaring the
   base type). Don't give up or guess the body.

3. Read or analyze a whole file of any supported type → MinifyFile.
   Auto-dispatches by extension. For C#, MinifyCSharpFile is equivalent
   (back-compat).

4. C# file dominated by long private symbol names → consider AliasCSharpFile.
   Private members renamed to short codes with a ledger. C# only.

5. What calls a method across the WHOLE PROJECT → TraceCallers with the
   project directory or .csproj path and the method name. Use instead of
   FocusCallers when you don't know which file to look in. C# only.
   EXCEPTION — existence checks ("is X used?", "does anything reference X?"):
   use Grep first; only escalate to TraceCallers when you need to see HOW
   callers use the method. A widely-used method can cost 100K+ tokens.

6. What implements an interface or extends a base type → TraceImplementors
   with the project directory or .csproj path and the type name. Returns a
   focused type view per implementor found across the project. C# only.

7. Where a type is registered / wired in DI, what it resolves to, or its
   lifetime — OR a constructor caller-trace for a DI-constructed type came
   back empty (the container builds it, no 'new') → TraceDiRegistrations with
   the project directory or .csproj path and the type name (interface OR
   concrete). Compact table of every Add/TryAdd/AddKeyed registration:
   file:line, method, ServiceType -> ImplType, keyed key. Chain to
   FocusMethod / TraceImplementors for the implementation body. C# only.

8. Don't know which file a type is in, or want a project overview →
   MapProject with the project directory or .csproj path; use instead of Grep
   for type discovery, then drill in with FocusMethod / FocusType. Pass
   nameFilter to narrow on large repos. C# only. DISABLED BY DEFAULT (opt-in
   via TOKENSAVER_ENABLE_MAP_PROJECT=1); on a "disabled" notice do not retry —
   fall back to Grep, FocusType, or OutlineCSharpFile.

SKIP these tools for: unsupported file types (.txt, .sql, binary), small files
(<50 lines), or when the user explicitly asks you to read the raw file.

In agent / edit mode, comprehension still goes through a tokensaver tool
FIRST — never Read a supported file just to understand it before editing.
Only after the tool has shown you the target do you Read, and then only the
lines containing the match string (±5) — never the whole file, and never
before the tool. This applies per-file, every time: having used a tool
earlier this turn, or having edited another file already, does NOT license a
raw Read of the next file to understand it.

SUPPORTED FILE TYPES (via MinifyFile, auto-dispatched by extension):
  C#/Razor (.cs, .razor.cs, .razor) · JavaScript (.js, .mjs, .cjs, .jsx) ·
  TypeScript (.ts, .tsx, .mts, .cts) · Python (.py, .pyi) · HTML (.html,
  .htm) · CSS/SCSS/LESS · JSON/JSONC · YAML (.yaml, .yml) · XML/.NET project
  (.xml, .csproj, .props, .targets, .config, .resx) · C (.c, .h) · C++ (.cpp,
  .cc, .cxx, .hpp, .hh, .hxx, .inl) · X++ (.xpp) · VB.NET (.vb) · Markdown
  (.md, .markdown)

THE TOOL OUTPUT IS A SUMMARY VIEW, NOT THE SOURCE OF TRUTH:
- Comments, XML docs, and #region directives are stripped; they exist in the
  real file. Field signatures omit initializers ("private int _count;" not
  "= 0").
- Whitespace is collapsed (indent-sensitive formats keep indentation); the
  real file is conventionally formatted.
- AliasCSharpFile renames private C# symbols to short codes; the real file
  uses the original names (the ledger maps back).
- Tools NEVER return more tokens than the original file.

When suggesting code or making edits, always:
- Format suggested code idiomatically — no minification carried into output.
- Preserve existing comments and doc comments when modifying a function.
- Use original symbol names (not M1/P1/F1 aliases) in code the user will
  paste into their file.

REPORTING TO THE USER:
Each tool result starts with a token-comparison header, up to three lines:
"// [Focused Emitter] Tokens without tool: 7,083  →  with tool: 3,133 (55% saved)"
"// vs a targeted read of just the relevant code (4,200 tokens): 25% saved"
"// session: 4 calls · saved 24,800 · net 22,700 after 2,100 overhead"
Line 1 compares against reading the WHOLE file (a best case); line 2, when
present, against reading only the relevant code. Mention the savings in one
short sentence; prefer line 2 or give the range — don't claim the whole-file
figure as guaranteed. On a repeat view of the same file, line 1 becomes a
"repeat view" note and the whole-file saving is not credited again — never
re-report a whole-file "% saved" for a repeat view.

NOTE: VS Copilot's #filename syntax and the Active Document context button
inline the entire file BEFORE this server is consulted. For token reduction,
reference files as plain text (e.g. "look at OnRunSql in SqlQuery.razor") and
remove any # or Active Document reference; reserve those for small files.
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
