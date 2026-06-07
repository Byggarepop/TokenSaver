// MCP server entry point. Speaks JSON-RPC over stdio — Visual Studio (and any
// other MCP host) launches this exe and communicates via stdin/stdout.
//
// CRITICAL: Nothing else may write to stdout. Logs go to stderr. The host
// parses every stdout byte as protocol traffic.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string ServerInstructions = """
This server (tokensaver) exposes ten tools that produce TOKEN-REDUCED views
of source files. PREFER these tools over reading whole files whenever the
task involves a supported file type — they save 30-95% of tokens with no
loss of logic.

SUPPORTED FILE TYPES (via MinifyFile, auto-dispatched by extension):
  C# / Razor           .cs, .razor.cs (Roslyn), .razor (markup + @code combined)
  JavaScript           .js, .mjs, .cjs, .jsx
  TypeScript           .ts, .tsx, .mts, .cts
  Python               .py, .pyi
  HTML                 .html, .htm
  CSS / SCSS / LESS    .css, .scss, .less
  JSON / JSONC         .json, .jsonc
  YAML                 .yaml, .yml
  XML / .NET project   .xml, .csproj, .props, .targets, .config, .resx
  C                    .c, .h
  C++                  .cpp, .cc, .cxx, .hpp, .hh, .hxx, .inl
  X++                  .xpp
  VB.NET               .vb
  Markdown             .md, .markdown

TOOL SELECTION RULES — follow by default, no need to ask the user:

1. User wants codebase navigation — "what's in this file?", "where would I
   add X?", "list the methods on Foo" → call OutlineCSharpFile. Signatures
   only, no bodies, typical 70-95% reduction. C# only.

2. User references a specific C# method ("look at Foo in Bar.cs", "speed up X",
   "translate this WinForms method to Razor") → call FocusMethod with
   methodName set, depth=1, and minify=true. depth=1 includes the bodies of
   private helpers; without those, your suggestions will hallucinate helper
   logic. C# only.

   methodName also accepts a CLASS NAME to target a constructor — e.g.
   methodName="MyService" focuses on the MyService(...) constructor body.

   User references TWO OR MORE C# methods at once, or a prior outline/NOT FOUND
   revealed which methods are relevant → call FocusMultipleMethods with a
   comma-separated methodNames list (e.g. "ExecSql,ClearGrid,SetBusy"). The
   file is parsed once and shared signatures are deduplicated — smaller output
   than N separate FocusMethod calls and one round-trip instead of N. C# only.
   Class names are accepted here too (mixed with method names is fine).

   On a NOT FOUND, act on any hint in the response: when the file's type is
   partial, the member may be in a sibling file in the same namespace/folder —
   glob that folder for the type's other parts and focus the right one; when the
   type has a base list, the member may be inherited — focus the file that
   declares the base type. Don't give up or guess the body.

3. User wants to read or analyze a whole file of any supported type → call
   MinifyFile. Auto-dispatches by extension. For C#, MinifyCSharpFile is
   equivalent (back-compat).

4. C# file dominated by long private symbol names → consider AliasCSharpFile.
   Private members renamed to short codes with a ledger. C# only.

5. User asks what calls a given method across the WHOLE PROJECT ("what calls X
   anywhere?", "find all callers of Foo", "who calls this across the codebase?")
   → call TraceCallers with the project directory or .csproj path and the method
   name. Returns focused caller views from every file that calls it. Use instead
   of FocusCallers when you don't know which file to look in. C# only.
   EXCEPTION - existence checks: if the question is "is X used?", "is X called
   anywhere?", or "does anything reference X?", use Grep first. Only escalate
   to TraceCallers if you need to see HOW callers use the method, not just
   confirm it is called. A widely-used method can cost 100K+ tokens.

6. User asks what implements an interface or extends a base type ("what
   implements IFoo?", "what extends BaseBar?", "show me all emitters") → call
   TraceImplementors with the project directory or .csproj path and the
   interface/base type name. Returns a focused type view for each implementor
   found across the project. C# only.

7. User asks where a type is registered / wired in Dependency Injection, what
   concrete a DI interface resolves to, or what lifetime it has ("where is IFoo
   registered?", "what's IFoo wired to?", "is Foo a singleton?") — OR a
   constructor caller-trace for a DI-constructed type came back empty (no 'new'
   because the container builds it) → call TraceDiRegistrations with the project
   directory or .csproj path and the type name (interface OR concrete). Returns
   a compact table of every Add/TryAdd/AddKeyed registration referencing it:
   file:line, method, ServiceType -> ImplType, and keyed key. Then chain to
   FocusMethod / TraceImplementors if you need the implementation body. C# only.

SKIP these tools for: unsupported file types (.txt, .sql, binary), small files
(<50 lines), or when the user explicitly asks you to read the raw file.

THE TOOL OUTPUT IS A SUMMARY VIEW, NOT THE SOURCE OF TRUTH:
- Comments and XML doc comments are stripped from output; they exist in the real file.
- #region / #endregion directives are stripped — pure organisation, no logic.
- Whitespace is collapsed (C#/JS/TS/JSON) or trailing/blank-runs trimmed
  (Python/YAML/XML preserve indentation since those formats are
  indent-sensitive); the real file is conventionally formatted.
- Field signatures omit initializers (e.g. "private int _count;" not "= 0").
- AliasCSharpFile renames private C# symbols to short codes; the real file
  uses the original names (the ledger maps back).
- Tools NEVER return more tokens than the original file — if minification
  yields no gain, the original file content is returned unchanged.

When suggesting code or making edits, always:
- Format suggested code in the language's idiomatic style (proper indentation,
  blank lines, no minification carried into your output).
- Preserve existing comments and doc comments when modifying a function.
- Use original symbol names (not M1/P1/F1 aliases) in code the user will
  paste into their file.
- In agent / edit mode, comprehension still goes through a tokensaver tool
  FIRST — never Read a supported file just to understand it before editing.
  Only after the tool has shown you the target do you Read, and then only the
  lines containing the match string (the insertion region ±5) — never the
  whole file, and never before the tool. Tool output is a reasoning aid, not
  a basis for the edit text.
- Mid-edit-flow is the trap, not the first read. The requirement is per-file,
  every time: each new supported file you open for comprehension resets it.
  Having used a tool earlier this turn, or having edited another file already,
  does NOT license a raw Read of the next file to understand it. That momentum
  hits hardest in the second half of a task — that is exactly when to run the
  tool instead.

REPORTING TO THE USER:
Each tool result starts with a token-comparison header. For the focused tools it
has up to three lines, e.g.:
"// [Focused Emitter] Tokens without tool: 7,083  →  with tool: 3,133 (55% saved)"
"// vs a targeted read of just the relevant code (4,200 tokens): 25% saved"
"// session: 4 calls · raw saved 24,800 · net of 2,100 one-time MCP overhead = 22,700"
The first line compares against reading the WHOLE file (a best case); the second,
when present, compares against reading only the relevant code (a careful reader's
real alternative). Mention the savings in one short sentence, and do NOT claim the
whole-file figure as if it were guaranteed — if the second line is present, prefer
it or give the range (e.g. "saved ~25-55% vs reading the file").

If you view the SAME file more than once in a session (a different method, or
outline-then-minify), the first line is replaced by "repeat view of this file
this session — whole-file baseline already counted ...". A file only costs its
whole-file tokens once, so later views are not credited that saving again; the
session total counts each file's baseline a single time. Do not re-report the
whole-file "% saved" for a repeat view — it was already counted on the first.

NOTE: VS Copilot's #filename syntax AND the Active Document context button
both inline the entire file into the prompt BEFORE this server is consulted —
our tools cannot intercept that content. For token reduction, the user should
reference files as plain text (e.g. "look at OnRunSql in SqlQuery.razor")
and remove any # or Active Document reference. Reserve those for small files
where reduction doesn't matter.
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
