# Smoke-tests the locally packed TokenSaver.Mcp (see pack-local.ps1) by
# driving it over stdio: initialize -> initialized -> tools/call minify_file.
# Verifies the dnx local-feed path end to end without restarting Claude Code.

$ErrorActionPreference = 'Stop'

$devVersion = '9.9.9-dev'
$repoRoot   = Split-Path -Parent $PSScriptRoot
$feedDir    = Join-Path $repoRoot 'nupkg\local-feed'
$sampleFile = Join-Path $repoRoot 'mcp\TokenSaver.Mcp.csproj'

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName               = 'dotnet'
$psi.Arguments              = "tool execute TokenSaver.Mcp --version $devVersion --source `"$feedDir`" --yes"
$psi.RedirectStandardInput  = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError  = $true
$psi.UseShellExecute        = $false
$psi.EnvironmentVariables['TOKENSAVER_UPDATE_INTERVAL_MINUTES'] = '0'

$proc = [System.Diagnostics.Process]::Start($psi)

$init = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}'
$initialized = '{"jsonrpc":"2.0","method":"notifications/initialized"}'
$call = '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"minify_file","arguments":{"filePath":"' + $sampleFile.Replace('\', '\\') + '"}}}'

$proc.StandardInput.WriteLine($init)
$proc.StandardInput.WriteLine($initialized)
$proc.StandardInput.WriteLine($call)
$proc.StandardInput.Flush()

# First run extracts the package; give the restore + handshake time to finish
# before closing stdin (closing it triggers shutdown that races the flush).
Start-Sleep 15
$proc.StandardInput.Close()

$stdout = $proc.StandardOutput.ReadToEnd()
$stderr = $proc.StandardError.ReadToEnd()
$proc.WaitForExit(10000) | Out-Null

$lines = $stdout -split "`n" | Where-Object { $_.Trim() }
$ok = $true
foreach ($id in 1, 2) {
    $resp = $lines | Where-Object { $_ -match "`"id`":$id" } | Select-Object -First 1
    if ($resp -and $resp -notmatch '"error"') {
        Write-Host "Response ${id}: OK"
    } else {
        Write-Host "Response ${id}: MISSING or error"
        $ok = $false
    }
}

if (-not $ok) {
    Write-Host '--- stdout ---'; Write-Host $stdout
    Write-Host '--- stderr ---'; Write-Host $stderr
    exit 1
}
Write-Host "Local MCP server at $devVersion responded correctly."
