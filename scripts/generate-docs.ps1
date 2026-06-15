# generate-docs.ps1
# Reads docs/tools.json and regenerates the marked tool sections in:
#   mcp/README.md           -- prose bullet list ("What the tools do")
#   README.md               -- two tool tables ("What the tools do")
#   TokenSaverViewer/wwwroot/llms.txt -- tool bullet list
#
# Usage (from repo root):  .\scripts\generate-docs.ps1
# Or from anywhere:        .\scripts\generate-docs.ps1 -RepoRoot C:\path\to\TokenSaver

param([string]$RepoRoot = '')

$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { $RepoRoot = Split-Path $PSScriptRoot -Parent }

$data   = [System.IO.File]::ReadAllText("$RepoRoot\docs\tools.json") | ConvertFrom-Json
$single = $data.tools | Where-Object { $_.category -eq 'single_file' }
$xfile  = $data.tools | Where-Object { $_.category -eq 'traversal' }

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$dash      = [char]0x2014   # em-dash for use in hardcoded prose strings

function Splice-Section {
    param([string]$File, [string]$Marker, [string]$Body)
    $begin   = "<!-- BEGIN:generated:$Marker -->"
    $end     = "<!-- END:generated:$Marker -->"
    $pattern = [regex]::Escape($begin) + '[\s\S]*?' + [regex]::Escape($end)
    $replace = $begin + "`n" + $Body + "`n" + $end
    $text    = [System.IO.File]::ReadAllText($File)
    $updated = [regex]::Replace($text, $pattern, $replace)
    if ($updated -ne $text) {
        [System.IO.File]::WriteAllText($File, $updated, $utf8NoBom)
        Write-Host "  updated  $($File.Substring($RepoRoot.Length).TrimStart('\/'))"
    } else {
        Write-Host "  no-op    $($File.Substring($RepoRoot.Length).TrimStart('\/'))"
    }
}

# ===========================================================================
# mcp/README.md -- prose bullet list
# ===========================================================================

$sb = New-Object System.Text.StringBuilder

[void]$sb.AppendLine('### Single-file tools')
[void]$sb.AppendLine()
foreach ($t in $single) {
    $langNote = if ($t.lang -ne 'all supported types') { " **$($t.lang) only.**" } else { '' }
    [void]$sb.AppendLine("- ``$($t.signature)`` $dash $($t.mcp_readme_description)$langNote")
}
[void]$sb.AppendLine()
[void]$sb.AppendLine('### Cross-file traversal tools')
[void]$sb.AppendLine()
[void]$sb.AppendLine("These scan an entire project directory in one call $dash no need to know which file")
[void]$sb.AppendLine("to look in first. Both accept a directory path or ``.csproj`` file; ``obj/`` and")
[void]$sb.AppendLine("``bin/`` are excluded automatically. **C# only.**")
[void]$sb.AppendLine()
foreach ($t in $xfile) {
    $disabledNote = if ($t.disabled_by_default) { " Disabled by default $dash set ``$($t.enable_env_var)=1`` to enable." } else { '' }
    [void]$sb.AppendLine("- ``$($t.signature)`` $dash $($t.mcp_readme_description)$disabledNote")
}

Splice-Section "$RepoRoot\mcp\README.md" 'tools' $sb.ToString().TrimEnd()

# ===========================================================================
# README.md -- two markdown tables
# ===========================================================================

$sb = New-Object System.Text.StringBuilder

[void]$sb.AppendLine('### Single-file tools')
[void]$sb.AppendLine()
[void]$sb.AppendLine('| Tool | What it does | Reduction |')
[void]$sb.AppendLine('|---|---|---|')
foreach ($t in $single) {
    $reduction = if ($t.reduction) { $t.reduction } else { '' }
    [void]$sb.AppendLine("| ``$($t.name)`` | $($t.readme_summary) | $reduction |")
}
[void]$sb.AppendLine()
[void]$sb.AppendLine("\* Reductions are measured against reading the **whole file** $dash the real alternative when you'd otherwise load it. The **end-to-end** saving on a task is smaller, because a capable model already reads somewhat selectively; it is largest on smaller/cheaper models and large files (see *Token savings in practice* below).")
[void]$sb.AppendLine()
[void]$sb.AppendLine('### Cross-file traversal tools')
[void]$sb.AppendLine()
[void]$sb.AppendLine("These scan an entire project directory in one call $dash no need to know which file to look in first.")
[void]$sb.AppendLine()
[void]$sb.AppendLine('| Tool | What it does |')
[void]$sb.AppendLine('|---|---|')
foreach ($t in $xfile) {
    [void]$sb.AppendLine("| ``$($t.name)`` | $($t.readme_summary) |")
}
[void]$sb.AppendLine()
[void]$sb.AppendLine("Both accept a directory path or ``.csproj`` file $dash ``obj/`` and ``bin/`` are excluded automatically.")

Splice-Section "$RepoRoot\README.md" 'tools' $sb.ToString().TrimEnd()

# ===========================================================================
# llms.txt -- bullet list
# ===========================================================================

$sb = New-Object System.Text.StringBuilder

foreach ($t in $data.tools) {
    if ($t.llms_skip) { continue }
    $display = if ($t.llms_display) { $t.llms_display } else { "``$($t.name)``" }
    [void]$sb.AppendLine("- $display $dash $($t.llms_summary)")
}

Splice-Section "$RepoRoot\TokenSaverViewer\wwwroot\llms.txt" 'tools' $sb.ToString().TrimEnd()

# ===========================================================================
# Capabilities.razor -- tool-grid cards
# ===========================================================================

$sb = New-Object System.Text.StringBuilder

[void]$sb.AppendLine('<div class="tool-grid">')
foreach ($t in $data.tools) {
    if (-not $t.display_name) { continue }
    [void]$sb.AppendLine('        <div class="tool-card">')
    [void]$sb.AppendLine('            <div class="tool-name">'  + $t.display_name       + '</div>')
    [void]$sb.AppendLine('            <div class="tool-desc">'  + $t.capabilities_desc  + '</div>')
    [void]$sb.AppendLine('            <div class="tool-example">&ldquo;' + $t.example_prompt + '&rdquo;</div>')
    [void]$sb.AppendLine('            <div class="tool-savings">' + $t.capabilities_savings + '</div>')
    [void]$sb.AppendLine('        </div>')
}
[void]$sb.Append('    </div>')

Splice-Section "$RepoRoot\TokenSaverViewer\Components\Pages\Capabilities.razor" 'tools' $sb.ToString()

Write-Host ''
Write-Host 'Done.'
Write-Host 'Reminder: mcp/Program.cs ServerInstructions and .github/copilot-instructions.md are hand-maintained -- update those separately for rule changes.'
