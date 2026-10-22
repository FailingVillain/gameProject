# Unit + dry-run tests for heavy_gate.ps1 v2 (pools, waiters not counted, wait log). No real heavy processes needed.
$env:KD_GATE_ART_LIMIT = '8'   # the pool-logic tests below assume the original art limit 8; the new default (2) is checked at the end
. "$PSScriptRoot\heavy_gate.ps1"
$fail = 0
function Ok($cond, $msg) { if (-not $cond) { "FAIL: $msg"; $script:fail++ } else { "ok: $msg" } }
function C($a, $b, $h) { @{ art = $a; build = $b; heavy = $h } }
Ok ((Get-HeavyKind 'KoreanDivision.exe' 'x --selftest') -eq 'heavy') 'selftest=heavy'
Ok ((Get-HeavyKind 'KoreanDivision.exe' 'x --stress') -eq 'heavy') 'stress=heavy'
Ok ($null -eq (Get-HeavyKind 'KoreanDivision.exe' 'C:\a\dist\KoreanDivision.exe --stress')) 'dist excluded'
Ok ((Get-HeavyKind 'dotnet.exe' 'dotnet build x.csproj') -eq 'build') 'dotnet build=build'
Ok ((Get-HeavyKind 'dotnet.exe' 'dotnet C:\sdk\MSBuild.dll /nodemode:1') -eq 'child') 'msbuild node=child (RAM only)'
Ok ((Get-HeavyKind 'dotnet.exe' 'dotnet KoreanDivision.dll --collide') -eq 'heavy') 'dll run=heavy'
Ok ((Get-HeavyKind 'python.exe' 'python make_unit.py') -eq 'art') 'make_unit=art'
Ok ($null -eq (Get-HeavyKind 'python.exe' 'python rerender96.py')) 'rerender96 driver not counted'
Ok ($null -eq (Get-HeavyKind 'powershell.exe' 'powershell -File heavy_gate.ps1 -Render')) 'waiting gate script not counted'
# pools
Ok (Test-PoolOpen (C 8 0 0) 'build' 7 10 $false) 'art full does not block build'
Ok (-not (Test-PoolOpen (C 8 0 0) 'art' 7 10 $false)) 'art at 8 closed'
Ok (Test-PoolOpen (C 7 0 0) 'art' 7 12 $false) 'art 8th open at 7 GB free'
Ok (-not (Test-PoolOpen (C 7 0 0) 'art' 5 12 $false)) 'art 8th closed below 6 GB free'
Ok (Test-PoolOpen (C 6 0 0) 'art' 5 12 $false) 'art 7th open at 5 GB free'
Ok (-not (Test-PoolOpen (C 7 2 0) 'art' 5 10 $false)) 'art cannot take last combined slot (9/10)'
Ok (Test-PoolOpen (C 0 0 1) 'heavy' 7 10 $false) 'heavy 2nd open'
Ok (-not (Test-PoolOpen (C 0 0 2) 'heavy' 7 10 $false)) 'heavy capped at 2 (build reserve)'
Ok (Test-PoolOpen (C 0 0 2) 'build' 7 10 $false) 'build uses reserved slot'
Ok (-not (Test-PoolOpen (C 0 1 2) 'build' 7 10 $false)) 'non-art pool 3 full'
Ok (-not (Test-PoolOpen (C 0 2 0) 'heavy' 7 10 $false) -eq $false) 'heavy open with 2 builds, 0 heavy'
Ok (Test-PoolOpen (C 0 0 3) 'build' 7 10 $true) 'priority build passes at 3'
Ok (Test-PoolOpen (C 0 0 2) 'heavy' 7 10 $true) 'priority heavy ignores reserve'
Ok (-not (Test-PoolOpen (C 0 0 0) 'build' 2.5 10 $false)) 'free < 3 GB closes'
Ok ((Get-GateLimit 5) -eq 10 -and (Get-GateLimit 6) -eq 12 -and (Get-GateLimit 7 8) -eq 8) 'combined limit 10/12/fixed'
Ok (-not (Test-BudgetOk 15 14) -and (Test-BudgetOk 14 14)) 'heavy RAM budget 14'
# fake process list: waiters hold no slot
$w = (1..6 | ForEach-Object { "$($_ + 100)|powershell.exe|powershell -File heavy_gate.ps1 -Render" }) -join ';'
$env:KD_GATE_FAKEPROCS = $w + ';200|python.exe|python rerender96.py;201|python.exe|python make_unit.py'
$c = Get-HeavyCounts
Ok ($c.art -eq 1 -and $c.build -eq 0 -and $c.heavy -eq 0) "6 waiters + driver not counted, 1 real render: art=$($c.art)"
# end-to-end dry runs
$gate = "$PSScriptRoot\heavy_gate.ps1"
$env:KD_GATE_FREEGB_OVERRIDE = '7'
$o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -DryRun -Build | Out-String)
Ok ($o -match 'DRYRUN gate open: kind=build art=1') "dryrun build open with waiters: $($o.Trim())"
$env:KD_GATE_FAKEPROCS = (1..8 | ForEach-Object { "$($_ + 300)|python.exe|python make_unit.py" }) -join ';'
$o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -DryRun -Render | Out-String)
Ok ($o -match 'DRYRUN gate closed: kind=art art=8' -and $o -match 'art:301:python.exe') 'dryrun art full closed, occupants listed'
$o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -DryRun -Build | Out-String)
Ok ($o -match 'DRYRUN gate open: kind=build art=8') 'dryrun build still open while art pool full'
$env:KD_GATE_FAKEPROCS = '401|KoreanDivision.exe|x --selftest;402|KoreanDivision.exe|x --stress;403|dotnet.exe|dotnet build a'
$o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -DryRun | Out-String)
Ok ($o -match 'DRYRUN gate closed: kind=heavy art=0 build=1 heavy=2' -and $o -match 'heavy:402:KoreanDivision.exe') 'dryrun heavy closed (pool 3 full)'
# wait log + stdout occupancy (about 4 s of real waiting)
$log = Join-Path $env:TEMP "kd-gate-test-$PID.log"; $env:KD_GATE_LOG = $log
$o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutSec 3 -PollSec 1 -ShowAfterSec 1 | Out-String)
$lg = Get-Content $log -Raw
Ok ($o -match 'gate timeout' -and $o -match 'build:403:dotnet.exe') "timeout prints occupants: $($o.Trim())"
Ok ($o -match 'gate waiting') 'waiting occupancy printed to stdout'
Ok ($lg -match 'WAIT_START' -and $lg -match 'TIMEOUT' -and $lg -match 'pid=\d+' -and $lg -match 'build:403:dotnet.exe') 'wait log has start, timeout, occupants'
Remove-Item $log -ErrorAction SilentlyContinue
$env:KD_GATE_FREEGB_OVERRIDE = '2.5'; $env:KD_GATE_FAKEPROCS = ''
$o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -DryRun -Build | Out-String)
Ok ($o -match 'DRYRUN gate closed: .*free=2.5') 'dryrun closed at 2.5 GB free'
Remove-Item Env:KD_GATE_FAKEPROCS, Env:KD_GATE_FREEGB_OVERRIDE, Env:KD_GATE_LOG -ErrorAction SilentlyContinue
if ($fail) { "FAILED $fail"; exit 1 } else { 'ALL PASS' }

# default art limit = 2 (user 2026-10-23); env override = 8
Remove-Item Env:KD_GATE_ART_LIMIT -ErrorAction SilentlyContinue
. "$PSScriptRoot\heavy_gate.ps1"
Ok ($script:ArtLimit -eq 2) 'default art limit is 2'
Ok ((Get-ArtLimit 7) -eq 2 -and (Get-ArtLimit 5) -eq 1) 'art limit 2 (1 when free RAM < 6 GB)'
$env:KD_GATE_ART_LIMIT = '8'; . "$PSScriptRoot\heavy_gate.ps1"
Ok ($script:ArtLimit -eq 8) 'KD_GATE_ART_LIMIT overrides'
