# TokenSaver.Mcp

<!-- mcp-name: io.github.Byggarepop/tokensaver -->

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

Each tool result starts with a token-comparison header. For the focused tools
(`FocusMethod`, `FocusMultipleMethods`, `FocusType`, `FocusCallers`) it has
three lines:
```
// [Focused Emitter] Tokens without tool: 16,800 → with tool: 5,200 (69% saved)
// vs a targeted read of just the relevant code (7,400 tokens): 29% saved
// session: 6 calls · raw saved 38,400 · net of 2,100 one-time MCP overhead = 36,300
```

The **first line** is just this one call: how big the file was versus how big
the tool's answer was. The "without tool" number here is the size of the
**whole file** — that is, it assumes the AI would otherwise have read the entire
file. That's a fair comparison when the question genuinely needs the whole file
("what's in here?", "audit this file"), because reading all of it is exactly
what would have happened.

But for a question about a single method, a careful AI might not read the whole
file — it could search for the method and read just the part it needs. So the
**second line** gives you the other end of the scale: it measures against
reading *only the relevant code* (the method you asked about plus the small
helpers it depends on). The true saving lives somewhere between these two lines.
You might also see "larger" instead of "saved" here — that's honest too: for a
tiny method, the tool's answer can be bigger than the bare code because it also
includes the surrounding signatures the AI needs to make sense of it. (This line
appears only for the focused tools; for whole-file tools like `Outline` and
`Minify`, reading the whole file *is* the real alternative, so there's nothing
to compare against.)

The **third line** is the running total for your whole session, and it
accounts for one thing the first line ignores. When the server is connected,
it adds a fixed block of text to the AI's context — its instructions and the
list of tools. That block costs some tokens (here, about 2,100).

Strictly speaking that block sits in the AI's context on *every* turn, so it's
an ongoing cost, not a one-time one. But here's the saving grace: most AI
clients **cache** it. They store the block after the first turn and reuse it
almost for free instead of re-reading it every turn. So in practice it behaves
like a single startup cost — you mostly pay for it once and barely again after
that.

That's why the session line subtracts it **once**: it adds up everything you've
saved so far, then takes off that startup cost a single time. If the number is
negative, it just means you haven't saved enough yet to cover the startup cost —
keep using the tools and it turns positive.

One more honesty detail. If the AI looks at the **same file more than once** in a
session — say it focuses one method, then another, or outlines a file and later
minifies it — reading that file is only worth its whole-file cost *once*. So on
the second and later views the first line changes to `repeat view of this file
this session — whole-file baseline already counted`, and the session total does
**not** credit the whole-file saving again. Without this, viewing one file five
different ways would look like five separate big savings, which would overstate
the real benefit. The running total counts each file's baseline a single time and
only adds the extra output each later view brings into context.

The 2,100 shown is the *full* price of the block, before any caching. We show
the full figure because the server can't see whether or how your client caches.
So treat it as a worst case: caching only makes your real savings **better**
than the number on screen.

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

See **[tokensavermcp.com/install](https://tokensavermcp.com/install)** for one-click install buttons, per-client config snippets, the `register` command, upgrade/uninstall instructions, and troubleshooting.

---

## Automatic updates

When the server is launched with `dotnet tool execute` (the default for every
client), it keeps itself up to date **without** the slow first query an unpinned
launch hits right after a new release.

How it works:

- Registered config entries pin an explicit `--version`, so each launch runs an
  already-cached package and starts instantly — no "resolve latest + download"
  on the launch path.
- Once the server is serving, a throttled background task checks the NuGet feed
  for a newer version, downloads it into the dnx cache, and **only then** re-pins
  the `--version` in your config files. The new version is always on disk before
  anything points at it, so the upgrade applies on the next launch with no stall.
- Existing unpinned entries migrate to the pinned form automatically on the first
  launch of a version that supports this.

You normally don't need to touch any of this. Two environment variables tune it,
set in the `env` block of your MCP server config (same place as the telemetry
opt-out below):

| Variable | Effect |
|---|---|
| `TOKENSAVER_DISABLE_AUTOUPDATE=1` | Turns the background update check off. Launches stay pinned to whatever version your config names. |
| `TOKENSAVER_UPDATE_INTERVAL_MINUTES` | Minimum minutes between background checks (default `360`). `0` checks on every launch. |

To update on demand, run `dotnet tool execute TokenSaver.Mcp --yes -- self-update`.

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
| `TokensWithoutTool` | `9202` | The conservative baseline we compare against (see note below) |
| `TokensWithTool` | `1039` | Token count of the tool output |
| `ClientId` | `9202828d...` | Random UUID generated once and stored in `%USERPROFILE%\.tokensaver\token-saver-client-id`. Never tied to a name or email. |

The dashboard's saved-token figure is `TokensWithoutTool − TokensWithTool`, and we
deliberately pick the **conservative** baseline so it never overstates savings. For
whole-file tools (`Outline`, `Minify`) that baseline is the whole file, because
reading all of it is the real alternative. For the focused tools it is the *relevant
code only* (the method you asked about plus its helpers) — not the whole file — since
a careful reader might have read just that. In other words, the public number is the
saving we're certain about, not the best case.

**What is never sent:** method, type, or file names, file paths, file
contents, your source code, or any other information from your local
environment. (Earlier versions also uploaded a `Notes` mode string that
could contain a method name; it is no longer transmitted.)

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

