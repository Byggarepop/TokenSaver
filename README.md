# TokenSaver

An MCP server for **.NET developers** that gives your AI assistant a token-efficient view of C#, Razor, and .NET project files using the Roslyn compiler platform. Typical reduction: **50–95%** on C# files with no loss of logic.

Works with **Visual Studio 2026** (GitHub Copilot Chat), **Claude Code**, VS Code Copilot, Claude Desktop, and any other MCP client that speaks stdio.

→ **Full docs and setup guide:** [mcp/README.md](mcp/README.md)  
→ **Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## Install

### Visual Studio 2026 / 2022 17.14+

[![Install in Visual Studio](https://img.shields.io/badge/Visual_Studio-Install_TokenSaver-purple?style=flat-square&logo=visualstudio&logoColor=white)](https://vs-open.link/mcp-install?%7B%22name%22%3A%22tokensaver%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dotnet%22%2C%22args%22%3A%5B%22tool%22%2C%22execute%22%2C%22TokenSaver.Mcp%22%2C%22--yes%22%5D%7D)

### VS Code

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install_TokenSaver-0078d4?style=flat-square&logo=visualstudiocode&logoColor=white)](vscode:mcp/install?%7B%22name%22%3A%22tokensaver%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dotnet%22%2C%22args%22%3A%5B%22tool%22%2C%22execute%22%2C%22TokenSaver.Mcp%22%2C%22--yes%22%5D%7D) [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install_TokenSaver-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](vscode-insiders:mcp/install?%7B%22name%22%3A%22tokensaver%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dotnet%22%2C%22args%22%3A%5B%22tool%22%2C%22execute%22%2C%22TokenSaver.Mcp%22%2C%22--yes%22%5D%7D)

### Cursor

Add to `~/.cursor/mcp.json`:
```json
{
  "mcpServers": {
    "tokensaver": {
      "command": "dotnet",
      "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"]
    }
  }
}
```

### Claude Desktop

Add to `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "tokensaver": {
      "command": "dotnet",
      "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"]
    }
  }
}
```

### Other clients, or install everywhere at once

```
dotnet tool install -g TokenSaver.Mcp
tokensaver-mcp register
```

To upgrade later — **close Visual Studio and any Claude sessions first**, then:
```
dotnet tool update --global TokenSaver.Mcp
```

This is the universal fallback — writes config for all detected clients in one shot, and is the recommended path for CI or scripted setup.

> If `dotnet tool execute` isn't recognized, you need [.NET 10 SDK](https://dotnet.microsoft.com/download) or later.

---

## What the tools do

### Single-file tools

| Tool | What it does | Reduction |
|---|---|---|
| `OutlineCSharpFile` | Signatures of every type and member — no bodies. Best for "what's in this file?" | 70–95% |
| `FocusMethod` | Named method with full body + signatures of referenced members. `depth=1` includes private helpers. | 80–97% |
| `FocusMultipleMethods` | Same as above but multiple methods in one parse pass — deduplicates shared signatures. | 80–97% |
| `FocusType` | Non-private members with full bodies, private members as signatures only. | 60–90% |
| `FocusCallers` | All methods in a file that call a given method — focused view. Answers "what calls X?" | 80–95% |
| `MinifyCSharpFile` | Lossless minify of an entire C# file — strips comments and whitespace, logic unchanged. | 20–50% |
| `MinifyFile` | Auto-dispatch by extension. Covers C#, Razor, JS/TS, Python, HTML, CSS, JSON, YAML, XML, C, C++, VB.NET. | varies |
| `AliasCSharpFile` | Minify + rename private symbols to short codes (`M1`, `P1`...). Best on files with very long private names. | 30–60% |

### Cross-file traversal tools

These scan an entire project directory in one call — no need to know which file to look in first.

| Tool | What it does |
|---|---|
| `TraceCallers(projectPath, methodName)` | Finds every `.cs` file across the project where `methodName` is called, and returns a focused view of each caller method. Answers "what calls X across the whole codebase?" |
| `TraceImplementors(projectPath, interfaceName)` | Finds every type that implements or extends a named interface/base across the project, and returns a focused type view for each. Answers "what implements IFoo?" |

Both accept a directory path or `.csproj` file — `obj/` and `bin/` are excluded automatically.

---

## Token savings in practice

Measured against this project's own `FocusedEmitter.cs` (9,261 tokens raw):

| Question type | Tool | Tokens sent | Reduction |
|---|---|---|---|
| "What's in this file?" | `OutlineCSharpFile` | 1,039 | **89%** |
| "Explain the `Emit` method" | `FocusMethod` (depth=1, minify) | 1,437 | **84%** |
| "Explain `EmitOutline` and `EmitMinified`" | `FocusMultipleMethods` | 424 | **95%** |
| "Audit the whole file" | `MinifyCSharpFile` | 5,525 | 40% |

---

## Collective impact

Every invocation is counted at **[tokensavermcp.com](https://tokensavermcp.com)** — a live dashboard showing total tokens saved by the community. Fewer tokens processed means less GPU compute and a smaller carbon footprint for AI-assisted development.

---

## Language support

| Tier | Languages |
|---|---|
| **Primary** (Roslyn, full support) | C# `.cs`, Razor `.razor`, VB.NET `.vb`, .NET project files `.csproj .props .config .xml` |
| **Basic** (comment-strip + whitespace collapse) | JavaScript, TypeScript, Python, HTML, CSS/SCSS/LESS, JSON/JSONC, YAML, C, C++ |

Cross-file traversal tools (`TraceCallers`, `TraceImplementors`) are **C# only**.
