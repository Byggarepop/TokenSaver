// MCP server entry point. Speaks JSON-RPC over stdio — Visual Studio (and any
// other MCP host) launches this exe and communicates via stdin/stdout.
//
// CRITICAL: Nothing else may write to stdout. Logs go to stderr. The host
// parses every stdout byte as protocol traffic.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string ServerInstructions = """
This server (roslyn-lean) exposes three tools that produce a TOKEN-REDUCED
view of C# files. PREFER these tools over reading whole files whenever the
task involves C# source — they save 50-70% of tokens on typical files with
no loss of logic.

TOOL SELECTION RULES — follow by default, no need to ask the user:

1. User references a specific method ("look at Foo in Bar.cs", "speed up X",
   "translate this WinForms method to Razor") → call FocusMethod with
   methodName set, depth=1, and minify=true. depth=1 includes the bodies of
   private helpers; without those, your suggestions will hallucinate helper
   logic.

2. User wants to read or analyze a whole C# file without naming a specific
   method → call MinifyCSharpFile. Lossless, ~20-50% reduction.

3. File is dominated by long private symbol names → consider AliasCSharpFile.
   The result has private members renamed to short codes with a ledger.

SKIP these tools for: non-C# files, small files (<50 lines), or when the
user explicitly asks you to read the raw file.

THE TOOL OUTPUT IS A SUMMARY VIEW, NOT THE SOURCE OF TRUTH:
- Comments and XML docs are stripped from output; they exist in the real file.
- Whitespace and indentation are collapsed; the real file is formatted.
- AliasCSharpFile renames private symbols to short codes; the real file uses
  the original names (the ledger maps back).

When suggesting code or making edits, always:
- Format suggested code in conventional idiomatic C# style (proper
  indentation, blank lines, no minification carried into your output).
- Preserve existing comments and XML docs when modifying a method.
- Add XML doc comments to new public APIs following project convention.
- Use original symbol names (not M1/P1/F1 aliases) in code the user will
  paste into their file.
- In agent / edit mode, READ THE FILE FROM DISK before applying changes.
  Tool output is a reasoning aid, not a basis for the edit text.

REPORTING TO THE USER:
Each tool result starts with a header like
"// [Focused Emitter] Tokens without tool: 7,083  →  with tool: 3,133 (55% saved)".
Mention the savings to the user in your reply — one short sentence is enough.
The user wants visibility into how much context was reduced.

NOTE: If the user attaches a file via VS Copilot's #filename.cs syntax, the
file content is already inlined by the IDE before this server is consulted —
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
