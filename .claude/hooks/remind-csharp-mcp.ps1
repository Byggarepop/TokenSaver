$ErrorActionPreference = 'SilentlyContinue'

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    exit 0
}

if ($payload.tool_name -ne 'Read') { exit 0 }

$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }
if ($path -notmatch '\.(cs|razor\.cs)$') { exit 0 }

# Tiny files don't benefit from MCP — let those through silently.
if (Test-Path -LiteralPath $path) {
    $lineCount = (Get-Content -LiteralPath $path -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
    if ($lineCount -lt 50) { exit 0 }
}

$reminder = @"
You are about to Read a C# file ($path) with the built-in Read tool.

Prefer the roslyn-lean MCP server (registered for this project):
  - minify_c_sharp_file : lossless ~20-50% reduction for whole-file reads
  - focus_method        : when you need a specific method (use depth=1)
  - alias_c_sharp_file  : files dominated by long private symbol names

ONLY use Read directly when you need exact on-disk text for an Edit call,
or when the user explicitly asked for the raw file. Otherwise, cancel
this Read and call the appropriate MCP tool instead.
"@

$out = @{
    hookSpecificOutput = @{
        hookEventName     = 'PreToolUse'
        additionalContext = $reminder
    }
} | ConvertTo-Json -Depth 5 -Compress

Write-Output $out
exit 0
