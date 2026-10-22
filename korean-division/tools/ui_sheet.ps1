# ui_sheet.ps1 - T38g: capture every UI screen at 3 resolutions and build contact sheets.
# Usage: powershell -ExecutionPolicy Bypass -File tools/ui_sheet.ps1 [-Exe <KoreanDivision.exe>] [-OutDir docs/art_review]
#   Windows are launched via tools/agent/run_game.ps1 (secondary monitor if present). Output:
#   <OutDir>/ui_sheet_<W>x<H>.png (one sheet per resolution) + ui_sheet_all.png (overview), raw shots in a temp dir.
# Failure policy: a screen whose process exits non-zero / writes no PNG is listed as FAIL (crash point = screen + res) and the script exits 3.
param([string]$Exe = "$PSScriptRoot\..\bin\t38g\KoreanDivision.exe",
      [string]$OutDir = "$PSScriptRoot\..\docs\art_review",
      [string[]]$Sizes = @('1024x640', '1280x720', '1920x1080'))
$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path -LiteralPath $Exe).Path
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path
$raw = Join-Path ([System.IO.Path]::GetTempPath()) ("kd_ui_sheet_" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $raw | Out-Null
$runner = Join-Path $PSScriptRoot 'agent\run_game.ps1'
# screen name -> extra args. menu pages use --menu-page/--menu-shot, in-game screens use --shot.
$menuPages = @('main', 'setup', 'spectate', 'replays', 'battle', 'load', 'settings', 'keys', 'keys-listen', 'keys-conflict', 'codex', 'missions', 'briefing', 'campaign')
$gameShots = [ordered]@{
    'hud-idle'     = @('--map', 'corners4', '--ai', 'standard', '--shot-frame', '120')
    'hud-selected' = @('--map', 'corners4', '--ai', 'standard', '--shot-frame', '120', '--exec', 'select infantry')
    'hud-combat'   = @('--battle', 'infantry:40', '--vs', 'infantry:40', '--battle-map', 'flat', '--shot-frame', '240', '--exec', 'select infantry')
}
$fails = @(); $manifest = @()
foreach ($sz in $Sizes) {
    foreach ($pg in $menuPages) {
        $png = Join-Path $raw "$sz`__$pg.png"
        & powershell -ExecutionPolicy Bypass -File $runner $Exe --size $sz --menu-page $pg --menu-shot $png --mute | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $png)) { $fails += "$pg @ $sz (exit $LASTEXITCODE)" } else { $manifest += "$sz|$pg|$png" }
    }
    foreach ($name in $gameShots.Keys) {
        $png = Join-Path $raw "$sz`__$name.png"
        $env:KD_SHOT_NOINPUT = '1'
        & powershell -ExecutionPolicy Bypass -File $runner $Exe --size $sz --no-replay --mute --shot $png @($gameShots[$name]) | Out-Null
        Remove-Item Env:KD_SHOT_NOINPUT -ErrorAction SilentlyContinue
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $png)) { $fails += "$name @ $sz (exit $LASTEXITCODE)" } else { $manifest += "$sz|$name|$png" }
    }
}
$manifest | Set-Content -Encoding utf8 (Join-Path $raw 'manifest.txt')
python -I (Join-Path $PSScriptRoot 'ui_sheet.py') (Join-Path $raw 'manifest.txt') $OutDir
if ($fails.Count -gt 0) { Write-Host "[ui_sheet] FAIL: $($fails -join '; ')"; exit 3 }
Write-Host "[ui_sheet] OK: $($manifest.Count) shots -> $OutDir (raw: $raw)"
