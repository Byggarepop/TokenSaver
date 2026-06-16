# Changelog

All notable changes to TokenSaver.Mcp are documented here.

## [1.15.1] — 2026-06-16

### Fixed
- `OutlineCSharpFile` on `.razor` files now reports **correct `// L..` line ranges**.
  The Razor preprocessor previously rebuilt the `@code`/`@functions` C# into a fresh
  synthetic class, so every member's line number was offset by however far the `@code`
  block sat in the file — a narrow `Read` of a printed range landed on the wrong lines.
  Extraction is now line-aligned: each extracted line keeps its original position, so
  the ranges map straight back to the real `.razor` file. (Surfaced once the warm-start
  flow — outline then Read the line-range — became the primary path.)

## [1.15.0] — 2026-06-16

### Changed — stripped to a warm-start tool set
- Reduced the advertised MCP surface from 11 tools to **3**: `OutlineCSharpFile`
  (structural warm start), `MinifyFile` (whole-file, all languages), and
  `TraceDiRegistrations` (DI wiring — the one discovery grep can't do cleanly).
  The focus/trace family (`FocusMethod`, `FocusMultipleMethods`,
  `FocusMethodsAcrossFiles`, `FocusType`, `FocusCallers`, `TraceCallers`,
  `TraceImplementors`, `MapProject`) is no longer registered as an MCP tool (#99).
- Fixed per-session overhead dropped **4,134 → 1,415 tokens** (−66%):
  `ServerInstructions` rewritten for the 3-tool set, 8 tool schemas removed, and a
  legacy duplicate `focus_method` registration (`FocusedEmissionTool`) deleted.
  Rationale: a controlled A/B (outline-only vs full toolset vs no-MCP) showed the
  focus/trace tools added no measured token value on scoped tasks; the durable win
  is a cheap structural warm start, after which `Grep` + a narrow `Read` of the
  outline's line-ranges does the rest. On a real cheaper-model task this flips the
  session ledger from net-negative to net-positive.
- Removed the Markdown emitter.

### Documentation
- Reframed the README, NuGet readme, viewer pages, `llms.txt`, and registry
  description around the warm start: it curbs wasteful reading, with the biggest win
  on smaller/cheaper models (~8%) and large files. Dropped the "30–95% for everyone"
  framing — the win scales inversely with model capability (~1% top-tier, ~5–7%
  mid-tier, ~8% cheaper).

## [1.14.1] — 2026-06-11

### Improvements
- Fixed MCP context overhead trimmed: `ServerInstructions` compacted from 2,207 to 1,583 tokens with no rule removed — selection rules now precede the supported-types table so clients that truncate long instructions keep the behavior-critical part — and emitter result banners collapsed from 2–5 lines to a single line, with a shorter `session:` header line (#94)

### Fixes
- Background self-update no longer downgrades a config pinned to a newer version than the discovered latest (e.g. a local dev build) — `SetPinnedVersion` refuses to lower an existing pin (#96)
- Markdown minify banner note collapsed to a single line, matching the other emitters (#96)

## [1.14.0] — 2026-06-08

### New Tools
- `trace_di_registrations` — discover every `Add`/`TryAdd`/`AddKeyed` registration for a type across the project; detects generic, `typeof()`, and factory-lambda forms (#86)
- `map_project` — project-wide type-to-file map with kind and base list; opt-in via `TOKENSAVER_ENABLE_MAP_PROJECT=1` (#87, #89)

### Improvements
- Agentic-mode optimizations: parse cache avoids re-parsing the same syntax tree across consecutive tool calls (#87)
- `trace_di_registrations` now recognizes `TryAddKeyed*` registrations (#88)
- `docs/tools.json` is now the single source of truth for tool metadata; README and viewer tool grid are generated from it (#90)

### Fixes
- Removed duplicate tool cards in Capabilities.razor that caused a build failure (#91)

### Other
- Added MIT License
- Startup log lines now show both UTC and local time (#85)


## [1.13.7] - 2026-06-07

### Fixed
- **Telemetry uploads are now idempotent, ending duplicate dashboard rows.**
  The durable startup resend re-POSTs every pending row, and a client can spawn
  several MCP server processes at once, so the same logical event could be
  POSTed more than once — the payload carried no identifier and the dashboard
  inserted every POST as a new row. Each recorded event now carries a stable
  `EventId` (GUID) that is sent with the upload; the dashboard dedupes on it via
  a unique index plus an idempotent insert, so a re-send collapses to a no-op.
  Older clients that send no `EventId`, and older dashboards that ignore the
  field, keep working unchanged.

## [1.13.6] - 2026-06-06

### Added
- **Telemetry uploads are now durable.** The dashboard upload was fire-and-forget
  with no retry and an ignored HTTP status, so a transient failure or a process exit
  mid-flight silently dropped a row that was recorded locally. Each `report.json` row
  now carries an upload-tracking flag: a confirmed `2xx` — or a permanent `4xx`
  rejection that retrying can't fix — settles the row, while a transient failure
  (`5xx` / `429` / network) leaves it pending. On startup the server resends any
  still-pending row. Rows written before this change are left untouched (never
  resent), so there is no mass re-upload of history. Rows the dashboard would
  reject — chiefly an honest negative saving where a focused view costs more than
  the bare relevant-code baseline (`TokensWithTool > TokensWithoutTool`) — are
  skipped client-side and settled locally rather than sent for a guaranteed 400.

### Fixed
- **NOT FOUND rows no longer log a bogus 0% saving.** A focus miss returns a
  small members outline (plus the partial/inherited hint), not the whole file,
  yet telemetry logged `whole-file → whole-file` (0% saved). It now logs
  `whole-file → actual response`, crediting the real saving versus the model
  reading the whole file to discover the member isn't there. Applies to the
  NOT FOUND paths of `focus_method`, `focus_multiple_methods`, `focus_type`,
  and `focus_callers`.

## [1.13.5] - 2026-06-06

### Added
- **Focus misses now hint where to look.** When `focus_method` or
  `focus_multiple_methods` returns NOT FOUND and the file's type is `partial`,
  the response notes the member may be in a sibling file in the same
  namespace/folder; when the type has a base list, it notes the member may be
  inherited and points at the base type's file. Turns a bare NOT FOUND into an
  actionable next step instead of a dead end.

## [1.13.4] - 2026-06-05

### Fixed
- **Cache hits no longer overstate savings against the whole-file baseline.** A
  cached re-serve logged the raw whole-file token count as its baseline (e.g.
  8,201 → 246, 97%) while the original call recorded the conservative
  relevant-code baseline (303 → 246, 19%) — re-crediting a whole-file saving the
  session ledger and in-chat header never re-credit, and inflating the
  dashboard's aggregate totals on every repeat view. The cache now stores the
  same conservative baseline the first call logged, so a cache-hit row is
  identical to the original. Applies to `focus_method`, `focus_multiple_methods`,
  `focus_type`, and `focus_callers`.

### Changed
- **Cache-hit telemetry rows are tagged with the originating tool.** A re-served
  result logged a bare `Cache` tool name, hiding which tool produced it; rows are
  now labelled `<tool> Cache` (e.g. `Focused Emitter (multi) Cache`).

## [1.13.3] - 2026-06-05

### Added
- **Reports now record the TokenSaver version that produced them.** Each
  uploaded report carries an `McpVersion` field resolved from the running
  build, so the dashboard can attribute savings to a specific release and tell
  old clients from new ones. The viewer stores it in a new nullable column and
  surfaces it in the admin log. Existing databases are migrated in place by a
  guarded `ALTER TABLE ... ADD COLUMN` on startup — additive and idempotent, so
  rows ingested before this field keep a null version and no data is touched.

### Changed
- **Session savings are honest about repeat views of the same file.** The
  session ledger added each call's whole-file baseline every time, so viewing
  one file several ways (a different method, or outline-then-minify) credited the
  whole-file saving once per view — inflating a real ~50% into an apparent ~90%.
  The ledger now tracks which sources have already been counted and adds the
  whole-file baseline only on first sighting; later distinct views add only their
  own output. Repeat views drop the "% saved" headline and state the baseline was
  already counted, so per-call lines can no longer be summed into an overstated
  total. (Identical repeat calls were already served from cache without touching
  the ledger.)

### Fixed
- **A comma-containing `focus_method` name now auto-routes to
  `focus_multiple_methods`.** Passing `"A,B"` as `methodName` was treated as a
  single missing method, dumping the whole outline as a "not found" reply; it is
  now split and routed to the multi-method tool.

## [1.13.2] - 2026-06-05

### Added
- **Focused tools now report a second, "targeted-read" baseline.** The
  whole-file "Tokens without tool" figure assumes the alternative was reading
  the entire file, which is a best case for `FocusMethod` / `FocusMultipleMethods`
  / `FocusType` / `FocusCallers` — a careful reader could instead read just the
  relevant code. The emitter now exposes that relevant code (the focus members
  plus expanded helpers) as `FocusResult.RelevantSourceText`, and the header adds
  a line comparing the tool output against reading only that code. The real-world
  saving therefore sits between the two baselines. The line honestly reports
  "larger" when the focused view (which adds surrounding signatures) exceeds the
  bare relevant code, and is omitted for whole-file tools where it doesn't apply.

### Changed
- **Telemetry/dashboard now records the conservative baseline, never the best
  case.** The uploaded and locally-recorded `TokensWithoutTool` is the figure we
  compare against to compute saved tokens. For the focused tools it is now the
  relevant-code count (the focus member plus helpers), not the whole file, so the
  public dashboard never overstates savings — it reports the saving we're certain
  about rather than the best case. Whole-file tools (`Outline`, `Minify`) keep the
  whole-file baseline, which is their true alternative. No payload/schema change:
  the existing field simply carries the honest value (`FocusedEmitterTools.TelemetryBaseline`).
- **Token-savings reporting is now honest about per-session MCP overhead.** The
  previous scheme dumped the entire one-time overhead (server instructions + tool
  schemas) onto the first call as an `[ToolName (Initial)]` header, leaving every
  later call to ignore it. Overhead is a single per-session cost, so it no longer
  distorts any individual call: the per-call header is now overhead-free, and a
  second `// session:` line reports the cumulative `raw saved` and the `net` after
  subtracting the overhead exactly once. The net can read negative early on,
  which honestly signals the server hasn't yet paid for its context cost.
- **Telemetry records the raw measured token counts.** `LogInvocation` previously
  received the clamped and overhead-adjusted values, biasing the dashboard's
  aggregates optimistically. It now logs the unmodified before/after counts the
  tokenizer produced; the friendly clamping is applied only to the displayed
  header string.

### Fixed
- **Manual `self-update` now re-pins host configs regardless of the running
  version.** The command decided whether to re-pin by comparing the running
  process version against the latest feed version, acting only when latest was
  newer. Invoked via unpinned `dotnet tool execute TokenSaver.Mcp self-update`,
  dnx resolves and runs the *latest* package, so the running version always
  equalled latest by construction — the comparison was never "newer", the
  command logged "up to date", and the re-pin was skipped, leaving configs on
  the old version. The re-pin is now decoupled from the running version and
  applied unconditionally; pinning stays idempotent, so only stale configs are
  rewritten.

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
