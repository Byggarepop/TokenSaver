# TokenSaver.Mcp

<!-- mcp-name: io.github.byggarepop/tokensaver -->

An MCP server built for **.NET developers** — it gives your AI assistant a
token-efficient view of C#, Razor, and .NET project files using the Roslyn
compiler platform. Typical reduction: **50–95 %** on C# files with no loss
of logic.

Works with:
- **Visual Studio 2026** (GitHub Copilot Chat)
- **Claude Code** (CLI)
- Any other MCP client that speaks stdio (VS Code Copilot, Claude Desktop, etc.)

→ [Changelog](https://github.com/Byggarepop/TokenSaver/blob/main/CHANGELOG.md)

> **Language support tiers**
>
> | Tier | Languages | Status |
> |---|---|---|
> | **Primary** | C# (`.cs`), Razor (`.razor`), VB.NET (`.vb`), .NET project files (`.csproj`, `.props`, `.config`, `.xml`) | Fully supported, actively tested |
> | **Basic** | JavaScript, TypeScript, Python, HTML, CSS/SCSS/LESS, JSON/JSONC, YAML | Comment-strip + whitespace collapse only — not actively tested, results may vary |
>
> If you work exclusively in .NET, the basic-tier languages are a bonus, not
> a selling point.

## What the tools do

All ten tools are **C#/Razor-first**. `MinifyFile` also dispatches to the
basic-tier minifiers for other extensions.

### Single-file tools

- `FocusMethod(filePath, methodName, depth=0, minify=false)` — emit the named
  method with full body plus signatures of referenced members. `depth=1`
  also includes bodies of private helper methods and properties accessed by
  the focus method. `minify=true` strips comments, `#region`/`#endregion`
  directives, and collapses whitespace. Pass the **class name** as `methodName`
  to target a constructor. **C# / Razor only.**
- `FocusMultipleMethods(filePath, methodNames, depth=0, minify=false)` — same
  as `FocusMethod` but focuses on multiple methods in one parse pass. Class
  names (constructors) can be mixed with method names. **C# / Razor only.**
- `FocusType(filePath, typeName, minify=false)` — emit a named type with
  non-private members shown as full bodies and private members as signatures
  only. Best for "explain class X" questions when the file has multiple types
  or private helpers dominate. **C# only.**
- `FocusCallers(filePath, methodName, depth=0, minify=false)` — find all
  methods in a **single file** that call the named method and return them as a
  focused multi-method view. Answers "what calls X?" in one round-trip.
  **C# only.** For project-wide search, use `TraceCallers`.
- `OutlineCSharpFile(filePath)` — skeleton of a file: types and member
  signatures, no bodies. Best for navigation ("what's in this file?").
  **C# / Razor only.**
- `MinifyCSharpFile(filePath)` — lossless minify of a whole C# file. Strips
  comments, `#region`/`#endregion` directives, and whitespace; logic preserved
  verbatim. **C# / Razor only.**
- `MinifyFile(filePath)` — auto-dispatch by extension. Calls the Roslyn
  minifier for C#/Razor; falls back to basic minification for other types.
- `AliasCSharpFile(filePath)` — minify plus rename private symbols to short
  codes (`M1`, `P1`, `F1`...). Useful on files with very long private names.
  **C# / Razor only.**

### Cross-file traversal tools

These scan an entire project directory in one call — no need to know which file
to look in first. Both accept a directory path or `.csproj` file; `obj/` and
`bin/` are excluded automatically. **C# only.**

- `TraceCallers(projectPath, methodName, depth=0, minify=false)` — scans every
  `.cs` file in the project and returns focused views of all methods that call
  `methodName`, grouped by file. Answers "what calls X across the whole
  codebase?" in a single call. Uses name-based matching, same as `FocusCallers`.
- `TraceImplementors(projectPath, interfaceName, minify=false)` — finds every
  type that implements or extends the named interface or base type, and returns
  a focused type view for each. Answers "what implements IFoo?" or "what extends
  BaseBar?" in a single call.

Each tool result starts with a token-comparison header:
```
// [Focused Emitter] Tokens without tool: 7,083 → with tool: 3,133 (55% saved)
```

Every invocation also appends a JSON entry to
`%USERPROFILE%\.tokensaver\report.json` and emits a one-line summary to
stderr (visible in your MCP client's output channel).

---

## What this looks like in practice

Measured against this project's own `FocusedEmitter.cs` (9,261 tokens raw):

| Question type | Tool used | Tokens sent to AI | Reduction |
|---|---|---|---|
| "What's in this file?" | `OutlineCSharpFile` | 1,039 | **89 %** |
| "Explain the `Emit` method" | `FocusMethod` (depth=1, minify) | 1,437 | **84 %** |
| "Explain `EmitOutline` and `EmitMinified`" | `FocusMultipleMethods` (minify) | 424 | **95 %** |
| "Audit the whole file" | `MinifyCSharpFile` | 5,525 | 40 % |

The focus example includes `Emit`'s full body, the bodies of 6 private
helpers it calls, and signatures of 45 other referenced symbols — enough
context for the AI to reason accurately, without the 7,800 tokens of
unrelated members.

### See the collective impact

Every invocation is counted at **[tokensavermcp.com](https://tokensavermcp.com)** — a live dashboard showing how many tokens the community has saved in total. Fewer tokens processed means less GPU compute, less energy drawn from the grid, and a smaller carbon footprint for AI-assisted development. When you use these tools, you are not just speeding up your own workflow — you are contributing to a more efficient use of AI infrastructure.

---

### Important: this helps with READ operations, not EDITS

These tools strip comments, collapse whitespace, and sometimes rename
private symbols. The output is a **reasoning aid** — perfect for
understanding code, explaining it, designing a refactor, translating it
to another language, or finding a bug.

It is **not** a faithful representation of the file on disk. When the AI
is actually editing your code, it needs the real text — original
indentation, blank lines between members, XML doc comments, and original
symbol names — so the edit matches what's there and your formatting
survives the change. A good agent will read the raw file before writing
back to it.

In short: **big savings on understanding, smaller savings on editing.**
That's intentional — correctness matters more than tokens when code is
changing.

---

## Install, upgrade & uninstall

### Install

**One-click for Visual Studio and VS Code** — see the badges on the
[GitHub README](https://github.com/Byggarepop/TokenSaver#install). The server
downloads automatically on first use via `dotnet tool execute`; no prior
install needed.

**Install everywhere at once** using the global tool and the `register` command:

```
dotnet tool install --global TokenSaver.Mcp
tokensaver-mcp register
```

`register` detects your environment and writes the server entry into:
- `%APPDATA%\Claude\claude_desktop_config.json` — Claude Desktop
- `%USERPROFILE%\.claude\claude.json` — Claude Code CLI
- `%APPDATA%\Code\User\settings.json` — VS Code / GitHub Copilot (skipped if not installed)
- `%USERPROFILE%\.mcp.json` — Visual Studio 2026 (global)

It merges safely — existing entries from other MCP servers are left untouched.
Restart your MCP host after running it.

**Flags:**
- `--claude-desktop` / `--claude-code` / `--vscode` / `--vs` — register only one target
- `--local` — write a solution-local `mcp.json` in the current directory
  instead of the global VS config (useful when you want per-repo opt-in)

After install, `tokensaver-mcp` is on your PATH. You can also verify the
instruction text the server advertises to AI clients:

```
tokensaver-mcp print-instructions
```

### Upgrade

```
dotnet tool update --global TokenSaver.Mcp
```

> **Maintainer note:** when publishing a new NuGet version, also update the
> `"version"` fields in `server.json` (repo root) to match, then run
> `mcp-publisher publish` to keep the MCP Registry entry current.

**Close all MCP clients before running this.** The old server process must not
be running when the tool is replaced on disk — dotnet will fail or silently
install alongside the old binary if the executable is locked.

- **Claude Code / Claude Desktop** — close the app or end the session.
- **Visual Studio** — close the IDE fully, not just the chat panel.

After the update, restart your client as normal.

#### Visual Studio: lazy loading and the first-prompt delay

Visual Studio does not start the MCP server process at IDE startup. Instead it
reads cached tool metadata from disk (you'll see `Loaded cached state for MCP
server 'tokensaver'...` in the Copilot output channel), and only launches the
actual server process when you send your first Copilot prompt of the session.

This means:

- **After an upgrade**, VS may show the old cached metadata in the output
  channel until the first prompt triggers a fresh start. That is normal — the
  new binary is running from the first prompt onward.
- **If tools look wrong after an upgrade** (e.g. a tool you expect is missing),
  use the cache-bust trick: rename the server entry in `%USERPROFILE%\.mcp.json`
  (e.g. `tokensaver` → `tokensaver-2`), restart VS, send one prompt, then rename
  back. VS will rebuild the cache from scratch.

### Uninstall

```
dotnet tool uninstall --global TokenSaver.Mcp
claude mcp remove tokensaver -s user     # if you used Claude Code
```

For VS, delete the `tokensaver` block from `%USERPROFILE%\.mcp.json`.

---

## Manual setup for Claude Code

Two one-time steps (skip if you used `register` above).

**1. Register the MCP server at user scope** so it's available in every project:

```
claude mcp add -s user tokensaver -e TOKENSAVER_API_URL=https://tokensavermcp.com -- dotnet tool execute TokenSaver.Mcp --yes
```

Verify:
```
claude mcp get tokensaver
```
Should show `Scope: User config (available in all your projects)` and
`Status: ✓ Connected`.

**2. Add a global `CLAUDE.md`** so Claude reaches for the MCP tools instead of
the built-in `Read` for C# files. From PowerShell:

```powershell
tokensaver-mcp print-instructions | Out-File -Append -Encoding utf8 $HOME\.claude\CLAUDE.md
```

(From bash / cmd: `tokensaver-mcp print-instructions >> "%USERPROFILE%\.claude\CLAUDE.md"`.)

That's it. **Start a new Claude Code session** (the tool list is fixed at
session start — existing sessions won't see the new server) and Claude will
auto-invoke the tools on C# work.

### Verify it works

In a new Claude Code session, ask something like:
> Look at the `OnInitializedAsync` method in some C# file and explain it.

Then check `%USERPROFILE%\.tokensaver\report.json` — a new JSON entry means
the tool was invoked.

---

## Manual setup for Visual Studio 2026 (GitHub Copilot Chat)

Two one-time steps (skip step 1 if you used `register` above).

**1. Register the MCP server.** Create or edit `%USERPROFILE%\.mcp.json`:

```json
{
  "servers": {
    "tokensaver": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"],
      "env": {
        "TOKENSAVER_API_URL": "https://tokensavermcp.com"
      }
    }
  }
}
```

**2. Restart Visual Studio** so it loads the new server registration.

### Verify it works

- *View → Output*, channel = *GitHub Copilot*. On startup you should see:
  ```
  Successfully started MCP server 'tokensaver'
  Loaded assets for MCP server 'tokensaver' with 10 tools, 0 prompts, and 0 resources.
  ```
- Send a normal prompt in Copilot Chat (no `#` reference):
  > Look at the `OnInitializedAsync` method in `C:\path\to\Foo.cs` and explain it.
- Check `%USERPROFILE%\.tokensaver\report.json` for a new entry.

### How to prompt — plain text, not # references

VS Copilot's `#filename.cs` syntax and the **Active Document** context
button both inline the entire file content into the prompt *before* Copilot
sees your message. The MCP tool can't intercept that — by the time the model
decides whether to call a tool, the file is already in context. Both bypass
token reduction entirely and send the full raw file to the model.

**To benefit from token reduction, reference files and methods as plain text:**

```
Look at the OnInitializedAsync method in MyPage.razor and explain it.
```

Not:

```
#MyPage.razor explain OnInitializedAsync    ← sends the whole file, bypasses the tool
```

Reserve `#` references and Active Document for small files where the overhead
doesn't matter.

> This applies to Visual Studio 2026 Copilot Chat. VS Code Copilot behaviour
> may differ — verify with your version.

### VS-specific gotchas
- **VS caches MCP server metadata** keyed by server name. If you change the
  server's tool definitions or instructions, VS may keep using cached state
  (you'll see `Loaded cached state for MCP server 'tokensaver'...` in the
  output channel instead of `Successfully started MCP server...`). Easiest
  cache bust: rename the server in `.mcp.json` (e.g. `tokensaver` →
  `tokensaver-2`) and restart VS. Rename back afterward if you like.
- **Pick a supported model** in the Copilot Chat model dropdown. Some models
  aren't available on free / limited SKUs and will fail the chat entirely
  with an error like `The requested model is not supported`.

---

## Telemetry

Each tool invocation sends a small anonymous report to
[tokensavermcp.com](https://tokensavermcp.com) to power the community
dashboard. Here is exactly what is included:

| Field | Example | Notes |
|---|---|---|
| `ToolName` | `Focused Emitter` | The tool that was called |
| `Language` | `C#` | Language detected from the file extension |
| `TokensWithoutTool` | `9202` | Estimated token count of the original file |
| `TokensWithTool` | `1039` | Estimated token count of the tool output |
| `Notes` | `focus=OnInitializedAsync depth=1 minify=True` | Mode string — includes the method name when using `FocusMethod` |
| `ClientId` | `9202828d...` | Random UUID generated once and stored in `%USERPROFILE%\.tokensaver\token-saver-client-id`. Never tied to a name or email. |

**What is never sent:** file paths, file contents, your source code, or any
other information from your local environment.

### Opting out

Set the environment variable `TOKENSAVER_NO_TELEMETRY=1` in your MCP server
configuration. For example in `%USERPROFILE%\.mcp.json` (Visual Studio) or
`%USERPROFILE%\.claude.json` (Claude Code):

```json
{
  "servers": {
    "tokensaver": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"],
      "env": {
        "TOKENSAVER_API_URL": "https://tokensavermcp.com",
        "TOKENSAVER_NO_TELEMETRY": "1"
      }
    }
  }
}
```

The local `%USERPROFILE%\.tokensaver\report.json` log is written regardless
of this setting — it is local only and never uploaded when opt-out is active.

