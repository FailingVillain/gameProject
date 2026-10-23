# Unit + dry-run tests for heavy_gate.ps1 v3 (pools, waiters not counted, wait log, resource admission, spacing, watchdog,
# gate_config.json + fallback, per-purpose floors, budget 20, priority exemption, FIFO ticket queue, waiter cap, stale tickets, gate_stats).
# No real heavy processes needed: RAM/CPU are injected (KD_GATE_FREEGB_OVERRIDE / KD_GATE_CPU_OVERRIDE). The watchdog test stops
# one throwaway sleeping powershell started here (by PID). About 90 s total.
. "$PSScriptRoot\heavy_gate.ps1"
$fail = 0
function Ok($cond, $msg) { if (-not $cond) { "FAIL: $msg"; $script:fail++ } else { "ok: $msg" } }
function C($a, $b, $h) { @{ art = $a; build = $b; heavy = $h } }
$tmpDir = Join-Path $env:TEMP "kd-gate-test-$PID"; New-Item -ItemType Directory -Force $tmpDir | Out-Null
$env:KD_GATE_LOG = Join-Path $tmpDir 'gate_wait.log'
$env:KD_GATE_NO_AUTOMONITOR = '1'
foreach ($e in 'KD_GATE_BUDGET_GB','KD_GATE_FLOOR_HEAVY_GB','KD_GATE_FLOOR_RENDER_GB','KD_GATE_FLOOR_EMERGENCY_GB','KD_GATE_WAITER_CAP') { Remove-Item "Env:$e" -ErrorAction SilentlyContinue }
# legacy section runs on the v2.1 fallback values (no config file): floors 8/8, budget 14
$env:KD_GATE_CONFIG = Join-Path $tmpDir 'no_such_config.json'; Import-GateConfig
$env:KD_GATE_CPU_OVERRIDE = '10'
Ok ((Get-HeavyKind 'KoreanDivision.exe' 'x --selftest') -eq 'heavy') 'selftest=heavy'
Ok ((Get-HeavyKind 'KoreanDivision.exe' 'x --stress') -eq 'heavy') 'stress=heavy'
Ok ($null -eq (Get-HeavyKind 'KoreanDivision.exe' 'C:\a\dist\KoreanDivision.exe --stress')) 'dist excluded'
Ok ((Get-HeavyKind 'dotnet.exe' 'dotnet build x.csproj') -eq 'build') 'dotnet build=build'
Ok ((Get-HeavyKind 'dotnet.exe' 'dotnet C:\sdk\MSBuild.dll /nodemode:1') -eq 'child') 'msbuild node=child (RAM only)'
Ok ((Get-HeavyKind 'dotnet.exe' 'dotnet KoreanDivision.dll --collide') -eq 'heavy') 'dll run=heavy'
Ok ((Get-HeavyKind 'python.exe' 'python make_unit.py') -eq 'art') 'make_unit=art'
Ok ($null -eq (Get-HeavyKind 'python.exe' 'python rerender96.py')) 'rerender96 driver not counted'
Ok ($null -eq (Get-HeavyKind 'powershell.exe' 'powershell -File heavy_gate.ps1 -Render')) 'waiting gate script not counted'
Ok ($null -eq (Get-HeavyKind 'powershell.exe' 'powershell -File heavy_gate.ps1 -Monitor')) 'monitor not counted'
# shipped temporary limits (2026-10-22 freeze): art 4, non-art 3, build reserve 1
Ok ($script:ArtLimit -eq 4 -and $script:NonArtLimit -eq 3 -and $script:BuildReserve -eq 1) 'temp limits art 4 / non-art 3 / reserve 1'
Ok (-not (Test-PoolOpen (C 4 0 0) 'art' 9 12 $false)) 'art at 4 closed (temp limit)'
Ok (Test-PoolOpen (C 3 0 0) 'art' 9 12 $false) 'art 4th open'
# pool logic at the normal art limit 8
$script:ArtLimit = 8
Ok (Test-PoolOpen (C 8 0 0) 'build' 7 10 $false) 'art full does not block build'
Ok (-not (Test-PoolOpen (C 8 0 0) 'art' 7 10 $false)) 'art at 8 closed'
Ok (Test-PoolOpen (C 7 0 0) 'art' 7 12 $false) 'art 8th open at 7 GB free'
Ok (-not (Test-PoolOpen (C 7 0 0) 'art' 5 12 $false)) 'art 8th closed below 6 GB free'
Ok (Test-PoolOpen (C 6 0 0) 'art' 5 12 $false) 'art 7th open at 5 GB free'
Ok (-not (Test-PoolOpen (C 7 2 0) 'art' 5 10 $false)) 'art cannot take last combined slot (9/10)'
$script:ArtLimit = 4
Ok (Test-PoolOpen (C 0 0 1) 'heavy' 7 10 $false) 'heavy 2nd open'
Ok (-not (Test-PoolOpen (C 0 0 2) 'heavy' 7 10 $false)) 'heavy capped at 2 (build reserve)'
Ok (Test-PoolOpen (C 0 0 2) 'build' 7 10 $false) 'build uses reserved slot'
Ok (-not (Test-PoolOpen (C 0 1 2) 'build' 7 10 $false)) 'non-art pool 3 full'
Ok (Test-PoolOpen (C 0 2 0) 'heavy' 7 10 $false) 'heavy open with 2 builds, 0 heavy'
Ok (Test-PoolOpen (C 0 0 3) 'build' 7 10 $true) 'priority build passes at 3'
Ok (Test-PoolOpen (C 0 0 2) 'heavy' 7 10 $true) 'priority heavy ignores reserve'
Ok (-not (Test-PoolOpen (C 0 0 0) 'build' 2.5 10 $false)) 'free < 3 GB closes'
Ok ((Get-GateLimit 5) -eq 10 -and (Get-GateLimit 6) -eq 12 -and (Get-GateLimit 7 8) -eq 8) 'combined limit 10/12/fixed'
Ok (-not (Test-BudgetOk 15 14) -and (Test-BudgetOk 14 14)) 'heavy RAM budget 14'
# resource reasons (pure): RAM >= 8 GB both kinds, CPU < 85 %, spacing 20 s
function RR($k, $f, $c, $s) { (@(Get-ResourceWaitReasons $k $f $c $s) -join ',') }
Ok ((RR 'heavy' 8 84.9 -1) -eq '') 'heavy: 8 GB free, cpu 84.9, no admission -> no reason'
Ok ((RR 'heavy' 7.9 10 -1) -eq 'RAM') 'heavy: 7.9 GB -> RAM'
Ok ((RR 'build' 7.9 10 -1) -eq 'RAM') 'build: 7.9 GB -> RAM'
Ok ((RR 'art' 7.9 10 -1) -eq 'RAM' -and (RR 'art' 8 10 -1) -eq '') 'render: needs 8 GB'
Ok ((RR 'art' 12 85 -1) -eq 'CPU') 'cpu 85 -> CPU'
Ok ((RR 'heavy' 5 90 5) -eq 'RAM,CPU,SPACING') 'all three reasons'
Ok ((RR 'heavy' 12 10 19.9) -eq 'SPACING' -and (RR 'heavy' 12 10 20) -eq '') 'spacing 20 s'
Ok ((RR 'heavy' 12 -1 -1) -eq '') 'unknown cpu (-1) does not block'
$env:KD_GATE_CPU_OVERRIDE = '42.5'; Ok ((Get-CpuPct) -eq 42.5) 'cpu override'; $env:KD_GATE_CPU_OVERRIDE = '10'
# victim selection: newest art/heavy started after the oldest admission; builds and pre-gate jobs never chosen
$now = Get-Date; $adm = @(@{ t = $now.AddSeconds(-100).ToUniversalTime().Ticks; kind = 'art'; pid = 1 })
$pr = @([pscustomobject]@{ Kind = 'art'; ProcessId = 11; Name = 'python.exe'; CreationDate = $now.AddSeconds(-50) },
        [pscustomobject]@{ Kind = 'heavy'; ProcessId = 12; Name = 'KoreanDivision.exe'; CreationDate = $now.AddSeconds(-20) },
        [pscustomobject]@{ Kind = 'build'; ProcessId = 13; Name = 'dotnet.exe'; CreationDate = $now.AddSeconds(-5) },
        [pscustomobject]@{ Kind = 'art'; ProcessId = 14; Name = 'python.exe'; CreationDate = $now.AddSeconds(-500) })
Ok ((Select-KillVictim $pr $adm).ProcessId -eq 12) 'victim = newest gate-admitted art/heavy (not build, not pre-gate)'
Ok ($null -eq (Select-KillVictim $pr @())) 'no admission recorded -> no victim'
# fake process list: waiters hold no slot
$w = (1..6 | ForEach-Object { "$($_ + 100)|powershell.exe|powershell -File heavy_gate.ps1 -Render" }) -join ';'
$env:KD_GATE_FAKEPROCS = $w + ';200|python.exe|python rerender96.py;201|python.exe|python make_unit.py'
$c = Get-HeavyCounts
Ok ($c.art -eq 1 -and $c.build -eq 0 -and $c.heavy -eq 0) "6 waiters + driver not counted, 1 real render: art=$($c.art)"
# end-to-end dry runs
$gate = "$PSScriptRoot\heavy_gate.ps1"
function G { $o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate @args | Out-String); $script:GC = $LASTEXITCODE; $o }
$env:KD_GATE_FREEGB_OVERRIDE = '9'
$o = G -DryRun -Build
Ok ($o -match 'DRYRUN gate open: kind=build art=1') "dryrun build open with waiters: $($o.Trim())"
$env:KD_GATE_FAKEPROCS = (1..4 | ForEach-Object { "$($_ + 300)|python.exe|python make_unit.py" }) -join ';'
$o = G -DryRun -Render
Ok ($o -match 'DRYRUN gate closed: kind=art art=4' -and $o -match 'why=POOL' -and $o -match 'art:301:python.exe') 'dryrun art 4 closed (POOL), occupants listed'
$o = G -DryRun -Build
Ok ($o -match 'DRYRUN gate open: kind=build art=4') 'dryrun build still open while art pool full'
$env:KD_GATE_FAKEPROCS = '1|idle.exe|none'
$env:KD_GATE_FREEGB_OVERRIDE = '7'
$o = G -DryRun -Render
Ok ($o -match 'DRYRUN gate closed: .*why=RAM' -and $o -match 'free=7') "dryrun render closed by RAM with empty slots: $($o.Trim())"
$env:KD_GATE_FREEGB_OVERRIDE = '20'; $env:KD_GATE_CPU_OVERRIDE = '90'
$o = G -DryRun
Ok ($o -match 'DRYRUN gate closed: .*cpu=90% .*why=CPU') 'dryrun heavy closed by CPU 90'
$env:KD_GATE_CPU_OVERRIDE = '10'
$env:KD_GATE_FAKEPROCS = '401|KoreanDivision.exe|x --selftest;402|KoreanDivision.exe|x --stress;403|dotnet.exe|dotnet build a'
$env:KD_GATE_FREEGB_OVERRIDE = '9'
$o = G -DryRun
Ok ($o -match 'DRYRUN gate closed: kind=heavy art=0 build=1 heavy=2' -and $o -match 'heavy:402:KoreanDivision.exe') 'dryrun heavy closed (pool 3 full)'
# wait log + stdout occupancy + status JSON (about 4 s of real waiting)
$o = G -TimeoutSec 3 -PollSec 1 -ShowAfterSec 1 -Watchdog 0
$lg = Get-Content $env:KD_GATE_LOG -Raw
Ok ($o -match 'gate timeout' -and $o -match 'build:403:dotnet.exe') "timeout prints occupants: $($o.Trim())"
Ok ($o -match 'gate waiting') 'waiting occupancy printed to stdout'
Ok ($lg -match 'WAIT_START' -and $lg -match 'TIMEOUT' -and $lg -match 'pid=\d+' -and $lg -match 'build:403:dotnet.exe') 'wait log has start, timeout, occupants'
Ok ($lg -match 'json=\{.*"why":"POOL"') 'wait log lines carry JSON with reason'
$sj = Get-Content (Join-Path $tmpDir 'gate_status.json') -Raw | ConvertFrom-Json
Ok ($sj.state -eq 'timeout' -and $sj.free_gb -eq 9 -and $sj.cpu_pct -eq 10 -and $sj.why -eq 'POOL') "status json: $(($sj | ConvertTo-Json -Compress))"
Remove-Item $env:KD_GATE_LOG -ErrorAction SilentlyContinue
# RAM wait with empty slots -> reason RAM in log
$env:KD_GATE_FAKEPROCS = '1|idle.exe|none'; $env:KD_GATE_FREEGB_OVERRIDE = '6'
$o = G -Render -TimeoutSec 2 -PollSec 1 -Watchdog 0
Ok ($o -match 'gate timeout' -and (Get-Content $env:KD_GATE_LOG -Raw) -match 'WAIT_START .*why=RAM') 'render waits on RAM with free slots, log why=RAM'
# admission spacing: first admit opens, second within 20 s waits SPACING, then opens with spacing 0
$env:KD_GATE_FREEGB_OVERRIDE = '12'
$o1 = G -Build -TimeoutSec 2 -PollSec 1
$o2 = G -Build -TimeoutSec 2 -PollSec 1
Ok ($o1 -match 'gate open' -and $o2 -match 'gate timeout' -and $o2 -match 'why=SPACING') "spacing: 1st open, 2nd waits SPACING: $($o2.Trim())"
$env:KD_GATE_SPACING_SEC = '0'
$o3 = G -Build -TimeoutSec 2 -PollSec 1
Ok ($o3 -match 'gate open') 'spacing 0 -> opens'
$stj = Get-Content (Join-Path $tmpDir 'gate_state.json') -Raw | ConvertFrom-Json
Ok (@($stj.admits).Count -eq 2) "state ledger has 2 admissions: $(@($stj.admits).Count)"
# watchdog: free 2 GB for >= 2 s -> newest gate-admitted job is stopped by PID; older job and build untouched
$victim = Start-Process powershell.exe -ArgumentList '-NoProfile', '-Command', 'Start-Sleep 120' -WindowStyle Hidden -PassThru
$keep = Start-Process powershell.exe -ArgumentList '-NoProfile', '-Command', 'Start-Sleep 120' -WindowStyle Hidden -PassThru
$env:KD_GATE_FAKEPROCS = "$($keep.Id)|python.exe|python make_unit.py a|2;$($victim.Id)|python.exe|python make_unit.py b|1;999999|dotnet.exe|dotnet build x|0"
$env:KD_GATE_FREEGB_OVERRIDE = '2'; $env:KD_GATE_KILL_AFTER_SEC = '2'
$o = G -Monitor -MonitorSec 3 -PollSec 1
Start-Sleep -Milliseconds 500
$lg = Get-Content $env:KD_GATE_LOG -Raw
Ok ($victim.HasExited -and -not $keep.HasExited) "watchdog stopped only the newest admitted job (victim exited=$($victim.HasExited), keep exited=$($keep.HasExited))"
Ok ($lg -match "KILL art:$($victim.Id):python.exe" -and ([regex]::Matches($lg, 'KILL ')).Count -eq 1) 'one KILL line in log (cooldown)'
$o = G -Monitor -MonitorSec 3 -PollSec 1 -Watchdog 0
Ok (-not $keep.HasExited) '-Watchdog 0 never stops anything'
Stop-Process -Id $keep.Id -ErrorAction SilentlyContinue
Stop-Process -Id $victim.Id -ErrorAction SilentlyContinue
# ================= v3 tests =================
Remove-Item Env:KD_GATE_KILL_AFTER_SEC -ErrorAction SilentlyContinue
$env:KD_GATE_FAKEPROCS = '1|idle.exe|none'; $env:KD_GATE_CPU_OVERRIDE = '10'; $env:KD_GATE_FREEGB_OVERRIDE = '12'; $env:KD_GATE_SPACING_SEC = '0'
function WriteTxt($name, $text) { $p = Join-Path $tmpDir $name; [System.IO.File]::WriteAllText($p, $text); return $p }
function RQ($k, $f, $c, $s, $p) { (@(Get-ResourceWaitReasons $k $f $c $s $p) -join ',') }
# --- config + fallback to the v2.1 values
Import-GateConfig (Join-Path $tmpDir 'missing.json')
Ok ($script:CfgSource -eq 'default' -and (Get-BudgetGB) -eq 14 -and (Get-FloorHeavy) -eq 8 -and (Get-FloorRender) -eq 8 -and (Get-FloorEmergency) -eq 3 -and (Get-WaiterCap) -eq 6) 'missing config -> v2.1 values (14 / 8 / 8 / 3, cap 6)'
$cfgBroken = WriteTxt 'broken.json' '{ not json'
Import-GateConfig $cfgBroken
Ok ($script:CfgSource -eq 'broken' -and (Get-BudgetGB) -eq 14 -and (Get-FloorHeavy) -eq 8) 'broken config -> v2.1 values'
Import-GateConfig (WriteTxt 'partial.json' '{"budget_gb":"abc","floor_heavy_gb":6,"waiter_cap":3,"floor_heavy_stages":[{"from":"bad","gb":1}]}')
Ok ($script:CfgSource -eq 'file-partial' -and (Get-BudgetGB) -eq 14 -and (Get-FloorHeavy) -eq 6 -and (Get-WaiterCap) -eq 3) 'bad keys ignored per key, good keys used'
$shipped = Join-Path $PSScriptRoot 'gate_config.json'
Import-GateConfig $shipped
Ok ($script:CfgSource -eq 'file' -and (Get-BudgetGB) -eq 20 -and (Get-FloorRender) -eq 8 -and (Get-FloorEmergency) -eq 4 -and (Get-WaiterCap) -eq 6) 'shipped gate_config.json: budget 20, render 8, emergency 4, cap 6'
$stg = $script:CfgStages
function FloorOn($d) { Get-StagedFloor $stg 7 ([DateTime]::Parse($d)) }
Ok ((FloorOn '2026-10-22') -eq 7 -and (FloorOn '2026-10-23') -eq 7 -and (FloorOn '2026-10-24') -eq 6.5 -and (FloorOn '2026-10-25') -eq 6 -and (FloorOn '2027-01-01') -eq 6) 'staged heavy floor 7 -> 6.5 -> 6, one stage per day'
Ok ((Get-FloorHeavy) -ge 6 -and (Get-FloorHeavy) -le 7) "today's heavy floor within 6..7: $(Get-FloorHeavy)"
$env:KD_GATE_BUDGET_GB = '25'; Ok ((Get-BudgetGB) -eq 25 -and (Get-BudgetGB 30) -eq 30) 'env / -BudgetGB override the config budget'; Remove-Item Env:KD_GATE_BUDGET_GB
Ok ((Get-BudgetGB) -eq 20 -and (Test-BudgetOk 20 20) -and -not (Test-BudgetOk 20.1 20)) 'budget 20 GB (sum of heavy WorkingSet)'
# --- per-purpose floors, emergency floor, priority exemption (floor pinned to 7 so the test does not depend on the date)
$env:KD_GATE_FLOOR_HEAVY_GB = '7'
Ok ((RR 'heavy' 7 10 -1) -eq '' -and (RR 'heavy' 6.9 10 -1) -eq 'RAM' -and (RR 'build' 6.9 10 -1) -eq 'RAM') 'build/merge/selftest/measure floor 7'
Ok ((RR 'art' 7.9 10 -1) -eq 'RAM' -and (RR 'art' 8 10 -1) -eq '') 'render floor 8 unchanged'
Ok ((RR 'heavy' 3.9 10 -1) -eq 'RAM,EMERG' -and (RR 'art' 3.9 10 -1) -eq 'RAM,EMERG') 'below emergency floor 4: EMERG'
Ok ((RQ 'build' 5 10 -1 $true) -eq '' -and (RQ 'build' 5 10 -1 $false) -eq 'RAM') 'priority is exempt from the purpose floor'
Ok ((RQ 'build' 3.9 10 -1 $true) -eq 'EMERG' -and (RQ 'build' 4 10 -1 $true) -eq '') 'priority still blocked below the emergency floor'
Ok ((RQ 'build' 12 95 -1 $true) -eq 'CPU') 'priority still obeys CPU'
$env:KD_GATE_CONFIG = $shipped
$env:KD_GATE_FREEGB_OVERRIDE = '7.5'; $o = G -DryRun -Build
Ok ($o -match 'DRYRUN gate open') "dryrun build open at 7.5 GB (floor 7): $($o.Trim())"
$env:KD_GATE_FREEGB_OVERRIDE = '6.5'; $o = G -DryRun -Build
Ok ($o -match 'DRYRUN gate closed' -and $o -match 'why=RAM') 'dryrun build closed at 6.5 GB (RAM)'
$o = G -DryRun -Build -Priority
Ok ($o -match 'DRYRUN gate open') 'dryrun -Priority build open at 6.5 GB (exempt from floor)'
$env:KD_GATE_FREEGB_OVERRIDE = '3.5'; $o = G -DryRun -Build -Priority
Ok ($o -match 'DRYRUN gate closed' -and $o -match 'why=EMERG') 'dryrun -Priority closed at 3.5 GB (EMERG)'
$env:KD_GATE_FREEGB_OVERRIDE = '12'
$env:KD_GATE_HEAVYRAM_OVERRIDE = '20'; $o = G -DryRun -Build
Ok ($o -match 'DRYRUN gate open') 'heavy RAM 20 GB = budget -> open'
$env:KD_GATE_HEAVYRAM_OVERRIDE = '20.5'; $o = G -DryRun -Build
Ok ($o -match 'DRYRUN gate closed' -and $o -match 'why=BUDGET') 'heavy RAM 20.5 GB -> BUDGET'
$o = G -DryRun -Build -Priority
Ok ($o -match 'DRYRUN gate closed' -and $o -match 'why=BUDGET') '-Priority still respects the budget hard cap'
Remove-Item Env:KD_GATE_HEAVYRAM_OVERRIDE
# broken config file -> the gate itself runs on v2.1 values (floor 8, budget 14)
$env:KD_GATE_CONFIG = $cfgBroken; Remove-Item Env:KD_GATE_FLOOR_HEAVY_GB
$env:KD_GATE_FREEGB_OVERRIDE = '7.5'; $o = G -DryRun -Build
Ok ($o -match 'DRYRUN gate closed' -and $o -match 'why=RAM') 'broken config: floor falls back to 8 (7.5 GB closed)'
$env:KD_GATE_FREEGB_OVERRIDE = '12'; $env:KD_GATE_HEAVYRAM_OVERRIDE = '15'; $o = G -DryRun -Build
Ok ($o -match 'why=BUDGET') 'broken config: budget falls back to 14'
Remove-Item Env:KD_GATE_HEAVYRAM_OVERRIDE
$env:KD_GATE_CONFIG = $shipped; $env:KD_GATE_FLOOR_HEAVY_GB = '7'
# --- FIFO ticket queue (pure + files)
$qd = Join-Path $tmpDir 'gate_queue'
Remove-Item "$qd\*" -Force -ErrorAction SilentlyContinue
$sl = @(1..3 | ForEach-Object { Start-Process powershell.exe -ArgumentList '-NoProfile', '-Command', 'Start-Sleep 300' -WindowStyle Hidden -PassThru })
try {
  $b = (UtcTicks) - [long]1000000000
  $null = Add-GateTicket 'heavy' $false $sl[0].Id ($b + 1)
  $null = Add-GateTicket 'build' $false $sl[1].Id ($b + 2)
  $null = Add-GateTicket 'build' $true $sl[2].Id ($b + 3)
  $lv = @(Get-LiveTickets $false)
  Ok ((($lv | ForEach-Object { $_.pid }) -join ',') -eq (@($sl[2].Id, $sl[0].Id, $sl[1].Id) -join ',')) 'queue order: priority ticket first, then FIFO by join time'
  Ok ((Get-QueueAhead $lv $sl[2].Id $true) -eq 0 -and (Get-QueueAhead $lv $sl[0].Id $false) -eq 1 -and (Get-QueueAhead $lv $sl[1].Id $false) -eq 2) 'ahead counts 0 / 1 / 2 (only the head evaluates)'
  Ok ((Get-QueueAhead $lv 999990 $false) -eq 3 -and (Get-QueueAhead $lv 999990 $true) -eq 1) 'new normal call joins at the end, new priority call only behind priority tickets'
  # stale tickets: dead PID, reused PID (start time mismatch), old corrupt file; a fresh corrupt file is skipped but kept
  $null = Add-GateTicket 'heavy' $false 999999 ($b + 4)
  $reuse = @{ pid = $sl[1].Id; pst = ((Get-ProcStartTicks $sl[1].Id) + [long]36000000000); prio = 1; t = ($b + 5); kind = 'x' } | ConvertTo-Json -Compress
  [System.IO.File]::WriteAllText((Join-Path $qd '1_reuse.tkt'), $reuse)
  [System.IO.File]::WriteAllText((Join-Path $qd 'old_corrupt.tkt'), 'zz'); (Get-Item (Join-Path $qd 'old_corrupt.tkt')).LastWriteTime = (Get-Date).AddMinutes(-2)
  [System.IO.File]::WriteAllText((Join-Path $qd 'fresh_corrupt.tkt'), 'zz')
  $n0 = @(Get-ChildItem $qd -Filter *.tkt).Count
  $lv = @(Get-LiveTickets $false)
  Ok ($n0 -eq 7 -and $lv.Count -eq 3 -and @(Get-ChildItem $qd -Filter *.tkt).Count -eq 7) 'read without cleanup: 3 live, nothing deleted'
  $lv = @(Get-LiveTickets $true)
  $left = @(Get-ChildItem $qd -Filter *.tkt | ForEach-Object { $_.Name })
  Ok ($lv.Count -eq 3 -and $left.Count -eq 4 -and ($left -contains 'fresh_corrupt.tkt') -and -not ($left -contains '1_reuse.tkt') -and -not ($left -contains 'old_corrupt.tkt')) "stale tickets removed (dead pid, reused pid, old corrupt): left=$($left -join ',')"
  Remove-Item (Join-Path $qd 'fresh_corrupt.tkt') -Force
  # queue behaviour of the gate itself (3 live tickets: 1 priority, 2 normal)
  $env:KD_GATE_FREEGB_OVERRIDE = '12'
  $o = G -DryRun -Build
  Ok ($o -match 'DRYRUN gate closed' -and $o -match 'why=QUEUE') "normal caller queues behind 3 tickets: $($o.Trim())"
  $o = G -DryRun -Build -Priority
  Ok ($o -match 'DRYRUN gate closed' -and $o -match 'why=QUEUE') 'priority caller still behind an older priority ticket'
  # waiter cap: more than the cap tickets -> new non-priority call exits 75 and does not join
  $env:KD_GATE_WAITER_CAP = '2'
  $o = G -DryRun -Build
  Ok ($script:GC -eq 75 -and $o -match 'gate overloaded' -and $o -match 'do other work') "cap 2 with 3 waiting: dryrun exit 75: $($o.Trim())"
  $o = G -Build -TimeoutSec 5 -PollSec 1
  $lg = Get-Content $env:KD_GATE_LOG -Raw
  Ok ($script:GC -eq 75 -and $lg -match 'OVERLOAD kind=build queue=3 cap=2' -and @(Get-ChildItem $qd -Filter *.tkt).Count -eq 3) 'overloaded call exits 75, logs OVERLOAD, adds no ticket'
  $o = G -DryRun -Build -Priority
  Ok ($script:GC -ne 75) 'priority call is never rejected by the cap'
  $env:KD_GATE_WAITER_CAP = '3'
  $o = G -DryRun -Build
  Ok ($script:GC -eq 1 -and $o -match 'why=QUEUE') 'exactly cap tickets: not overloaded (limit is "more than" cap)'
  $env:KD_GATE_WAITER_CAP = '6'
  # priority jumps: remove the priority ticket -> a priority call is head and opens, a normal call still queues
  Remove-Item (Get-ChildItem $qd -Filter '0_*.tkt').FullName -Force
  $o = G -DryRun -Build -Priority
  Ok ($o -match 'DRYRUN gate open') 'priority caller is head when no priority ticket is ahead (jumps normal tickets)'
  $o = G -DryRun -Build
  Ok ($o -match 'why=QUEUE') 'normal caller still queued behind 2 normal tickets'
} finally {
  foreach ($p in $sl) { Stop-Process -Id $p.Id -ErrorAction SilentlyContinue }
  Remove-Item "$qd\*" -Force -ErrorAction SilentlyContinue
}
# stale ticket does not block a real call, and the call leaves no ticket behind
Add-GateTicket 'heavy' $false 999999 ((UtcTicks) - [long]100000000) | Out-Null
$o = G -Build -TimeoutSec 5 -PollSec 1
Ok ($o -match 'gate open' -and @(Get-ChildItem $qd -Filter *.tkt -ErrorAction SilentlyContinue).Count -eq 0) 'dead-PID ticket cleaned, call opens, no ticket left'
$env:KD_GATE_CPU_OVERRIDE = '95'
$o = G -Build -TimeoutSec 2 -PollSec 1
Ok ($o -match 'gate timeout' -and @(Get-ChildItem $qd -Filter *.tkt -ErrorAction SilentlyContinue).Count -eq 0) 'timeout removes the ticket'
$env:KD_GATE_CPU_OVERRIDE = '10'
# real FIFO order with 3 concurrent gate processes: A, B (normal) then C (-Priority) join while SPACING holds the gate.
# Expected admission order: C (priority jumps), A, B. Admissions are 5 s apart (spacing), fake process list, no real heavy work.
$env:KD_GATE_SPACING_SEC = '5'
Write-GateState @{ lastAdmit = (UtcTicks); lastKill = [long]0; admits = @() }
function Launch($name, $extra) {
  $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$gate`"", '-Build', '-PollSec', '1', '-TimeoutSec', '90', '-Watchdog', '0') + $extra
  Start-Process powershell.exe -ArgumentList $a -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $tmpDir "$name.out")
}
Remove-Item (Join-Path $tmpDir 'gate_wait.log') -ErrorAction SilentlyContinue
$pa = Launch 'A' @(); Start-Sleep -Milliseconds 1300
$pb = Launch 'B' @(); Start-Sleep -Milliseconds 1300
$pc = Launch 'C' @('-Priority')
Wait-Process -Id $pa.Id, $pb.Id, $pc.Id -Timeout 100 -ErrorAction SilentlyContinue
$ta = [System.IO.File]::GetLastWriteTimeUtc((Join-Path $tmpDir 'A.out')); $tb = [System.IO.File]::GetLastWriteTimeUtc((Join-Path $tmpDir 'B.out')); $tc = [System.IO.File]::GetLastWriteTimeUtc((Join-Path $tmpDir 'C.out'))
$txt = (Get-Content (Join-Path $tmpDir 'A.out') -Raw) + (Get-Content (Join-Path $tmpDir 'B.out') -Raw) + (Get-Content (Join-Path $tmpDir 'C.out') -Raw)
Ok (([regex]::Matches($txt, 'gate open')).Count -eq 3) "all three gate processes opened: $($txt.Trim() -replace '\s+', ' ')"
Ok ($tc -lt $ta -and $ta -lt $tb) "admission order C(priority) < A < B (C $($tc.ToString('mm:ss')), A $($ta.ToString('mm:ss')), B $($tb.ToString('mm:ss')))"
Ok ((Get-Content $env:KD_GATE_LOG -Raw) -match 'why=QUEUE') 'log shows non-head callers waiting with why=QUEUE'
Ok (@(Get-ChildItem $qd -Filter *.tkt -ErrorAction SilentlyContinue).Count -eq 0) 'all tickets removed after the run'
$env:KD_GATE_SPACING_SEC = '0'
# --- gate_stats.ps1 on a synthetic log
$slog = WriteTxt 'stats.log' (@(
  '2026-10-23 10:00:00 pid=11 WAIT_START kind=heavy free=6 GB why=RAM occupants=[] json={}',
  '2026-10-23 10:01:00 pid=11 WAIT waited=60s kind=heavy why=CPU occupants=[] json={}',
  '2026-10-23 10:01:40 pid=11 OPEN waited=100s kind=heavy why= json={}',
  '2026-10-23 10:00:00 pid=12 WAIT_START kind=build queue=2/2 free=9 GB why=QUEUE occupants=[] json={}',
  '2026-10-23 10:00:30 pid=12 TIMEOUT waited=30s kind=build queue=2/2 why=QUEUE json={}',
  '2026-10-23 10:00:40 pid=13 OVERLOAD kind=build queue=7 cap=6 (exit 75) json={}') -join "`r`n")
$so = (& powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\gate_stats.ps1" -Log $slog -Since '2000-01-01' | Out-String)
Ok ($so -match 'OPEN=1 TIMEOUT=1' -and $so -match 'OVERLOAD\(exit 75\)=1' -and $so -match 'median\s+65') "gate_stats totals (median of 100 and 30 = 65): $(($so -split "`n")[1..2] -join ' | ')"
Ok ($so -match 'RAM\s+1\s+60\s+60' -and $so -match 'CPU\s+1\s+40\s+40' -and $so -match 'QUEUE\s+1\s+30\s+30') 'gate_stats per-reason: RAM 60 s, CPU 40 s, QUEUE 30 s'
Remove-Item Env:KD_GATE_FAKEPROCS, Env:KD_GATE_FREEGB_OVERRIDE, Env:KD_GATE_LOG, Env:KD_GATE_CPU_OVERRIDE, Env:KD_GATE_SPACING_SEC, Env:KD_GATE_KILL_AFTER_SEC, Env:KD_GATE_NO_AUTOMONITOR, Env:KD_GATE_CONFIG, Env:KD_GATE_WAITER_CAP, Env:KD_GATE_FLOOR_HEAVY_GB, Env:KD_GATE_BUDGET_GB, Env:KD_GATE_HEAVYRAM_OVERRIDE -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
if ($fail) { "FAILED $fail"; exit 1 } else { 'ALL PASS' }
