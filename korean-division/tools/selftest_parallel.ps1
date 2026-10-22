# Parallel full selftest: splits the KD_SELFTEST_ONLY groups of src/SelfTest.cs into N balanced buckets (longest-first greedy,
# timings from tools/selftest_timings.json) and runs each bucket as its own process chain. Headless only (no window is ever opened).
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/selftest_parallel.ps1 [-Jobs 4] [-Dll bin/<name>/KoreanDivision.dll]
#       [-OutDir <dir>] [-Only g1,g2] [-DetCheck] [-SelfCheck] [-GateTimeoutMin 15] [-GroupTimeoutMin 25]
# Exit code: 0 = every group PASS, 1 = some group FAIL (or detcheck mismatch), 2 = partial (gate stayed closed / group missing).
param(
    [int]$Jobs = 4,
    [string]$Dll = "",
    [string]$OutDir = "",
    [string[]]$Only = @(),
    [switch]$DetCheck,
    [switch]$SelfCheck,
    [int]$GateTimeoutMin = 15,
    [int]$GroupTimeoutMin = 25,
    [switch]$Worker,                 # internal: child process that runs one bucket
    [int]$JobId = 0,
    [string]$GroupList = ""
)
$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$root = Split-Path -Parent $PSScriptRoot
$gate = Join-Path $PSScriptRoot "agent/heavy_gate.ps1"
$timingsFile = Join-Path $PSScriptRoot "selftest_timings.json"
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = "C:\Program Files\dotnet\dotnet.exe" }

# Groups that write shared files (must share ONE bucket). Currently none: tests use TestTemp (KD_TMP, per process). Add names here if one appears.
$SharedState = @()
# Groups that are NOT part of the full selftest: lmgtable = dev table; lodaudit = opens a Raylib window (headless rule) and is not called by the full run.
$NotTests = @("lmgtable", "lodaudit")
# Timing-sensitive (perf thresholds): run alone after all buckets finished, never beside other load.
$SerialAfter = @("perf", "core_fog")   # core_fog: fog update ms threshold
# Per-group env: the ms thresholds were tuned inside the warmed-up serial full run; a fresh process runs cold (tiered JIT) and misses them (core_fog 0.35 vs 0.30 ms).
# TieredCompilation=0 = fully optimized from the first call = the warmed steady state (0.21 ms, same as the serial run). Thresholds unchanged.
$GroupEnv = @{ core_fog = @{ DOTNET_TieredCompilation = "0" }; perf = @{ DOTNET_TieredCompilation = "0" } }
$SelfCheckGroups = @("hash", "savecompat", "uiicons")

function Get-GroupList {
    $src = Join-Path $root "src/SelfTest.cs"
    if (-not (Test-Path $src)) { throw "cannot read group list: $src missing" }
    $text = Get-Content $src -Raw -Encoding UTF8
    $names = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches($text, 'KD_SELFTEST_ONLY"\)\s*==\s*"([A-Za-z0-9_]+)"')) { $names.Add($m.Groups[1].Value) }
    # switch block added for the inline sections: case "core_map": ...  (only inside the 'string only =' block)
    $i = $text.IndexOf('string only = Environment')
    if ($i -ge 0) {
        $j = $text.IndexOf('default: core = false', $i)
        if ($j -gt $i) { foreach ($m in [regex]::Matches($text.Substring($i, $j - $i), 'case "([A-Za-z0-9_]+)":')) { $names.Add($m.Groups[1].Value) } }
    }
    return @($names | Select-Object -Unique | Where-Object { $NotTests -notcontains $_ })
}

function Invoke-Task([string]$Name, [string]$DllPath, [string]$Out, [int]$Job) {
    $logDir = Join-Path $Out "logs"; $resDir = Join-Path $Out "results"; $tmp = Join-Path $Out "tmp/$Name"
    New-Item -ItemType Directory -Force $logDir, $resDir, $tmp | Out-Null
    $tStart = Get-Date
    $env:KD_TMP = $tmp
    $env:KD_CAMPAIGN_DIR = $tmp
    if ($Name -eq "detcheck") { Remove-Item Env:KD_SELFTEST_ONLY -ErrorAction SilentlyContinue; $argList = "`"$DllPath`" --detcheck" }
    else { $env:KD_SELFTEST_ONLY = $Name; $argList = "`"$DllPath`" --selftest" }
    $extra = @(); if ($GroupEnv.ContainsKey($Name)) { foreach ($k in $GroupEnv[$Name].Keys) { Set-Item "Env:$k" $GroupEnv[$Name][$k]; $extra += $k } }
    $o = Join-Path $logDir "$Name.out"; $e = Join-Path $logDir "$Name.err"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $dotnet -ArgumentList $argList -WorkingDirectory $tmp -WindowStyle Hidden -PassThru -RedirectStandardOutput $o -RedirectStandardError $e
    $null = $p.Handle   # PS 5.1: keep the handle so ExitCode is readable after WaitForExit
    if (-not $p.WaitForExit($GroupTimeoutMin * 60000)) { try { Stop-Process -Id $p.Id -Force } catch {}; $code = -999 } else { $p.WaitForExit(); $code = $p.ExitCode }
    $sw.Stop()
    foreach ($k in $extra) { Remove-Item "Env:$k" -ErrorAction SilentlyContinue }
    $log = Join-Path $logDir "$Name.log"
    (Get-Content $o -Raw -Encoding UTF8) + "`n--- stderr ---`n" + (Get-Content $e -Raw -Encoding UTF8) | Set-Content $log -Encoding UTF8
    Remove-Item $o, $e -ErrorAction SilentlyContinue
    $txt = Get-Content $log -Raw -Encoding UTF8
    if ($Name -eq "detcheck") { $pass = ($code -eq 0) -and ($txt -match "DETCHECK ") -and ($txt -notmatch "MISMATCH|GOLDEN MISMATCH|GOLDEN FAIL") }
    else { $pass = ($code -eq 0) -and ($txt -match "SELFTEST PASS") -and ($txt -notmatch "\[FAIL\]|SELFTEST FAIL") }
    $res = [ordered]@{ group = $Name; job = $Job; t0 = $tStart.ToString("HH:mm:ss"); t1 = (Get-Date).ToString("HH:mm:ss"); exit = $code; secs = [math]::Round($sw.Elapsed.TotalSeconds, 1); pass = [bool]$pass }
    ($res | ConvertTo-Json -Compress) | Set-Content (Join-Path $resDir "$Name.json") -Encoding UTF8
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

# ---- worker mode: run a bucket sequentially ----
if ($Worker) {
    foreach ($g in ($GroupList -split ",")) { if ($g) { Invoke-Task $g $Dll $OutDir $JobId } }
    exit 0
}

# ---- parent ----
if ($Dll -eq "") {
    $cand = Get-ChildItem (Join-Path $root "bin") -Recurse -Filter KoreanDivision.dll -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "[\\/]publish[\\/]|[\\/]dist[\\/]" } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $cand) { throw "no bin/*/KoreanDivision.dll found - build first (own bin dir) or pass -Dll" }
    $Dll = $cand.FullName
}
$Dll = (Resolve-Path $Dll).Path
if ($OutDir -eq "") { $OutDir = Join-Path $env:TEMP ("kd-selftest-par/" + (Get-Date -Format "yyyyMMdd-HHmmss")) }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
Remove-Item (Join-Path $OutDir "results") -Recurse -Force -ErrorAction SilentlyContinue

$all = Get-GroupList
if ($all.Count -lt 10) { throw "group list parse looks wrong ($($all.Count) groups) - check src/SelfTest.cs" }
if ($SelfCheck) { $Jobs = 2; $Only = $SelfCheckGroups; $DetCheck = $false }
$full = ($Only.Count -eq 0)
$groups = if ($full) { $all } else { @($Only | ForEach-Object { $_ -split "," } | Where-Object { $_ }) }
# unknown name silently falls through to the FULL selftest in the exe, so fail loudly here
$unknown = @($groups | Where-Object { $all -notcontains $_ })
if ($unknown.Count -gt 0) { Write-Host "UNKNOWN selftest group(s): $($unknown -join ', ')" -ForegroundColor Red; Write-Host "known: $($all -join ' ')"; exit 1 }
$groups = @($groups | Select-Object -Unique)
$serialGroups = @($groups | Where-Object { $SerialAfter -contains $_ })
$parGroups = @($groups | Where-Object { $SerialAfter -notcontains $_ })
if ($DetCheck) { $parGroups += "detcheck" }

# ---- bucket plan: longest-first greedy ----
$timings = @{}
if (Test-Path $timingsFile) { $o = Get-Content $timingsFile -Raw | ConvertFrom-Json; foreach ($pp in $o.PSObject.Properties) { $timings[$pp.Name] = [double]$pp.Value } }
$known = @($timings.Values | Sort-Object)
$def = if ($known.Count) { $known[[int]($known.Count / 2)] } else { 20 }
function Est([string]$g) { if ($timings.ContainsKey($g)) { $timings[$g] } else { $def } }
$n = [math]::Max(1, [math]::Min($Jobs, $parGroups.Count))
$buckets = @(); for ($i = 0; $i -lt $n; $i++) { $buckets += , (New-Object System.Collections.ArrayList) }
$load = New-Object 'double[]' $n
$shared = @($parGroups | Where-Object { $SharedState -contains $_ })
foreach ($g in $shared) { [void]$buckets[0].Add($g); $load[0] += (Est $g) }
foreach ($g in ($parGroups | Where-Object { $SharedState -notcontains $_ } | Sort-Object { -(Est $_) })) {
    $k = 0; for ($i = 1; $i -lt $n; $i++) { if ($load[$i] -lt $load[$k]) { $k = $i } }
    [void]$buckets[$k].Add($g); $load[$k] += (Est $g)
}
Write-Host "dll: $Dll"; Write-Host "out: $OutDir"
Write-Host "groups: $($groups.Count) (+detcheck: $([bool]$DetCheck))  jobs: $n  serial-after: $($serialGroups -join ',')"
for ($i = 0; $i -lt $n; $i++) { Write-Host ("  bucket {0}: est {1,5:0}s  {2}" -f $i, $load[$i], ($buckets[$i] -join ' ')) }

# ---- launch: one heavy-gate slot per job ----
$sw0 = [Diagnostics.Stopwatch]::StartNew()
$procs = @{}; $notRun = @()
for ($i = 0; $i -lt $n; $i++) {
    $gout = & powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutMin $GateTimeoutMin
    if ($LASTEXITCODE -ne 0) { Write-Host "job ${i}: heavy gate closed > ${GateTimeoutMin} min -> NOT RUN: $($buckets[$i] -join ' ')" -ForegroundColor Yellow; $notRun += $buckets[$i]; continue }
    Write-Host "job ${i}: $gout"
    $wargs = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Worker -JobId $i -Dll `"$Dll`" -OutDir `"$OutDir`" -GroupList `"$($buckets[$i] -join ',')`" -GroupTimeoutMin $GroupTimeoutMin"
    $procs[$i] = Start-Process -FilePath "powershell.exe" -ArgumentList $wargs -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 4   # let its first dotnet appear so the next gate check counts it
}
$resDir = Join-Path $OutDir "results"
$lastN = -1
while (@($procs.Values | Where-Object { -not $_.HasExited }).Count -gt 0) {
    Start-Sleep -Seconds 10
    $done = if (Test-Path $resDir) { @(Get-ChildItem $resDir -Filter *.json).Count } else { 0 }
    if ($done -ne $lastN) { Write-Host ("  [{0,4:0}s] {1}/{2} done" -f $sw0.Elapsed.TotalSeconds, $done, $parGroups.Count); $lastN = $done }
}
# ---- timing-sensitive groups alone ----
foreach ($g in $serialGroups) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutMin $GateTimeoutMin | Out-Null
    if ($LASTEXITCODE -ne 0) { $notRun += $g; continue }
    Write-Host "  serial-after: $g"; Invoke-Task $g $Dll $OutDir 99
}
# ---- solo retry of failures that are ONLY millisecond-threshold checks (machine-load flakes, e.g. "Step max 127.9ms < 100ms") ----
# Anything else (logic FAIL, exception, exit != 0 without FAIL lines) is never retried. The first log is kept as <group>.first.log and the retry is printed.
$retried = @()
$resDir0 = Join-Path $OutDir "results"
foreach ($f in @(Get-ChildItem $resDir0 -Filter *.json -ErrorAction SilentlyContinue)) {
    $r = Get-Content $f.FullName -Raw | ConvertFrom-Json
    if ($r.pass -or $r.group -eq "detcheck") { continue }
    $lg = Join-Path $OutDir "logs/$($r.group).log"
    $fl = @(Get-Content $lg -Encoding UTF8 | Where-Object { $_ -match "\[FAIL\]" })
    if ($fl.Count -eq 0 -or @($fl | Where-Object { $_ -notmatch "\d(\.\d+)?\s*ms" }).Count -gt 0) { continue }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutMin $GateTimeoutMin | Out-Null
    if ($LASTEXITCODE -ne 0) { continue }
    Copy-Item $lg (Join-Path $OutDir "logs/$($r.group).first.log") -Force
    Write-Host "  retry solo (timing-only FAIL): $($r.group)" -ForegroundColor Yellow
    Invoke-Task $r.group $Dll $OutDir 98
    $retried += $r.group
}
$sw0.Stop()

# ---- aggregate ----
$results = @{}
if (Test-Path $resDir) { foreach ($f in Get-ChildItem $resDir -Filter *.json) { $r = Get-Content $f.FullName -Raw | ConvertFrom-Json; $results[$r.group] = $r } }
$want = @($parGroups) + @($serialGroups)
$fail = @(); $missing = @()
Write-Host "`n=== RESULT (wall $([math]::Round($sw0.Elapsed.TotalSeconds))s, sum of group time $([math]::Round((($results.Values | Measure-Object secs -Sum).Sum)))s) ==="
foreach ($g in $want) {
    if ($results.ContainsKey($g)) {
        $r = $results[$g]
        Write-Host ("{0,-6} {1,-12} job{2} {3,6:0.0}s exit={4}" -f $(if ($r.pass) { "PASS" } else { "FAIL" }), $g, $r.job, $r.secs, $r.exit)
        if (-not $r.pass) { $fail += $g } elseif ($retried -contains $g) { Write-Host "       ^ passed only on solo retry (first run failed a ms threshold under load; see logs/$g.first.log)" -ForegroundColor Yellow }
    } else { $missing += $g; Write-Host ("{0,-6} {1}" -f $(if ($notRun -contains $g) { "NOTRUN" } else { "NORES" }), $g) }
}
foreach ($g in $fail) {
    Write-Host "`n--- FAILED: $g (last 30 lines of $OutDir/logs/$g.log) ---" -ForegroundColor Red
    Get-Content (Join-Path $OutDir "logs/$g.log") -Tail 30 -Encoding UTF8
}
if ($DetCheck -and (Test-Path (Join-Path $OutDir "logs/detcheck.log"))) {
    Write-Host "`n--- detcheck ---"
    Get-Content (Join-Path $OutDir "logs/detcheck.log") -Encoding UTF8 | Where-Object { $_ -match "DETCHECK|GOLDEN" } | ForEach-Object { Write-Host $_ }
}
# refresh timings only after a clean full run (parallel durations are slightly inflated vs solo, fine for relative balance)
if ($full -and -not $SelfCheck -and $fail.Count -eq 0 -and $missing.Count -eq 0) {
    $o = [ordered]@{}; foreach ($g in ($results.Keys | Sort-Object)) { $o[$g] = $results[$g].secs }
    ($o | ConvertTo-Json) | Set-Content $timingsFile -Encoding UTF8
    Write-Host "timings updated: $timingsFile"
}
$code = if ($fail.Count -gt 0) { 1 } elseif ($missing.Count -gt 0) { 2 } else { 0 }
$label = @("ALL PASS", "FAIL: $($fail -join ' ')", "PARTIAL: not run/no result: $($missing -join ' ')")[$code]
Write-Host "`nPARALLEL SELFTEST $label  (exit $code)"
exit $code
