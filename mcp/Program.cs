// MCP server entry point. Speaks JSON-RPC over stdio — Visual Studio (and any
// other MCP host) launches this exe and communicates via stdin/stdout.
//
// CRITICAL: Nothing else may write to stdout. Logs go to stderr. The host
// parses every stdout byte as protocol traffic.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string ServerInstructions = """
This server (roslyn-lean) exposes five tools that produce TOKEN-REDUCED views
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

TOOL SELECTION RULES — follow by default, no need to ask the user:

1. User wants codebase navigation — "what's in this file?", "where would I
   add X?", "list the methods on Foo" → call OutlineCSharpFile. Signatures
   only, no bodies, typical 70-95% reduction. C# only.

2. User references a specific C# method ("look at Foo in Bar.cs", "speed up X",
   "translate this WinForms method to Razor") → call FocusMethod with
   methodName set, depth=1, and minify=true. depth=1 includes the bodies of
   private helpers; without those, your suggestions will hallucinate helper
   logic. C# only.

3. User wants to read or analyze a whole file of any supported type → call
   MinifyFile. Auto-dispatches by extension. For C#, MinifyCSharpFile is
   equivalent (back-compat).

4. C# file dominated by long private symbol names → consider AliasCSharpFile.
   Private members renamed to short codes with a ledger. C# only.

SKIP these tools for: unsupported file types (.md, .txt, binary), small files
(<50 lines), or when the user explicitly asks you to read the raw file.

THE TOOL OUTPUT IS A SUMMARY VIEW, NOT THE SOURCE OF TRUTH:
- Comments are stripped from output; they exist in the real file.
- Whitespace is collapsed (C#/JS/TS/JSON) or trailing/blank-runs trimmed
  (Python/YAML/XML preserve indentation since those formats are
  indent-sensitive); the real file is conventionally formatted.
- AliasCSharpFile renames private C# symbols to short codes; the real file
  uses the original names (the ledger maps back).

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

NOTE: If the user attaches a file via VS Copilot's #filename syntax, the file
content is already inlined by the IDE before this server is consulted —
calling our tools at that point is redundant. For token reduction, the user
should reference files as plain text paths instead of #-attachments.
""";

// `roslyn-lean-mcp print-instructions` emits the copilot-instructions content
// to stdout, so users can pipe it into their repo's .github/ folder during setup:
//   roslyn-lean-mcp print-instructions > .github/copilot-instructions.md
if (args.Length > 0 && args[0] == "print-instructions")
{
    Console.WriteLine(ServerInstructions);
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
