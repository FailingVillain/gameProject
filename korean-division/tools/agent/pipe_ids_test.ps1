# pipe_ids_test.ps1 - tests for card_id.ps1 / roadmap_check.ps1 on a throw-away git repo, plus the real-repo acceptance check.
#   pipe_ids_test.ps1 [-NoReal]     (PowerShell 5.1; exit 0 = all PASS)
param([switch]$NoReal)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$here = $PSScriptRoot
$card = Join-Path $here 'card_id.ps1'; $rc = Join-Path $here 'roadmap_check.ps1'
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('pipe_ids_' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$fail = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Host "PASS $name" } else { Write-Host "FAIL $name $detail"; $script:fail++ }
}
function Run-Ps([string[]]$a) { $ErrorActionPreference = 'Continue'; $o = & powershell -NoProfile -ExecutionPolicy Bypass -File @a 2>&1 | ForEach-Object { "$_" }; return [pscustomobject]@{ Out = ($o -join "`n"); Code = $LASTEXITCODE } }
function G([string[]]$a) { $ErrorActionPreference = 'Continue'; & git -C $tmp -c user.name=t -c user.email=t@t @a 2>&1 | Out-Null }
function W([string]$rel, [string]$text) {
    $p = Join-Path $tmp $rel; New-Item -ItemType Directory -Force -Path (Split-Path -Parent $p) | Out-Null
    [System.IO.File]::WriteAllText($p, $text, (New-Object System.Text.UTF8Encoding($false)))
}
try {
    New-Item -ItemType Directory -Path $tmp | Out-Null
    & git -C $tmp init -q -b main 2>&1 | Out-Null
    W 'ops/QUEUE.md' "| id | t |`n|---|---|`n| T04 | queue only card | x |`n"
    W 'korean-division/tasks/T01-alpha.md' 'a'; W 'korean-division/tasks/T02-beta.md' 'b'
    W 'korean-division/docs/ROADMAP_SHORT.md' "- 날씨 시스템 계획`n"
    W 'korean-division/src/Render/WeatherFx.cs' "// 날씨 효과 weather overlay`nclass WeatherFx {}`n"
    G @('add', '-A'); G @('commit', '-q', '-m', 'init')
    G @('checkout', '-q', '-b', 'spec/x'); W 'korean-division/tasks/T03-gamma.md' 'g'; W 'korean-division/tasks/T05a-foo.md' 'f'
    G @('add', '-A'); G @('commit', '-q', '-m', 'x')
    G @('checkout', '-q', '-b', 'spec/y', 'main'); W 'korean-division/tasks/T05a-bar.md' 'b'
    G @('add', '-A'); G @('commit', '-q', '-m', 'y'); G @('checkout', '-q', 'main')

    # --- card_id ---
    $r = Run-Ps @($card, '-Id', 'T03', '-Title', 'dup', '-Top', $tmp)
    Check 'reserve used id (in other ref) rejected' ($r.Code -eq 2 -and $r.Out -match 'REJECT') $r.Out
    $r = Run-Ps @($card, '-Id', 'T04', '-Title', 'dup', '-Top', $tmp)
    Check 'reserve id used only in QUEUE rejected' ($r.Code -eq 2) $r.Out
    $r = Run-Ps @($card, '-Check', 'T05a', '-Top', $tmp)
    Check '-Check conflicting id -> exit 1' ($r.Code -eq 1 -and $r.Out -match 'CONFLICT') $r.Out
    $r = Run-Ps @($card, '-Check', 'T01', '-Top', $tmp)
    Check '-Check single-owner id -> exit 3' ($r.Code -eq 3) $r.Out
    $r = Run-Ps @($card, '-Check', 'T99', '-Top', $tmp)
    Check '-Check free id -> exit 0' ($r.Code -eq 0 -and $r.Out -match 'FREE') $r.Out
    $r = Run-Ps @($card, '-Prefix', 'T', '-Count', '2', '-Title', 'two cards', '-Top', $tmp)
    Check 'reserve next numbers after max (T06 T07)' ($r.Code -eq 0 -and $r.Out -match 'T06' -and $r.Out -match 'T07') $r.Out
    $r = Run-Ps @($card, '-Prefix', 'T', '-Count', '3', '-Series', '-Title', 'series', '-Top', $tmp)
    Check 'series gives one number with letters (T08a-c)' ($r.Code -eq 0 -and $r.Out -match 'T08a' -and $r.Out -match 'T08c') $r.Out
    $r = Run-Ps @($card, '-Id', 'T06', '-Title', 'again', '-Top', $tmp)
    Check 'reserved id cannot be reserved twice' ($r.Code -eq 2) $r.Out
    # concurrency: 6 processes at once
    $procs = 1..6 | ForEach-Object {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'powershell'; $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$card`" -Prefix T -Count 1 -Title par$_ -Top `"$tmp`""
        $psi.UseShellExecute = $false; $psi.RedirectStandardOutput = $true; $psi.CreateNoWindow = $true
        [System.Diagnostics.Process]::Start($psi)
    }
    $ids = @($procs | ForEach-Object { $_.StandardOutput.ReadToEnd().Trim() })
    $procs | ForEach-Object { $_.WaitForExit() }
    $uniq = @($ids | Where-Object { $_ -match '^T\d+$' } | Select-Object -Unique)
    Check 'concurrent callers get 6 distinct ids' ($uniq.Count -eq 6) ($ids -join ',')
    $reg = Get-Content (Join-Path $tmp 'ops/CARD_REGISTRY.md') -Encoding UTF8
    Check 'registry has a line per reserved id, no duplicates' ((@($reg | Where-Object { $_ -match '^- T\d+ ' } | ForEach-Object { ($_ -split ' ')[1] }) | Group-Object | Where-Object { $_.Count -gt 1 }).Count -eq 0)
    Check 'registry marks the conflicting id' (($reg -join "`n") -match '- T05a .*\[충돌\]')

    # --- roadmap_check ---
    $r = Run-Ps @($rc, '-Keywords', '날씨,weather', '-Top', $tmp, '-NoRefs')
    Check 'roadmap_check hit: exit 1 + code hit WeatherFx + roadmap hit' ($r.Code -eq 1 -and $r.Out -match 'WeatherFx' -and $r.Out -match 'roadmap \. ' -and $r.Out -match '구현 존재 가능') $r.Out
    $r = Run-Ps @($rc, '-Keywords', 'zzqqxx', '-Top', $tmp)
    Check 'roadmap_check no hit: exit 0 + conclusion' ($r.Code -eq 0 -and $r.Out -match '히트 없음') $r.Out
    $r = Run-Ps @($rc, '-Keywords', 'gamma', '-Top', $tmp)
    Check 'roadmap_check finds a card in another branch (refs source)' ($r.Code -eq 1 -and $r.Out -match 'spec/x' -and $r.Out -match '문서 계획만') $r.Out
    $r = Run-Ps @($rc, '-Keywords', 'gamma', '-Card', 'T03', '-Top', $tmp)
    Check '-Card excludes the card own file' ($r.Code -eq 0) $r.Out

    # --- real repo acceptance ---
    if (-not $NoReal) {
        $r = Run-Ps @($rc, '-Keywords', '날씨,weather')
        Check 'REAL: roadmap_check 날씨,weather -> exit 1, WeatherFx + Weather.cs code hits, <=40 lines' ($r.Code -eq 1 -and $r.Out -match 'WeatherFx' -and $r.Out -match 'Weather\.cs' -and $r.Out -match '구현 존재 가능' -and (($r.Out -split "`n").Count -le 40)) $r.Out
    }
} finally {
    if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
}
if ($fail -gt 0) { Write-Host "pipe_ids_test: $fail FAIL"; exit 1 }
Write-Host 'pipe_ids_test: ALL PASS'; exit 0
