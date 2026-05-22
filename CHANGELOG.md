# Changelog

All notable changes to TokenSaver.Mcp are documented here.

## [Unreleased]

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
