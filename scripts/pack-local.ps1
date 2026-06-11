# Packs TokenSaver.Mcp into a local folder feed for testing without nuget.org.
#
# The package is always versioned 9.9.9-dev: it can never collide with a
# published version, so the pinned --version in .claude.json never serves a
# stale public package, and the .claude.json entry never needs updating when
# the real csproj version bumps.
#
# Pair with this mcpServers entry (--source replaces all NuGet sources, so
# startup never touches nuget.org):
#
#   "command": "dotnet",
#   "args": ["tool", "execute", "TokenSaver.Mcp",
#            "--version", "9.9.9-dev",
#            "--source", "<repo>\\nupkg\\local-feed",
#            "--yes"]
#
# After running this script, restart Claude Code (MCP servers load at session
# start) to pick up the new build.

$ErrorActionPreference = 'Stop'

$devVersion = '9.9.9-dev'
$repoRoot   = Split-Path -Parent $PSScriptRoot
$feedDir    = Join-Path $repoRoot 'nupkg\local-feed'
$csproj     = Join-Path $repoRoot 'mcp\TokenSaver.Mcp.csproj'

New-Item -ItemType Directory -Force $feedDir | Out-Null

Write-Host "Packing $csproj as $devVersion ..."
dotnet pack $csproj -c Release -p:Version=$devVersion -o $feedDir --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE" }

# dotnet tool execute caches the extracted package in the NuGet global
# packages folder; the same version is never re-fetched, so the previous
# dev build must be evicted for the new one to load.
$cacheDir = Join-Path $env:USERPROFILE ".nuget\packages\tokensaver.mcp\$devVersion"
if (Test-Path $cacheDir) {
    try {
        Remove-Item -Recurse -Force $cacheDir -ErrorAction Stop
        Write-Host "Cleared cached $devVersion from NuGet packages folder."
    } catch {
        Write-Warning "Could not clear $cacheDir - a running MCP server is likely locking it."
        Write-Warning "Close all Claude Code sessions using the dev server, then re-run this script."
        exit 1
    }
}

Write-Host "Done. Local feed: $feedDir"
Write-Host "Restart Claude Code to load the new build."
