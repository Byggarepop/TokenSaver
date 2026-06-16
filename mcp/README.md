# TokenSaver.Mcp

<!-- mcp-name: io.github.Byggarepop/tokensaver -->

A **structural warm start** for AI coding agents in .NET. Instead of loading
whole files into your assistant, TokenSaver hands it a cheap map of your code —
every type and member as a signature, each tagged with its line range — so the
model reads only the slice it needs instead of slurping the file. Built on the
Roslyn compiler platform.

**Where it pays off:** outlining a file costs **70–95 % fewer tokens** than
reading it — up to **90 % on a large file**. The end-to-end win is biggest on
**smaller / cheaper models** (which over-read the most) and on **large
codebases**: on real tasks it trims a Haiku-class model's token use by ~8 %, and
the savings climb with file size. A top-tier model already reads tightly, so it
sees less benefit — the leaner the model, the more a warm start helps.

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

`OutlineCSharpFile` and `TraceDiRegistrations` are **C#/Razor-first**;
`MinifyFile` dispatches by extension to minifiers for every supported language.

<!-- BEGIN:generated:tools -->
### Single-file tools

- `OutlineCSharpFile(filePath)` — skeleton of a file: types and member signatures, no bodies. Best for navigation ("what's in this file?"). **C# / Razor only.**
- `MinifyFile(filePath)` — lossless minify of a whole file, auto-dispatched by extension. Calls the Roslyn minifier for C#/Razor (strips comments, `#region` directives, and whitespace; logic preserved verbatim); falls back to basic minification for other types.

### Cross-file traversal tools

These scan an entire project directory in one call — no need to know which file
to look in first. Both accept a directory path or `.csproj` file; `obj/` and
`bin/` are excluded automatically. **C# only.**

- `TraceDiRegistrations(projectPath, typeName)` — finds every Dependency-Injection registration referencing a type (interface or concrete) and returns a compact table: `file:line`, method, `ServiceType -> ImplType`, and keyed key. Answers "where is IFoo wired, and to what implementation?" — the question a constructor caller-trace can't, since DI-built types are never `new`-ed.
<!-- END:generated:tools -->

Each tool result starts with a token-comparison header. For `OutlineCSharpFile`
it has up to three lines:
```
// [Focused Emitter] Tokens without tool: 16,800 → with tool: 5,200 (69% saved)
// vs a targeted read of just the relevant code (7,400 tokens): 29% saved
// session: 6 calls · saved 38,400 · net 36,300 after 2,100 overhead
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

The **third line** is the running total for your whole session. (The `6 calls`
and `38,400` here are illustrative — they stand for several different calls on
different files across a session, not a figure derived from the single call in
the first two lines above.) It also accounts for one thing the first line
ignores: when the server is connected,
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
the second and later views the first line changes to `repeat view — whole-file
baseline already counted; adds N tokens`, and the session total does
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
| "Read one method body" | `OutlineCSharpFile` + a narrow `Read` of its `// L..` range | ~300 | **~95 %** |
| "Audit the whole file" | `MinifyFile` | 5,525 | 40 % |

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
  Loaded assets for MCP server 'tokensaver' with 3 tools, 0 prompts, and 0 resources.
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

---

## Environment variables reference

All variables are set in the `env` block of your MCP server config. The full
set of supported keys (all optional unless noted):

```json
{
  "servers": {
    "tokensaver": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["tool", "execute", "TokenSaver.Mcp", "--yes"],
      "env": {
        "TOKENSAVER_API_URL": "https://tokensavermcp.com",
        "TOKENSAVER_NO_TELEMETRY": "1",
        "TOKENSAVER_CLIENT_ID": "your-custom-client-id",
        "TOKENSAVER_ENABLE_MAP_PROJECT": "1",
        "TOKENSAVER_DISABLE_AUTOUPDATE": "1",
        "TOKENSAVER_UPDATE_INTERVAL_MINUTES": "360"
      }
    }
  }
}
```

| Variable | Accepted values | Default | Effect |
|---|---|---|---|
| `TOKENSAVER_API_URL` | URL string | *(none)* | Required for telemetry uploads and the community dashboard. Omit to run fully offline. |
| `TOKENSAVER_NO_TELEMETRY` | `"1"` (any non-empty, non-`"0"` value works) | *(unset)* | Disables telemetry uploads. Local `report.json` is still written. |
| `TOKENSAVER_CLIENT_ID` | any string | auto-generated UUID | Overrides the auto-generated anonymous client ID used in telemetry. |
| `TOKENSAVER_DISABLE_AUTOUPDATE` | `"1"` | *(unset — enabled)* | Turns off the background update check. Launches stay pinned to whatever version your config names. |
| `TOKENSAVER_UPDATE_INTERVAL_MINUTES` | non-negative integer | `"360"` | Minimum minutes between background update checks. `"0"` checks on every launch. |

