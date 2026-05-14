// MCP server entry point. Speaks JSON-RPC over stdio — Visual Studio (and any
// other MCP host) launches this exe and communicates via stdin/stdout.
//
// CRITICAL: Nothing else may write to stdout. Logs go to stderr. The host
// parses every stdout byte as protocol traffic.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string ServerInstructions = """
This server (tokensaver) exposes six tools that produce TOKEN-REDUCED views
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

3. User wants to read or analyze a whole file of any supported type → call
   MinifyFile. Auto-dispatches by extension. For C#, MinifyCSharpFile is
   equivalent (back-compat).

4. C# file dominated by long private symbol names → consider AliasCSharpFile.
   Private members renamed to short codes with a ledger. C# only.

SKIP these tools for: unsupported file types (.md, .txt, binary), small files
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
- In agent / edit mode, READ THE FILE FROM DISK before applying changes.
  Tool output is a reasoning aid, not a basis for the edit text.

REPORTING TO THE USER:
Each tool result starts with a header like
"// [Focused Emitter] Tokens without tool: 7,083  →  with tool: 3,133 (55% saved)".
Mention the savings to the user in your reply — one short sentence is enough.

NOTE: VS Copilot's #filename syntax AND the Active Document context button
both inline the entire file into the prompt BEFORE this server is consulted —
our tools cannot intercept that content. For token reduction, the user should
reference files as plain text (e.g. "look at OnRunSql in SqlQuery.razor")
and remove any # or Active Document reference. Reserve those for small files
where reduction doesn't matter.
""";

// `tokensaver-mcp print-instructions` emits the copilot-instructions content
// to stdout, so users can pipe it into their repo's .github/ folder during setup:
//   tokensaver-mcp print-instructions > .github/copilot-instructions.md
if (args.Length > 0 && args[0] == "print-instructions")
{
    Console.WriteLine(ServerInstructions);
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

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer(opts => opts.ServerInstructions = ServerInstructions)
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
