# RoslynLean.Mcp

An MCP server that exposes a Roslyn-based focused C# emitter to MCP clients.
Reduces tokens sent to LLMs by 50-70% on typical C# files without losing logic.

Works with:
- **Visual Studio 2026** (GitHub Copilot Chat)
- **Claude Code** (CLI)
- Any other MCP client that speaks stdio (VS Code Copilot, Claude Desktop, etc.)

## What the tools do

- `FocusMethod(filePath, methodName, depth=0, minify=false)` — emit the named
  method with full body plus signatures of referenced members. `depth=1`
  also includes private helper bodies. `minify=true` strips comments and
  collapses whitespace.
- `MinifyCSharpFile(filePath)` — lossless minify of a whole file. Strips
  comments and whitespace; logic preserved verbatim.
- `AliasCSharpFile(filePath)` — minify plus rename private symbols to short
  codes (`M1`, `P1`, `F1`...). Useful on files with very long private names.

Each tool result starts with a token-comparison header:
```
// [Focused Emitter] Tokens without tool: 7,083 → with tool: 3,133 (55% saved)
```

Every invocation also appends a JSON entry to
`%USERPROFILE%\token-saver-report.json` (consumed by the TokenSaverViewer
Blazor app) and emits a one-line summary to stderr (visible in your MCP
client's output channel). Older `0.1.x` builds wrote to
`%LOCALAPPDATA%\RoslynLeanMcp\invocations.log`; if you still see writes
there, the installed global tool is stale — repack and reinstall.

---

## Install the tool

```
dotnet tool install --global RoslynLean.Mcp
```

After install, `roslyn-lean-mcp` is on your PATH. Verify:

```
roslyn-lean-mcp print-instructions
```

That command prints the recommended instruction text — you'll need it below.

---

## Setup for Claude Code

Two one-time steps.

**1. Register the MCP server at user scope** so it's available in every project:

```
claude mcp add roslyn-lean roslyn-lean-mcp --scope user
```

Verify:
```
claude mcp get roslyn-lean
```
Should show `Scope: User config (available in all your projects)` and
`Status: ✓ Connected`.

**2. Add a global `CLAUDE.md`** so Claude reaches for the MCP tools instead of
the built-in `Read` for C# files. From PowerShell:

```powershell
roslyn-lean-mcp print-instructions | Out-File -Append -Encoding utf8 $HOME\.claude\CLAUDE.md
```

(From bash / cmd: `roslyn-lean-mcp print-instructions >> "%USERPROFILE%\.claude\CLAUDE.md"`.)

That's it. **Start a new Claude Code session** (the tool list is fixed at
session start — existing sessions won't see the new server) and Claude will
auto-invoke the tools on C# work.

### Verify it works

In a new Claude Code session, ask something like:
> Look at the `OnInitializedAsync` method in some C# file and explain it.

Then check `%USERPROFILE%\token-saver-report.json` — a new JSON entry means
the tool was invoked. (Or open the TokenSaverViewer Blazor app.)

---

## Setup for Visual Studio 2026 (GitHub Copilot Chat)

Three one-time steps. The third is unfortunately needed because VS 2026
Copilot does **not** honor the MCP `ServerInstructions` field (we tested
this), so the tool-selection guidance has to be shipped as a workspace file.

**1. Register the MCP server.** Create or edit `%USERPROFILE%\.mcp.json`:

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

**2. Drop the Copilot instructions into every repo where you want auto-invocation.**
From inside the repo:

```
roslyn-lean-mcp print-instructions > .github\copilot-instructions.md
```

VS Copilot reads `<workspace>\.github\copilot-instructions.md` and includes
it in the system prompt. Without this file, Copilot won't reliably pick the
MCP tools.

**3. Restart Visual Studio** so it loads the new server registration.

### Verify it works

- *View → Output*, channel = *GitHub Copilot*. On startup you should see:
  ```
  Successfully started MCP server 'roslyn-lean'
  Loaded assets for MCP server 'roslyn-lean' with 3 tools, 0 prompts, and 0 resources.
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
  (you'll see `Loaded cached state for MCP server 'roslyn-lean'...` in the
  output channel instead of `Successfully started MCP server...`). Easiest
  cache bust: rename the server in `.mcp.json` (e.g. `roslyn-lean` →
  `roslyn-lean-2`) and restart VS. Rename back afterward if you like.
- **Pick a supported model** in the Copilot Chat model dropdown. Some models
  aren't available on free / limited SKUs and will fail the chat entirely
  with an error like `The requested model is not supported`.

---

## Building the NuGet package from source

If you're modifying the server and need to publish an updated package.

**1. Bump the version.** In `RoslynLean.Mcp.csproj`:
```xml
<Version>0.1.0</Version>  <!-- increment -->
```

**2. Pack.** From the `mcp/` folder:
```
dotnet pack -c Release
```
Produces `bin\Release\RoslynLean.Mcp.<version>.nupkg`.

**3. Distribute** — pick one:

- **Local install for yourself** (no network):
  ```
  dotnet tool update --global --add-source .\bin\Release RoslynLean.Mcp
  ```
  (Use `install` instead of `update` the first time.)

- **Hand someone the .nupkg file:**
  ```
  dotnet tool install --global --add-source <folder-containing-nupkg> RoslynLean.Mcp
  ```

- **Push to NuGet.org** (requires an API key):
  ```
  dotnet nuget push bin\Release\RoslynLean.Mcp.<version>.nupkg --source https://api.nuget.org/v3/index.json --api-key <YOUR_KEY>
  ```
  Then anyone can install with `dotnet tool install --global RoslynLean.Mcp`.

- **Push to a private feed** (Azure DevOps, GitHub Packages):
  same `dotnet nuget push` command, swap the `--source` URL.

**4. After updating, restart the MCP client** (Claude Code session or VS) so
it picks up the new tool definitions. Server name keying may require a
cache-bust on VS — see the gotchas above.

---

## Uninstalling

```
dotnet tool uninstall --global RoslynLean.Mcp
claude mcp remove roslyn-lean -s user     # if you used Claude Code
```
For VS, delete the `roslyn-lean` block from `%USERPROFILE%\.mcp.json` and
delete `.github\copilot-instructions.md` from any repo where you don't
want the tool-selection guidance.
