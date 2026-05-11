# RoslynLean.Mcp

An MCP server that exposes a Roslyn-based focused C# emitter to MCP clients
(Visual Studio Copilot, VS Code Copilot, Claude Desktop). Reduces tokens
sent to the model by 50-70% on typical C# files without losing logic.

## Install

```
dotnet tool install --global RoslynLean.Mcp
```

After install, `roslyn-lean-mcp` is on your PATH.

## Setup (3 steps)

**1. Register the server with your MCP client.**

For Visual Studio 2026 and VS Code, add to `%USERPROFILE%\.mcp.json`:

```json
{
  "servers": {
    "roslyn-lean": {
      "type": "stdio",
      "command": "roslyn-lean-mcp"
    }
  }
}
```

**2. Drop the Copilot instructions file into your repo.**

The MCP spec field for server instructions isn't honored by every client
(in particular VS 2026 Copilot ignores it), so the instructions ship as a
file you copy into each repo where you want auto-invocation:

```
roslyn-lean-mcp print-instructions > .github/copilot-instructions.md
```

**3. Restart your MCP client** so it picks up the new server registration.

## Tools

- `FocusMethod(filePath, methodName, depth=0, minify=false)` — emit the named
  method with full body plus signatures of referenced members. `depth=1`
  includes private helper bodies. `minify=true` strips comments and collapses
  whitespace.
- `MinifyCSharpFile(filePath)` — lossless minify of a whole file. Strips
  comments and whitespace; logic preserved verbatim.
- `AliasCSharpFile(filePath)` — minify + rename private symbols to short
  codes (M1, P1, F1...). Useful on files with very long private names.

Each tool result starts with a token-comparison header:
```
// [Focused Emitter] Tokens without tool: 7,083 → with tool: 3,133 (55% saved)
```

## Logs

Every tool invocation appends a line to:
```
%LOCALAPPDATA%\RoslynLeanMcp\invocations.log
```

The same line is also written to stderr (visible in your MCP client's
output channel).
