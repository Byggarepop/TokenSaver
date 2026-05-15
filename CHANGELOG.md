# Changelog

All notable changes to TokenSaver.Mcp are documented here.

## [Unreleased]

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
