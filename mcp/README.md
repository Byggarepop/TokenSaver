# TokenSaver.Mcp

An MCP server built for **.NET developers** — it gives your AI assistant a
token-efficient view of C#, Razor, and .NET project files using the Roslyn
compiler platform. Typical reduction: **50–95 %** on C# files with no loss
of logic.

Works with:
- **Visual Studio 2026** (GitHub Copilot Chat)
- **Claude Code** (CLI)
- Any other MCP client that speaks stdio (VS Code Copilot, Claude Desktop, etc.)

> **Language support tiers**
>
> | Tier | Languages | Status |
> |---|---|---|
> | **Primary** | C# (`.cs`), Razor (`.razor`), .NET project files (`.csproj`, `.props`, `.config`, `.xml`) | Fully supported, actively tested |
> | **Basic** | JavaScript, TypeScript, Python, HTML, CSS/SCSS/LESS, JSON/JSONC, YAML | Comment-strip + whitespace collapse only — not actively tested, results may vary |
>
> If you work exclusively in .NET, the basic-tier languages are a bonus, not
> a selling point.

## What the tools do

All four tools are **C#/Razor-first**. `MinifyFile` also dispatches to the
basic-tier minifiers for other extensions.

- `FocusMethod(filePath, methodName, depth=0, minify=false)` — emit the named
  method with full body plus signatures of referenced members. `depth=1`
  also includes private helper bodies. `minify=true` strips comments and
  collapses whitespace. **C# / Razor only.**
- `FocusMultipleMethods(filePath, methodNames, depth=0, minify=false)` — same
  as `FocusMethod` but focuses on multiple methods in one parse pass.
  **C# / Razor only.**
- `OutlineCSharpFile(filePath)` — skeleton of a file: types and member
  signatures, no bodies. Best for navigation ("what's in this file?").
  **C# / Razor only.**
- `MinifyCSharpFile(filePath)` — lossless minify of a whole C# file. Strips
  comments and whitespace; logic preserved verbatim. **C# / Razor only.**
- `MinifyFile(filePath)` — auto-dispatch by extension. Calls the Roslyn
  minifier for C#/Razor; falls back to basic minification for other types.
- `AliasCSharpFile(filePath)` — minify plus rename private symbols to short
  codes (`M1`, `P1`, `F1`...). Useful on files with very long private names.
  **C# / Razor only.**

Each tool result starts with a token-comparison header:
```
// [Focused Emitter] Tokens without tool: 7,083 → with tool: 3,133 (55% saved)
```

Every invocation also appends a JSON entry to
`%USERPROFILE%\token-saver-report.json` (consumed by the TokenSaverViewer
Blazor app) and emits a one-line summary to stderr (visible in your MCP
client's output channel). Older `0.1.x` builds wrote to
`%LOCALAPPDATA%\TokenSaverMcp\invocations.log`; if you still see writes
there, the installed global tool is stale — repack and reinstall.

---

## Install and register (zero-config)

```
dotnet tool install --global TokenSaver.Mcp
tokensaver-mcp register
```

`register` detects your environment and writes the server entry into:
- `%APPDATA%\Claude\claude_desktop_config.json` — Claude Desktop
- `%USERPROFILE%\.mcp.json` — Visual Studio 2026 (global)

It merges safely — existing entries from other MCP servers are left untouched.
Restart your MCP host after running it.

**Flags:**
- `--claude-desktop` / `--vs` — register only one target instead of both
- `--local` — write a solution-local `mcp.json` in the current directory
  instead of the global VS config (useful when you want per-repo opt-in)

After install, `tokensaver-mcp` is on your PATH. You can also verify the
instruction text the server advertises to AI clients:

```
tokensaver-mcp print-instructions
```

---

## Manual setup for Claude Code

Two one-time steps (skip if you used `register` above).

**1. Register the MCP server at user scope** so it's available in every project:

```
claude mcp add tokensaver tokensaver-mcp --scope user
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

Then check `%USERPROFILE%\token-saver-report.json` — a new JSON entry means
the tool was invoked. (Or open the TokenSaverViewer Blazor app.)

---

## Manual setup for Visual Studio 2026 (GitHub Copilot Chat)

Three one-time steps (skip steps 1–2 if you used `register` above). Step 3
is unfortunately always needed because VS 2026 Copilot does **not** honor the
MCP `ServerInstructions` field (we tested this), so the tool-selection
guidance has to be shipped as a workspace file.

**1. Register the MCP server.** Create or edit `%USERPROFILE%\.mcp.json`:

```json
{
  "servers": {
    "tokensaver": {
      "type": "stdio",
      "command": "tokensaver-mcp"
    }
  }
}
```

**2. Drop the Copilot instructions into every repo where you want auto-invocation.**
From inside the repo:

```
tokensaver-mcp print-instructions > .github\copilot-instructions.md
```

VS Copilot reads `<workspace>\.github\copilot-instructions.md` and includes
it in the system prompt. Without this file, Copilot won't reliably pick the
MCP tools.

**3. Restart Visual Studio** so it loads the new server registration.

### Verify it works

- *View → Output*, channel = *GitHub Copilot*. On startup you should see:
  ```
  Successfully started MCP server 'tokensaver'
  Loaded assets for MCP server 'tokensaver' with 6 tools, 0 prompts, and 0 resources.
  ```
- Send a normal prompt in Copilot Chat (no `#` reference):
  > Look at the `OnInitializedAsync` method in `C:\path\to\Foo.cs` and explain it.
- Check `%USERPROFILE%\token-saver-report.json` for a new entry, or open the TokenSaverViewer.

### VS-specific gotchas

- **Don't use `#filename.cs` references** when you want token reduction.
  `#` is a VS feature that inlines the *entire* file into the prompt before
  Copilot ever sees your message — our tool can't intercept that. Use plain
  text paths instead.
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

## Uninstalling

```
dotnet tool uninstall --global TokenSaver.Mcp
claude mcp remove tokensaver -s user     # if you used Claude Code
```
For VS, delete the `tokensaver` block from `%USERPROFILE%\.mcp.json` and
delete `.github\copilot-instructions.md` from any repo where you don't
want the tool-selection guidance.
