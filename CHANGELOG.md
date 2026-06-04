# Changelog

All notable changes to TokenSaver.Mcp are documented here.

## [Unreleased]

## [1.13.1] - 2026-06-04

### Changed
- **Telemetry no longer uploads the `Notes` field.** The field carried the mode
  string, which for `FocusMethod` / `FocusType` / `FocusCallers` (plus cache
  hits and the CLI path) embedded user code identifiers — method, type, and file
  names — from private codebases. The dashboard never displayed it and `ToolName`
  already records which tool ran, so the field is simply omitted from the upload
  payload, covering every current and future mode format. The local `report.json`
  still records `Notes` in full for the user's own stats. The MCP README's
  telemetry disclosure is updated to match.
- **Shipped instruction surfaces reworded to keep agents tool-first.** Both the
  server `ServerInstructions` and `copilot-instructions.md` previously granted a
  standing permission to `Read` a supported file for comprehension before
  editing, which made the tokensaver tools easy to bypass. Comprehension now
  always goes through a tokensaver tool first, with `Read` reduced to a narrow
  second-step edit-prep action (target lines ±5, never before the tool, never the
  whole file). Also added VB.NET and Markdown to the server instructions'
  supported-types list and corrected the stale `.md` "unsupported" examples in
  both files.

## [1.13.0] - 2026-06-01

### Added
- **Background self-update for dnx installs** — when launched via
  `dotnet tool execute`, the server keeps itself current without the
  first-query stall an unpinned launch hits after a new release. Registered
  config entries are pinned to an explicit `--version`, so every launch runs an
  already-cached package (instant) instead of resolving + downloading the latest
  on the critical path. After the host is serving, a throttled background task
  checks the feed, prefetches any newer version into the dnx cache, and **only
  then** re-pins the config — the new version is always on disk before any
  launch points at it, so the upgrade applies on the next launch with no stall.
  Adds the `print-version` and `self-update` (manual "update now") commands and
  the `TOKENSAVER_DISABLE_AUTOUPDATE` / `TOKENSAVER_UPDATE_INTERVAL_MINUTES`
  environment knobs.

### Changed
- **All registration entries now use the `dotnet tool execute` (dnx) model**,
  including Claude Desktop and Claude Code (previously the global
  `tokensaver-mcp` command). One launch model for every host — no separate
  `dotnet tool install -g` — and all hosts share the background auto-update.
  Existing global-command entries are left untouched by the updater; existing
  unpinned entries migrate themselves to the pinned form on first launch.

## [1.12.0] - 2026-06-01

### Added
- **X++ support** — `MinifyFile` now handles Dynamics 365 Finance & Operations
  X++ source files (`.xpp`). Basic tier: X++ shares C-style comment syntax
  (`//` and `/* */`) and `#`-prefixed macro directives, so it reuses the C/C++
  strip-and-collapse pass — comments stripped, whitespace collapsed, `#macro`
  directives preserved.

## [1.11.1] - 2026-05-31

### Changed
- **Removed the VS MCP cache delete** — `AutoUpdateRegistrations` no longer
  deletes Visual Studio's `*.cache` files on the first run after an upgrade.
  Deleting that file from inside the running server process raced with VS's
  active use of it, forcing a re-reconcile mid-query and stalling the first
  prompt after every version bump. VS re-queries `tools/list` live when it
  starts the server for a session, and `serverInfo.version` (set in
  `Program.cs`) signals invalidation, so the brute-force delete was redundant.
  If a future VS build is observed serving stale tool metadata after an
  upgrade, restore the delete with better timing.

## [1.11.0] - 2026-05-30

### Changed
- **Prefer Grep for existence checks** — all instruction files (server
  instructions, `copilot-instructions.md`, `CLAUDE.md`, and the
  `trace_callers` tool description) now include an explicit exception rule:
  for "is X used?"-style questions, use `Grep` first and only escalate to
  `trace_callers` when caller context is needed. A widely-used method can
  cost 100K+ tokens with `trace_callers`.
- **Startup file log** — the MCP server writes a startup log file to help
  diagnose VS MCP activation failures when the server silently does not
  start.

## [1.10.0] - 2026-05-30

### Changed
- **Net savings on first tool call** — the first tool call in a session now
  deducts the fixed MCP overhead (~3,300 tokens: server instructions + all tool
  descriptions) from reported savings. The header is labelled
  `[ToolName (Initial)]` and the "with tool" token count reflects the true net
  cost. Subsequent calls in the same session report full gross savings as before.
- Telemetry records the adjusted cost and the `(Initial)` tool name so the
  viewer can distinguish first-call entries.
- `tokensaver-mcp print-overhead` prints a breakdown of instructions vs schema
  token counts.
- Four new tests verify the label, the math, and that subsequent calls are
  unaffected (120/120 passing).

### Documentation
- Capabilities page on [tokensavermcp.com](https://tokensavermcp.com) now notes
  the ~3,300 token first-call overhead so users understand when a session breaks
  even.
- Token savings since tagline on the viewer now shows months in addition to days.

## [1.9.0] - 2026-05-27

### Added
- **Markdown emitter** — `MinifyFile` now handles `.md` and `.markdown` files:
  strips HTML comments and collapses blank runs, with no other content altered.

### Changed
- `focus_callers` tool description updated to clarify it is for **discovery
  only**. Once callers are identified, switch to `focus_multiple_methods` or a
  targeted `Read` for the actual edit — calling `focus_callers` again after the
  callers are already known wastes tokens.
- Copilot instructions (`copilot-instructions.md`) updated with the same
  `focus_callers` guidance.

### Documentation
- Install sections in `README.md` now point to `tokensavermcp.com/install`.
- VS / VS Code troubleshooting moved inline into the install cards for faster
  self-service.
- VS install configuration fixed and install docs consolidated (#43).

## [1.8.0] - 2026-05-24

### Fixed
- `FocusType` incorrectly classified all interface members as private and hid
  default interface implementation bodies. `IsPrivate` now exempts interface
  members (implicitly public) and only marks a member private when it carries an
  explicit `private` keyword.
- Leading-space artifact in signatures of interface methods and other
  implicit-access members: `ToSignature` now uses a `Prefix()` helper that only
  appends a trailing space when modifiers are non-empty.
- Three tests added covering both fixed behaviours (112/112 passing).

### Changed
- Token counts in every tool result header now use the **tiktoken cl100k_base**
  BPE tokenizer (via `Microsoft.ML.Tokenizers`) instead of the previous
  char÷4 heuristic. Before/after numbers and savings percentages now reflect
  real token counts. The vocabulary is embedded — no network download required.
- `Microsoft.Bcl.Memory` pinned to 10.0.8 to override a vulnerable 9.0.4
  transitive dependency introduced by `Microsoft.ML.Tokenizers`.
- `tests/TEST_REPORT.md` untracked (already in `.gitignore`; was previously
  committed).
- Edit-prep guidance in all three instruction files (`copilot-instructions.md`,
  `mcp/Program.cs`, `CLAUDE.md`) clarified: use a targeted partial `Read` of
  only the lines around the insertion point rather than a full file read.

### Documentation
- `README.md` — new **Prerequisites** section promoting the .NET 10 SDK
  requirement before the Install section; removes the easy-to-miss footnote.
- `README.md` — new **Claude Code** install subsection with ready-to-paste
  `claude mcp add` command.
- `benchmark-case-study.md` — four case studies covering comprehension, single-
  edit, and multi-edit tasks; all token counts taken from actual tool header
  output. Case Study 4 demonstrates 83% token reduction on a large file using
  `FocusMultipleMethods` + `Grep` across three edits.

## [1.7.2] - 2026-05-22

### Fixed
- Add `runtimeHint: dnx` and `runtimeArguments: --yes` to `server.json` so
  Visual Studio and other clients can install the server directly from the
  MCP Registry without manual configuration.

## [1.7.1] - 2026-05-22

### Fixed
- Add `mcp-name` ownership verification comment to package README, required
  for MCP Registry (`registry.modelcontextprotocol.io`) submission.

## [1.7.0] - 2026-05-20

### Added
- **Telemetry opt-out** — set `TOKENSAVER_NO_TELEMETRY=1` in the MCP server's
  `env` block to disable usage uploads entirely. The local
  `%USERPROFILE%\.tokensaver\report.json` log is still written; only the
  remote upload is skipped. Setting the variable to `0` re-enables uploads.

### Documentation
- `mcp/README.md` — new **Telemetry** section listing every field that is
  uploaded on each invocation (`ToolName`, `Language`, `TokensWithoutTool`,
  `TokensWithTool`, `Notes` / method name, anonymous `ClientId`) and
  explicitly stating what is never sent (file paths, file contents, source
  code). Includes opt-out instructions with a ready-to-paste JSON snippet.
- `mcp/README.md` — Install, Upgrading, and Uninstalling consolidated into a
  single **Install, upgrade & uninstall** section with three subsections for
  cleaner navigation.

## [1.6.0] - 2026-05-20

### Added
- **Server-side EmissionCache** — repeated calls to `FocusMethod`,
  `FocusMultipleMethods`, `FocusType`, or `FocusCallers` for the same method in
  an unchanged file now skip the Roslyn re-parse entirely. The full output is
  always returned (no dependency on prior context window state); cache hits are
  marked `[re-parse skipped]` in the tool result header. The cache is
  invalidated automatically when the file's last-write timestamp changes.
- 4 new tests: `Cache_MissOnFirstCall`, `Cache_HitOnSecondCall`,
  `Cache_InvalidatedAfterFileChange`, and an end-to-end
  `McpTool_SecondCallReturnsReparseSkipped` that calls `FocusedEmitterTools`
  directly.

### Changed
- Log prefix corrected from `[roslyn-lean]` to `[tokensaver]` in all MCP
  server stderr output.

### Documentation
- `mcp/README.md` — new **Upgrading** section covering the `dotnet tool update`
  command, the requirement to close all MCP clients (VS, Claude) before
  upgrading, and a detailed explanation of Visual Studio's lazy-loading
  behaviour (cached metadata at startup, server process started on first prompt,
  cache-bust rename trick).
- Root `README.md` — brief upgrade note added alongside the install command.

## [1.5.0] - 2026-05-19

### Fixed
- Visual Studio showing a stale tool count (e.g. 8 tools instead of 10) after
  upgrading the NuGet package
- `serverInfo.version` is now set in the MCP `initialize` handshake so Visual
  Studio detects a version change and invalidates its cached tool metadata
- `AutoUpdateRegistrations` now clears `*.cache` files under
  `%LOCALAPPDATA%\Microsoft\VisualStudio\Copilot\McpServers\` on the first
  startup after each version upgrade; a `[tokensaver] cleared VS MCP cache`
  message is written to stderr for each deleted file

## [1.4.0] - 2026-05-19

### Added
- `TraceCallers(projectPath, methodName)` — project-wide version of `FocusCallers`.
  Scans every `.cs` file in a project directory and returns focused views of all
  methods that call the named method, grouped by file. Accepts a directory path
  or `.csproj` file; `obj/` and `bin/` are excluded automatically.
- `TraceImplementors(projectPath, interfaceName)` — finds every type that
  implements or extends a named interface or base type across the project and
  returns a focused type view for each. Answers "what implements IFoo?" in one call.
- `ProjectTraversal` — internal class powering the two new tools; uses
  syntax-tree scanning (no full compilation required) consistent with `FocusCallers`
- 5 new tests covering caller-file detection, implementor discovery, empty results,
  and `.csproj` path input
- All instruction files updated to document 10 tools (MCP server instructions,
  `copilot-instructions.md`, `CLAUDE.md`, NuGet README, GitHub README)
- GitHub README rewritten as a proper project overview (previously contained a
  stale TokenStats design document)

## [1.3.0] - 2026-05-18

### Added
- VB.NET (`.vb`) support via Roslyn: `MinifyFile` strips `'` and `REM` comments
  and collapses blank runs
- `FocusMethod`, `FocusMultipleMethods`, `OutlineCSharpFile`, `FocusType`, and
  `FocusCallers` now accept `.vb` files in addition to `.cs` / `.razor` — full
  outline, focused method, type focus, and caller-finding on VB.NET source
- CI workflow: test suite runs automatically on every pull request

### Changed
- Roslyn packages aligned to 5.3.0 across all projects (was 4.11.0 in the MCP
  and test projects)

## [1.2.0] - 2026-05-18

### Added
- `register` now writes `TOKENSAVER_API_URL` into the `env` block for all
  targets (Claude Desktop, Claude Code, VS Code, Visual Studio) so usage
  statistics are reported to [tokensavermcp.com](https://tokensavermcp.com)
- Auto-update on startup: if an existing registration has a stale or missing
  `TOKENSAVER_API_URL`, it is silently corrected in-place without requiring
  a full re-register; a sentinel file prevents re-running on every launch
- Visual Studio 2026 GitHub Copilot Chat confirmed working in both Ask and
  Agent mode (inline completions bypass MCP by design)
- README link to tokensavermcp.com — live dashboard showing collective token
  savings and the environmental impact of the community's usage

## [1.1.0] - 2026-05-15

### Added
- `FocusType` — focused view of a named C# type: non-private members with
  full bodies, private members as signatures only; sits between `OutlineCSharpFile`
  and `MinifyCSharpFile` in detail level
- `FocusCallers` — finds every method in a file that calls a given target and
  returns them as a focused multi-method view; answers "what calls X?" without
  loading the whole file

### Changed
- `depth=1` on `FocusMethod`, `FocusMultipleMethods`, and `FocusCallers` now
  includes private **property** bodies in addition to private method bodies

### Fixed
- Improved XML doc summaries on all `Emit*` methods with concrete examples

## [1.0.0] - 2026-05-15

First stable release. Evolved from the alpha series (alpha1–alpha5) with
documentation and tool-description polish before the stable cut.

### Added
- `FocusMethod` — emit a named C# method with full body; `depth=1` includes
  private helper bodies; `minify=true` strips comments
- `FocusMultipleMethods` — focus N methods in one Roslyn parse (deduplicated
  shared signatures, fewer round-trips than N separate calls)
- `OutlineCSharpFile` — signatures-only view of a C# file, 70–95 % token
  reduction with no body content
- `MinifyFile` — auto-dispatch minifier for all supported extensions:
  C#, Razor, JS/TS, Python, HTML, CSS/SCSS/LESS, JSON/JSONC, YAML,
  XML/.NET project files, C, C++
- `AliasCSharpFile` — private symbol renaming to short codes for files
  dominated by long internal names
- `MinifyCSharpFile` — kept for back-compat; equivalent to `MinifyFile` on `.cs`
- Token-savings header on every tool result
  (`// Tokens without tool: X  →  with tool: Y  (Z% saved)`)
- `#reference` / Active Document warning in tool descriptions so AI assistants
  know not to pass a stale file path

### Fixed
- `FocusMethod` no longer drops the last helper when `depth=1`
- `RazorPreprocessor` no longer drops the first `@code` block when braces
  appear in strings or comments
- Tool result never returns more tokens than the original file
