# merge_step.ps1 - verify ONE merge step end to end (PowerShell 5.1, no admin). Source is ASCII-only on purpose.
# Stages (none can be skipped - LESSONS 23/28):
#   1 gate+build  2 detcheck vs -Expected (mismatch => STOP at once)  3 selftest group "snapshot"  4 --snaptest
#   5 optional own groups (-Groups)  6 any FAIL not in -AllowFail => STOP  7 git tag  8 append one line to MERGE_PROGRESS.md
# Ends with ONE summary line:  PASS <step> ...  or  STOP <step> <stage>: <reason>   (exit 0 / 1)
#
#   merge_step.ps1 -Step E1 -Expected "672804207fc4e486 3c42aea49324248e" -AllowFail uiicons,traindemo [-Groups isai,units] [-Tag post-E1]
#
#   -Step <label>        step name for the log/tag (e.g. E1). Tag default = "post-<Step>".
#   -Expected "<a> <b>"  expected two 16-hex detcheck checksums (space or comma separated).
#   -AllowFail a,b       substrings of FAIL test names that are expected (e.g. uiicons,traindemo). Others => STOP.
#   -Groups g1,g2        extra KD_SELFTEST_ONLY groups (own bundle). The "snapshot" group always runs.
#   -Root <dir>          korean-division dir of the merge worktree (default: this script's checkout).
#   -Progress <file>     default <repo top>/ops/MERGE_PROGRESS.md (appended, not committed).
#   -Out <dir>           build output (default bin/merge). Runs `dotnet <Out>/KoreanDivision.dll`.
#   -Priority            pass -Priority to heavy_gate (only while holding the merge lock). -NoGate skips the gate call.
#   -GuardBranch <ref>   stage 0: run queue_guard.ps1 on the branch being merged (QUEUE/STATE/MERGE_QUEUE edits => WARN line,
#                        STOP with -GuardStrict). -GuardBase default main.
#   -DryRun              print the plan, run nothing, tag nothing, write nothing. With -Feed <dir> the stages read
#                        detcheck.txt / selftest_<group>.txt / snaptest.txt from that dir instead (tests the STOP logic).
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory = $true)][string]$Step,
    [Parameter(Mandatory = $true)][string]$Expected,
    [string[]]$AllowFail = @(),
    [string[]]$Groups = @(),
    [string]$Tag = '',
    [string]$Root = '',
    [string]$Progress = '',
    [string]$Out = 'bin/merge',
    [string]$Note = '',
    [switch]$Priority,
    [switch]$NoGate,
    [switch]$DryRun,
    [string]$Feed = '',
    [string]$GuardBranch = '',
    [string]$GuardBase = 'main',
    [switch]$GuardStrict
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if ($Root -eq '') { $Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$Root = (Resolve-Path $Root).Path
$Top = (& git -C $Root rev-parse --show-toplevel 2>$null)
if (-not $Top) { $Top = Split-Path -Parent $Root }
if ($Progress -eq '') { $Progress = Join-Path $Top 'ops/MERGE_PROGRESS.md' }
if ($Tag -eq '') { $Tag = "post-$Step" }
$AllowFail = @($AllowFail | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' } | ForEach-Object { $_.Trim().ToLower() })
$Groups = @($Groups | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' } | ForEach-Object { $_.Trim() })
$Exp = @($Expected -split '[\s,]+' | Where-Object { $_ -ne '' } | ForEach-Object { $_.ToLower() })
$Dll = Join-Path $Root (Join-Path $Out 'KoreanDivision.dll')
$script:Log = @()

function Stop-Step([string]$stage, [string]$why) {
    Write-Host ("STOP {0} {1}: {2}" -f $Step, $stage, $why)
    exit 1
}
function Say([string]$m) { Write-Host ("[merge_step {0}] {1}" -f $Step, $m) }

# Run the game dll with args and optional KD_SELFTEST_ONLY; returns @{ Out; Code }. In DryRun reads -Feed instead.
function Invoke-Kd([string]$feedName, [string[]]$kdArgs, [string]$only) {
    if ($DryRun) {
        $sfx = ''
        if ($only) { $sfx = "  (KD_SELFTEST_ONLY=$only)" }
        Say ("dry: dotnet {0} {1}{2}" -f $Dll, ($kdArgs -join ' '), $sfx)
        $f = ''
        if ($Feed -ne '') { $f = Join-Path $Feed $feedName }
        if ($f -ne '' -and (Test-Path $f)) { return @{ Out = [System.IO.File]::ReadAllText($f); Code = 0; Dry = $false } }
        return @{ Out = '(dry run, no output)'; Code = 0; Dry = $true }
    }
    $saved = [Environment]::GetEnvironmentVariable('KD_SELFTEST_ONLY')
    if ($only) { [Environment]::SetEnvironmentVariable('KD_SELFTEST_ONLY', $only) } else { [Environment]::SetEnvironmentVariable('KD_SELFTEST_ONLY', $null) }
    $ErrorActionPreference = 'Continue'
    try { $o = & dotnet $Dll @kdArgs 2>&1 | Out-String; $c = $LASTEXITCODE }
    finally { [Environment]::SetEnvironmentVariable('KD_SELFTEST_ONLY', $saved); $ErrorActionPreference = 'Stop' }
    return @{ Out = $o; Code = $c; Dry = $false }
}
# FAIL/ERROR test names from "[FAIL] name" lines.
function Get-Fails([string]$text) {
    $r = @()
    foreach ($l in ($text -split "`r?`n")) { if ($l -match '\[(FAIL|ERROR)\]\s*(.*)$') { $r += $Matches[2].Trim() } }
    return ,$r
}
function Test-Allowed([string]$name) {
    $n = $name.ToLower()
    foreach ($a in $AllowFail) { if ($n.Contains($a)) { return $true } }
    return $false
}
function Check-Fails([string]$stage, $res) {
    $bad = @(); $ok = 0
    foreach ($f in (Get-Fails $res.Out)) { if (Test-Allowed $f) { $ok++ } else { $bad += $f } }
    if ($bad.Count -gt 0) { Stop-Step $stage ("{0} unexpected FAIL: {1}" -f $bad.Count, (($bad | Select-Object -First 5) -join ' | ')) }
    if ($res.Code -ne 0 -and $ok -eq 0) { Stop-Step $stage "exit code $($res.Code) with no parsable FAIL line (crash?)" }
    $script:Log += ("{0}:allowedFAIL={1}" -f $stage, $ok)
}

$grpText = 'snapshot'
if ($Groups.Count) { $grpText += ',' + ($Groups -join ',') }
$dryText = ''
if ($DryRun) { $dryText = ' DRYRUN' }
Say ("root={0} tag={1} expected={2} allow=[{3}] groups=[{4}]{5}" -f $Root, $Tag, ($Exp -join ' '), ($AllowFail -join ','), $grpText, $dryText)
if ($Exp.Count -lt 1) { Stop-Step 'args' 'no expected checksum' }
$head = (& git -C $Root rev-parse --short HEAD).Trim()
$branch = (& git -C $Root rev-parse --abbrev-ref HEAD).Trim()
if (-not $DryRun) {
    if (& git -C $Root tag -l $Tag) { Stop-Step 'tag' "tag $Tag already exists" }
    $gd = (& git -C $Root rev-parse --git-dir).Trim()
    if (-not [System.IO.Path]::IsPathRooted($gd)) { $gd = Join-Path $Root $gd }
    if (Test-Path (Join-Path $gd 'MERGE_HEAD')) { Stop-Step 'git' 'merge in progress (MERGE_HEAD)' }
}

# 0 queue guard (work branches must not edit QUEUE/STATE/MERGE_QUEUE)
if ($GuardBranch -ne '') {
    $qg = Join-Path $PSScriptRoot 'queue_guard.ps1'
    $qo = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $qg -Branch $GuardBranch -Base $GuardBase -Top $Top 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) {
        Say ("WARN queue_guard: " + ($qo -join ' ; ') + " -> keep the merge line's own copy of those files ('ours')")
        $script:Log += 'queueguard=WARN'
        if ($GuardStrict) { Stop-Step 'queue-guard' ($qo -join ' ; ') }
    } else { $script:Log += 'queueguard=ok' }
}

# 1 gate + build
if ($DryRun) {
    $gt = ''
    if ($Priority) { $gt = '-Priority' } elseif ($NoGate) { $gt = '(skipped)' }
    Say ("dry: heavy_gate {0}; dotnet build KoreanDivision.csproj -c Release -o {1}" -f $gt, $Out)
}
else {
    if (-not $NoGate) {
        $gate = Join-Path $PSScriptRoot 'heavy_gate.ps1'
        $ga = @('-ExecutionPolicy', 'Bypass', '-File', $gate)
        if ($Priority) { $ga += '-Priority' }
        & powershell -NoProfile @ga | Out-Null
        if ($LASTEXITCODE -ne 0) { Stop-Step 'gate' "heavy_gate exit $LASTEXITCODE" }
    }
    Push-Location $Root
    try {
        $ErrorActionPreference = 'Continue'
        $b = & dotnet build KoreanDivision.csproj -c Release -o $Out -v q --nologo 2>&1 | Out-String
        $bc = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
    } finally { Pop-Location }
    if ($bc -ne 0) { Stop-Step 'build' ((($b -split "`r?`n") | Where-Object { $_ -match 'error' } | Select-Object -First 3) -join ' | ') }
    if (-not (Test-Path $Dll)) { Stop-Step 'build' "missing $Dll" }
}
$script:Log += 'build:ok'

# 2 detcheck
$r = Invoke-Kd 'detcheck.txt' @('--detcheck') ''
$m = [regex]::Match($r.Out, 'DETCHECK\s+([0-9a-fA-F]{16})\s+([0-9a-fA-F]{16})')
if ($r.Dry) { $got = $Exp; Say 'dry: detcheck assumed to match' }
elseif (-not $m.Success) { Stop-Step 'detcheck' "no DETCHECK line (exit $($r.Code))" }
else { $got = @($m.Groups[1].Value.ToLower(), $m.Groups[2].Value.ToLower()) }
for ($i = 0; $i -lt $Exp.Count; $i++) {
    if ($i -ge $got.Count -or $got[$i] -ne $Exp[$i]) { Stop-Step 'detcheck' ("checksum mismatch: got {0} expected {1}" -f ($got -join ' '), ($Exp -join ' ')) }
}
$script:Log += ("detcheck={0}" -f ($got -join ' '))

# 3 snapshot selftest group (+ own groups), 4 snaptest. Every FAIL is checked against -AllowFail.
foreach ($g in (@('snapshot') + $Groups)) {
    $r = Invoke-Kd ("selftest_{0}.txt" -f $g) @('--selftest') $g
    Check-Fails "selftest-$g" $r
}
$env:KD_SNAP_QUICK = '1'
try { $r = Invoke-Kd 'snaptest.txt' @('--snaptest') '' } finally { Remove-Item Env:KD_SNAP_QUICK -ErrorAction SilentlyContinue }
Check-Fails 'snaptest' $r
$okCount = ([regex]::Matches($r.Out, '\[OK\]')).Count
$script:Log += ("snaptestOK={0}" -f $okCount)

# 7 tag, 8 progress line
$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm'
$allowTxt = 'allow=none'
if ($AllowFail.Count) { $allowTxt = 'allow=' + ($AllowFail -join ',') }
$noteTxt = ''
if ($Note) { $noteTxt = " | $Note" }
$line = "- {0} step {1} PASS {2} ({3}) tag {4} | {5} | {6}{7}" -f $stamp, $Step, $head, $branch, $Tag, ($script:Log -join ' '), $allowTxt, $noteTxt
if ($DryRun) { Say "dry: git tag $Tag"; Say ("dry: append to {0}: {1}" -f $Progress, $line) }
else {
    & git -C $Root tag $Tag
    if ($LASTEXITCODE -ne 0) { Stop-Step 'tag' "git tag $Tag failed" }
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::AppendAllText($Progress, $line + "`r`n", $enc)
}
$dryEnd = ''
if ($DryRun) { $dryEnd = ' (dry run)' }
Write-Host ("PASS {0} {1} tag={2} detcheck={3} snaptestOK={4} allowedFAIL=[{5}]{6}" -f $Step, $head, $Tag, ($got -join ' '), $okCount, ($AllowFail -join ','), $dryEnd)
exit 0
