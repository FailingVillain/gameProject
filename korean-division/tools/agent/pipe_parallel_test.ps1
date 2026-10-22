# pipe_parallel_test.ps1 - tests for fv_claim / fv_assign / assemble_notes / queue_guard on a throw-away git repo.
#   pipe_parallel_test.ps1      (PowerShell 5.1; exit 0 = all PASS). ASCII-only source.
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$here = $PSScriptRoot
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('pipe_par_' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$kd = Join-Path $tmp 'korean-division'
$fail = 0
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Check([string]$name, [bool]$ok, [string]$detail = '') { if ($ok) { Write-Host "PASS $name" } else { Write-Host "FAIL $name $detail"; $script:fail++ } }
function Run-Ps([string[]]$a, [string]$cwd = $tmp) {
    $ErrorActionPreference = 'Continue'
    Push-Location $cwd
    try { $o = & powershell -NoProfile -ExecutionPolicy Bypass -File @a 2>&1 | ForEach-Object { "$_" } } finally { Pop-Location }
    return [pscustomobject]@{ Out = ($o -join "`n"); Code = $LASTEXITCODE }
}
function G([string[]]$a) { $ErrorActionPreference = 'Continue'; $o = & git -C $tmp -c user.name=t -c user.email=t@t -c core.autocrlf=false @a 2>&1; return (($o | ForEach-Object { "$_" }) -join "`n") }
function W([string]$rel, [string]$text) { $p = Join-Path $tmp $rel; New-Item -ItemType Directory -Force -Path (Split-Path -Parent $p) | Out-Null; [System.IO.File]::WriteAllText($p, $text, $utf8) }
function ReadF([string]$rel) { return [System.IO.File]::ReadAllText((Join-Path $tmp $rel), $utf8) }
function Snap([int]$fv) { return "class Snapshot {`n    public const int FormatVersion = $fv; // ${fv}: test`n}`n" }
function Rep([int]$rp) { return "class ReplayFile {`n    public const int FormatVersion = $rp;`n}`n" }
$SN = 'korean-division/src/Core/Snapshot/Snapshot.cs'; $RP = 'korean-division/src/Core/Commands/Replay.cs'
function Consts() { $a = [regex]::Match((ReadF $SN), 'FormatVersion = (\d+)').Groups[1].Value; $b = [regex]::Match((ReadF $RP), 'FormatVersion = (\d+)').Groups[1].Value; return "$a/$b" }
try {
    New-Item -ItemType Directory -Path $tmp | Out-Null
    G @('init', '-q', '-b', 'main') | Out-Null
    W $SN (Snap 66); W $RP (Rep 24); W 'ops/QUEUE.md' "q`n"; W 'ops/STATE.md' "s`n"
    W 'korean-division/docs/DECISIONS_PENDING.md' "# head`n- [old] keep me`n"; W 'korean-division/docs/ENGINE_NOTES.md' "# notes`n## old section`n"
    G @('add', '-A') | Out-Null; G @('commit', '-q', '-m', 'base66') | Out-Null
    # branches that all use the temporary FV 67 (= base 66 + 1); claims written with fv_claim.ps1
    $spec = @(@('kd/tcb', 1, 0), @('kd/tcc', 1, 0), @('kd/m1', 1, 1), @('kd/g3', 1, 1), @('kd/plain', 0, 0))
    foreach ($s in $spec) {
        G @('checkout', '-q', '-b', $s[0], 'main') | Out-Null
        if ($s[1] -gt 0) { W $SN (Snap 67) }
        if ($s[2] -gt 0) { W $RP (Rep 25) }
        if ($s[1] -gt 0) {
            $r = Run-Ps @((Join-Path $here 'fv_claim.ps1'), '-Fv', $s[1], '-Replay', $s[2], '-Reason', "test $($s[0])", '-Top', $tmp)
            Check "claim $($s[0])" ($r.Code -eq 0 -and $r.Out -match 'claim appended') $r.Out
        }
        W ("korean-division/src/f_" + ($s[0] -replace '/', '_') + '.txt') 'x'
        G @('add', '-A') | Out-Null; G @('commit', '-q', '-m', $s[0]) | Out-Null
    }
    G @('checkout', '-q', 'kd/g3') | Out-Null
    $r = Run-Ps @((Join-Path $here 'fv_claim.ps1'), '-Show', '-Top', $tmp)
    Check 'claim -Show' ($r.Out -match 'total: FV\+1 Replay\+1') $r.Out
    $r = Run-Ps @((Join-Path $here 'fv_claim.ps1'), '-Fv', 1, '-Reason', 'x', '-Branch', 'main', '-Top', $tmp)
    Check 'claim on main refused' ($r.Code -eq 1) $r.Out
    $r = Run-Ps @((Join-Path $here 'fv_claim.ps1'), '-Fv', 1, '-Top', $tmp)
    Check 'claim without reason refused' ($r.Code -eq 1) $r.Out
    G @('checkout', '-q', 'main') | Out-Null
    # main moves to 67/24 (the merge/0.2-final state)
    W $SN (Snap 67); G @('add', '-A') | Out-Null; G @('commit', '-q', '-m', 'final67') | Out-Null

    $fa = Join-Path $here 'fv_assign.ps1'
    $r = Run-Ps @($fa, '-Report', '-Root', $kd)
    Check 'report lists 4 claiming branches' ($r.Out -match '4 branch\(es\) with claims' -and $r.Out -match 'kd/tcb' -and $r.Out -notmatch 'kd/plain') $r.Out
    $r = Run-Ps @($fa, '-Order', 'kd/tcb,kd/tcc,kd/plain,kd/m1,kd/g3', '-Root', $kd)
    Check 'plan 68..71 / Replay 24,24,25,26' ($r.Out -match 'FV 68 / Replay 24' -and $r.Out -match 'FV 69 / Replay 24' -and $r.Out -match 'FV 70 / Replay 25' -and $r.Out -match 'final tip: FV 71 / Replay 26' -and $r.Out -match 'none \(unchanged\)') $r.Out
    # real sequential merge with the apply step (conflict on the FV line is resolved with --ours, then fv_assign sets the number)
    $steps = @(@('kd/tcb', '68/24'), @('kd/tcc', '69/24'), @('kd/plain', '69/24'), @('kd/m1', '70/25'), @('kd/g3', '71/26'))
    foreach ($s in $steps) {
        G @('merge', '--no-commit', '--no-ff', $s[0]) | Out-Null
        $conf = (G @('diff', '--name-only', '--diff-filter=U'))
        if ($conf) { G @('checkout', '--ours', '--', $SN, $RP) | Out-Null; G @('add', $SN, $RP) | Out-Null }
        $r = Run-Ps @($fa, '-Branch', $s[0], '-Apply', '-NoBuild', '-Root', $kd)
        Check "apply $($s[0]) -> $($s[1])" ($r.Code -eq 0 -and (Consts) -eq $s[1]) ($r.Out + ' consts=' + (Consts))
        G @('add', '-A') | Out-Null; G @('commit', '-q', '-m', "merge $($s[0])") | Out-Null
    }
    Check 'comment renumbered with constant' ((ReadF $SN) -match '= 71; // 71:') (ReadF $SN)
    $r = Run-Ps @($fa, '-Branch', 'kd/tcb', '-Root', $kd)
    Check 'dry run does not edit' ($r.Out -match 'dry' -and (Consts) -eq '71/26') $r.Out

    # --- queue_guard ---
    $qg = Join-Path $here 'queue_guard.ps1'
    $r = Run-Ps @($qg, '-Branch', 'kd/tcb', '-Base', 'main', '-Top', $tmp)
    Check 'queue_guard clean branch -> 0' ($r.Code -eq 0) $r.Out
    G @('checkout', '-q', '-b', 'kd/bad', 'main') | Out-Null
    W 'ops/QUEUE.md' "q2`n"; G @('add', '-A') | Out-Null; G @('commit', '-q', '-m', 'bad') | Out-Null
    G @('checkout', '-q', 'main') | Out-Null
    $r = Run-Ps @($qg, '-Branch', 'kd/bad', '-Base', 'main', '-Top', $tmp)
    Check 'queue_guard bad branch -> 1' ($r.Code -eq 1 -and $r.Out -match 'QUEUE_GUARD: ops/QUEUE.md') $r.Out
    G @('checkout', '-q', 'kd/bad') | Out-Null; G @('reset', '-q', '--hard', 'main') | Out-Null
    W 'ops/STATE.md' "dirty`n"
    $r = Run-Ps @($qg, '-Top', $tmp)
    Check 'queue_guard uncommitted STATE edit -> 1' ($r.Code -eq 1 -and $r.Out -match 'ops/STATE.md') $r.Out
    G @('checkout', '-q', '--', 'ops/STATE.md') | Out-Null; G @('checkout', '-q', 'main') | Out-Null

    # --- assemble_notes ---
    W 'korean-division/docs/decisions_pending.d/kd__b.md' "date: 2026-10-22 12:00`n- [b] second question`n"
    W 'korean-division/docs/decisions_pending.d/kd__a.md' "date: 2026-10-21`n- [a] first question`n"
    W 'korean-division/docs/engine_notes.d/kd__a.md' "## New system A`ntext a`n"
    $an = Join-Path $here 'assemble_notes.ps1'
    $r = Run-Ps @($an, '-Target', 'decisions', '-Docs', (Join-Path $kd 'docs'))
    $ia = $r.Out.IndexOf('first question'); $ib = $r.Out.IndexOf('second question')
    Check 'assemble prints date-ordered' ($ia -gt 0 -and $ib -gt $ia) $r.Out
    $before = ReadF 'korean-division/docs/DECISIONS_PENDING.md'
    $r = Run-Ps @($an, '-Write', '-Docs', (Join-Path $kd 'docs'))
    $r = Run-Ps @($an, '-Write', '-Docs', (Join-Path $kd 'docs'))
    $dp = ReadF 'korean-division/docs/DECISIONS_PENDING.md'; $en = ReadF 'korean-division/docs/ENGINE_NOTES.md'
    Check 'write keeps original text, idempotent' ($dp.StartsWith($before.TrimEnd()) -and ([regex]::Matches($dp, 'assembled-notes:begin')).Count -eq 1 -and ([regex]::Matches($en, 'New system A')).Count -eq 1 -and $en -match '## old section') $dp
    Check 'write order in file' ($dp.IndexOf('first question') -lt $dp.IndexOf('second question') -and $dp.IndexOf('keep me') -lt $dp.IndexOf('first question'))
    W 'korean-division/docs/decisions_pending.d/kd__a.md' ''
    $r = Run-Ps @($an, '-Write', '-Target', 'decisions', '-Docs', (Join-Path $kd 'docs'))
    $dp = ReadF 'korean-division/docs/DECISIONS_PENDING.md'
    Check 'removed fragment disappears on rewrite' ($dp -notmatch 'first question' -and $dp -match 'second question')
}
finally { if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue } }
if ($fail -eq 0) { Write-Host 'ALL PASS'; exit 0 } else { Write-Host "$fail FAILED"; exit 1 }
