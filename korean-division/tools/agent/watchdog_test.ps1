# watchdog_test.ps1 - tests for watchdog.ps1 (3 verdicts via -Fake, gate-log probe) and stack_depth.ps1 (temp git repo). PS 5.1, ASCII-only.
# Prints PASS/FAIL lines; exit 0 only if all pass. Touches nothing outside a temp dir.
$ErrorActionPreference = 'Continue'
$dir = $PSScriptRoot
$fail = 0
function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { "PASS $name" } else { "FAIL $name :: $detail"; $script:fail++ }
}
$ps = (Get-Command powershell.exe).Source
function Run([string]$script, [string[]]$a) {
    $o = & $ps -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dir $script) @a 2>&1 | Out-String
    return @{ Out = $o; Code = $LASTEXITCODE }
}

# --- watchdog: three verdicts from mock input
$r = Run 'watchdog.ps1' @('-Fake', 'commit=2;file=none;scratch=90;runner=0;cpu=0;gate=none')
Check 'wd OK (recent commit)' ($r.Code -eq 0 -and $r.Out -match '\[OK\]') $r.Out
$r = Run 'watchdog.ps1' @('-Fake', 'commit=120;file=100;scratch=95;runner=2;cpu=1;gate=none')
Check 'wd OK (runner cpu moving)' ($r.Code -eq 0 -and $r.Out -match '\[OK\]') $r.Out
$r = Run 'watchdog.ps1' @('-Fake', 'commit=120;file=100;scratch=95;runner=0;cpu=0;gate=wait')
Check 'wd WAITING_GATE' ($r.Code -eq 0 -and $r.Out -match '\[WAITING_GATE\]') $r.Out
$r = Run 'watchdog.ps1' @('-Fake', 'commit=120;file=100;scratch=95;runner=0;cpu=0;gate=none')
Check 'wd SUSPECT_STALL' ($r.Code -eq 2 -and $r.Out -match '\[SUSPECT_STALL\]') $r.Out
$r = Run 'watchdog.ps1' @('-Fake', 'commit=120;file=none;scratch=none;runner=1;cpu=0;gate=none')
Check 'wd SUSPECT_STALL (idle runner noted)' ($r.Code -eq 2 -and $r.Out -match 'CPU did not change') $r.Out
$r = Run 'watchdog.ps1' @('-Fake', 'commit=29;scratch=none;runner=0;cpu=0;gate=none')
Check 'wd boundary 29 min = OK' ($r.Code -eq 0) $r.Out
$r = Run 'watchdog.ps1' @('-Fake', 'commit=30;scratch=none;runner=0;cpu=0;gate=none')
Check 'wd boundary 30 min = STALL' ($r.Code -eq 2) $r.Out

# --- watchdog: real probe on this checkout with a fresh wait log -> WAITING_GATE; stale log -> not WAITING_GATE
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('wdtest_' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $top = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $dir))
    $log = Join-Path $tmp 'gate_wait.log'
    Set-Content -Path $log -Value '2026-10-22 18:00:00 WAIT_START art=8/8 occupants=[make_unit:1]'
    $r = Run 'watchdog.ps1' @($top, '-GateLog', $log, '-SampleSec', '0')
    Check 'wd probe fresh wait log -> WAITING_GATE' ($r.Out -match '\[WAITING_GATE\]') $r.Out
    (Get-Item $log).LastWriteTime = (Get-Date).AddMinutes(-20)
    $r = Run 'watchdog.ps1' @($top, '-GateLog', $log, '-SampleSec', '0')
    Check 'wd probe stale wait log -> not WAITING_GATE' ($r.Out -notmatch '\[WAITING_GATE\]' -and $r.Out -match 'no process was touched') $r.Out

    # --- stack_depth on a temp repo: line L; chain a<-b<-c<-d<-e unmerged; m already in line
    $rp = Join-Path $tmp 'repo'
    New-Item -ItemType Directory -Path $rp | Out-Null
    $g = { param($gitArgs) & git -C $rp -c user.email=t@t -c user.name=t @gitArgs 2>&1 | Out-Null }
    & $g @('init', '-q', '-b', 'main')
    function Commit([string]$n) { Set-Content -Path (Join-Path $rp "$n.txt") -Value $n; & $g @('add', '-A'); & $g @('commit', '-q', '-m', $n) }
    Commit 'base'
    & $g @('branch', 'merge/0.2')
    & $g @('checkout', '-q', '-b', 'a'); Commit 'a'
    & $g @('checkout', '-q', '-b', 'b'); Commit 'b'
    & $g @('checkout', '-q', '-b', 'c'); Commit 'c'
    & $g @('checkout', '-q', '-b', 'd'); Commit 'd'
    & $g @('checkout', '-q', '-b', 'e'); Commit 'e'
    & $g @('checkout', '-q', '-b', 'side', 'merge/0.2'); Commit 'side'
    $r = Run 'stack_depth.ps1' @('-Top', $rp, '-Line', 'merge/0.2', 'a', 'c', 'd', 'e', 'side')
    Check 'sd depth a=1' ($r.Out -match '\[OK\] a depth 1') $r.Out
    Check 'sd depth c=3 OK' ($r.Out -match '\[OK\] c depth 3') $r.Out
    Check 'sd depth d=4 WARN' ($r.Out -match '\[WARN\] d depth 4') $r.Out
    Check 'sd e lists chain a <- b <- c <- d' ($r.Out -match 'e depth 5 .*: a <- b <- c <- d') $r.Out
    Check 'sd side independent depth 1' ($r.Out -match '\[OK\] side depth 1 .*\(none\)') $r.Out
    Check 'sd exit code 3 on warn' ($r.Code -eq 3) "code $($r.Code)"
    $r = Run 'stack_depth.ps1' @('-Top', $rp, '-Line', 'merge/0.2', 'c')
    Check 'sd exit 0 when within limit' ($r.Code -eq 0) $r.Out
    # after landing a and b in the line the depth of e drops to 3
    & $g @('checkout', '-q', 'merge/0.2'); & $g @('merge', '-q', '--ff-only', 'b')
    $r = Run 'stack_depth.ps1' @('-Top', $rp, '-Line', 'merge/0.2', 'e')
    Check 'sd landed predecessors drop out' ($r.Out -match '\[OK\] e depth 3 .*: c <- d' -and $r.Code -eq 0) $r.Out
} finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
if ($fail -gt 0) { "watchdog_test: $fail FAIL"; exit 1 }
"watchdog_test: all PASS"
exit 0
