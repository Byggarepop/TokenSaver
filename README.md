# TokenSaver

An MCP server for **.NET developers** that gives your AI assistant a token-efficient view of C#, Razor, and .NET project files using the Roslyn compiler platform. Typical reduction: **50–95%** on C# files with no loss of logic.

Works with **Visual Studio 2026** (GitHub Copilot Chat), **Claude Code**, VS Code Copilot, Claude Desktop, and any other MCP client that speaks stdio.

→ **Full docs and setup guide:** [mcp/README.md](mcp/README.md)  
→ **Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later — required to run the MCP server (`dotnet tool execute`).

---

## Install

> Requires **.NET 10 SDK** or later. The server downloads automatically on first use — no separate install step.

### Visual Studio 2026 / 2022 17.14+

[![Install in Visual Studio](https://img.shields.io/badge/Visual_Studio-Install_TokenSaver-purple?style=flat-square&logo=visualstudio&logoColor=white)](https://vs-open.link/mcp-install?%7B%22name%22%3A%22tokensaver%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dotnet%22%2C%22args%22%3A%5B%22tool%22%2C%22execute%22%2C%22TokenSaver.Mcp%22%2C%22--yes%22%5D%2C%22env%22%3A%7B%22TOKENSAVER_API_URL%22%3A%22https%3A%2F%2Ftokensavermcp.com%22%7D%7D)

<details>
<summary>No installation prompt? Add manually</summary>

Add the entry to `%USERPROFILE%\.mcp.json` on Windows (or `~/.mcp.json` on macOS). Create the file if it doesn't exist. Restart Visual Studio after saving.

```json
{
  "servers": {
    "tokensaver": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"],
      "env": { "TOKENSAVER_API_URL": "https://tokensavermcp.com" }
    }
  }
}
```
</details>

### VS Code

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install_TokenSaver-0078d4?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect?url=vscode:mcp/install?%7B%22name%22%3A%22tokensaver%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dotnet%22%2C%22args%22%3A%5B%22tool%22%2C%22execute%22%2C%22TokenSaver.Mcp%22%2C%22--yes%22%5D%2C%22env%22%3A%7B%22TOKENSAVER_API_URL%22%3A%22https%3A%2F%2Ftokensavermcp.com%22%7D%7D) [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install_TokenSaver-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect?url=vscode-insiders:mcp/install?%7B%22name%22%3A%22tokensaver%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dotnet%22%2C%22args%22%3A%5B%22tool%22%2C%22execute%22%2C%22TokenSaver.Mcp%22%2C%22--yes%22%5D%2C%22env%22%3A%7B%22TOKENSAVER_API_URL%22%3A%22https%3A%2F%2Ftokensavermcp.com%22%7D%7D)

<details>
<summary>No installation prompt? Add manually</summary>

Open **User Settings (JSON)** via `Ctrl+Shift+P` → *Open User Settings (JSON)* and merge in the following. Reload VS Code after saving.

```json
{
  "mcp": {
    "servers": {
      "tokensaver": {
        "type": "stdio",
        "command": "dotnet",
        "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"],
        "env": { "TOKENSAVER_API_URL": "https://tokensavermcp.com" }
      }
    }
  }
}
```
</details>

### Claude Code

Run once in your terminal — installs globally across all projects:
```
claude mcp add -s user tokensaver -e TOKENSAVER_API_URL=https://tokensavermcp.com -- dotnet tool execute TokenSaver.Mcp --yes
```

### Cursor

Add to `~/.cursor/mcp.json`:
```json
{
  "mcpServers": {
    "tokensaver": {
      "command": "dotnet",
      "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"],
      "env": { "TOKENSAVER_API_URL": "https://tokensavermcp.com" }
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
      "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"],
      "env": { "TOKENSAVER_API_URL": "https://tokensavermcp.com" }
    }
  }
}
```

### Install everywhere at once

The `register` command detects installed MCP clients and writes the config for all of them in one shot. Recommended for CI and scripted setup.

```
dotnet tool install -g TokenSaver.Mcp
tokensaver-mcp register
```

`register` writes the server entry into:
- `%APPDATA%\Claude\claude_desktop_config.json` — Claude Desktop
- `%USERPROFILE%\.claude\claude.json` — Claude Code CLI
- `%APPDATA%\Code\User\settings.json` — VS Code / GitHub Copilot (skipped if not installed)
- `%USERPROFILE%\.mcp.json` — Visual Studio 2026 (global)

It merges safely — existing entries from other MCP servers are left untouched. Restart your MCP host after running it.

**Flags:**
- `--claude-desktop` / `--claude-code` / `--vscode` / `--vs` — register only one target
- `--local` — write a solution-local `mcp.json` in the current directory instead of the global VS config (useful when you want per-repo opt-in)

### Upgrade & Uninstall

**To upgrade** — close all MCP clients first, then:

```
dotnet tool update --global TokenSaver.Mcp
```

- **Claude Code / Claude Desktop** — close the app or end the session.
- **Visual Studio** — close the IDE fully, not just the chat panel.

Restart your client after the update.

> Visual Studio reads cached tool metadata at startup and only launches the server on your first Copilot prompt. After an upgrade, VS may briefly show old metadata — that's normal. If tools look wrong, rename the entry in `%USERPROFILE%\.mcp.json` (e.g. `tokensaver` → `tokensaver-2`), restart VS, send one prompt, then rename back to force a cache rebuild.

**To uninstall:**

```
dotnet tool uninstall --global TokenSaver.Mcp
claude mcp remove tokensaver -s user     # if you used Claude Code
```

For VS, also delete the `tokensaver` block from `%USERPROFILE%\.mcp.json`.

> **Maintainer:** when publishing a new NuGet version, update the `"version"` fields in `server.json` and run `mcp-publisher publish` to keep the MCP Registry entry in sync.

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
| **Basic** (comment-strip + whitespace collapse) | JavaScript, TypeScript, Python, HTML, CSS/SCSS/LESS, JSON/JSONC, YAML, C, C++, Markdown |

Cross-file traversal tools (`TraceCallers`, `TraceImplementors`) are **C# only**.
