# Unit test for heavy_gate.ps1 lane classification and gate decisions.
. "$PSScriptRoot\heavy_gate.ps1"
$fail = 0
function Ok($cond, $msg) { if (-not $cond) { "FAIL: $msg"; $script:fail++ } else { "ok: $msg" } }
function C($v, $m, $r) { @{ validate = $v; measure = $m; render = $r } }
Ok ((Get-HeavyLane 'KoreanDivision.exe' 'x --selftest') -eq 'validate') 'selftest=validate'
Ok ((Get-HeavyLane 'KoreanDivision.exe' 'x --detcheck') -eq 'validate') 'detcheck=validate'
Ok ((Get-HeavyLane 'KoreanDivision.exe' 'x --tactics-obs a') -eq 'measure') 'tactics-obs=measure'
Ok ((Get-HeavyLane 'KoreanDivision.exe' 'x --bunch') -eq 'measure') 'bunch=measure'
Ok ($null -eq (Get-HeavyLane 'KoreanDivision.exe' 'C:\a\dist\KoreanDivision.exe --stress')) 'dist excluded'
Ok ((Get-HeavyLane 'dotnet.exe' 'dotnet build x.csproj') -eq 'validate') 'dotnet build=validate'
Ok ((Get-HeavyLane 'dotnet.exe' 'dotnet KoreanDivision.dll --collide') -eq 'measure') 'dll collide=measure'
Ok ((Get-HeavyLane 'python.exe' 'python make_unit.py') -eq 'render') 'make_unit=render'
Ok ($null -eq (Get-HeavyLane 'python.exe' 'python rerender96.py')) 'rerender96 driver is not heavy (child make_unit is)'
Ok ($null -eq (Get-HeavyLane 'python.exe' 'python other.py')) 'other python ignored'
Ok (Test-GateOpen (C 3 3 1) 'render' 8 $false) 'render below limit passes when others full'
Ok (-not (Test-GateOpen (C 3 3 2) 'validate' 8 $false)) 'total 8 blocks'
Ok (Test-GateOpen (C 3 0 0) 'validate' 8 $false) 'validate borrows free slots'
Ok (-not (Test-GateOpen (C 3 0 0) 'validate' 8 $false @('measure'))) 'borrower yields to waiting owner'
Ok (Test-GateOpen (C 2 3 2) 'validate' 8 $false) 'validate below limit passes'
Ok (-not (Test-GateOpen (C 3 3 2) 'validate' 8 $true) -eq $false) 'priority passes at 8 (limit+3)'
Ok (-not (Test-GateOpen (C 4 4 3) 'validate' 8 $true)) 'priority blocked at 11'
# RAM-dependent limit via KD_GATE_FREEGB_OVERRIDE
Ok ((Get-GateLimit 5) -eq 10) 'limit 10 at 5 GB'
Ok ((Get-GateLimit 7) -eq 12) 'limit 12 at 7 GB'
Ok ((Get-GateLimit 6) -eq 12) 'limit 12 at exactly 6 GB'
Ok ((Get-GateLimit 7 8) -eq 8) 'fixed -Max overrides'
$env:KD_GATE_FREEGB_OVERRIDE = '7'; Ok ((Get-FreeGB) -eq 7) 'env override read'
Ok (Test-GateOpen (C 3 3 2) 'validate' (Get-GateLimit (Get-FreeGB)) $false) 'open at total 8 with limit 12'
$env:KD_GATE_FREEGB_OVERRIDE = '5'
Ok (-not (Test-GateOpen (C 4 4 2) 'validate' (Get-GateLimit (Get-FreeGB)) $false)) 'blocked at total 10 with limit 10'
Ok (Test-GateOpen (C 4 4 1) 'render' (Get-GateLimit (Get-FreeGB)) $false) 'render lane guarantee (below 2) passes at 9/10'
# end-to-end script runs (status line + closed gate)
$gate = "$PSScriptRoot\heavy_gate.ps1"
$env:KD_GATE_FREEGB_OVERRIDE = '5'; $o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutMin 1 | Out-String)
Ok ($o -match 'gate open: heavy=\d+/limit 10 free=5(\.0)? GB') "status 5GB: $($o.Trim())"
$env:KD_GATE_FREEGB_OVERRIDE = '7'; $o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutMin 1 | Out-String)
Ok ($o -match 'gate open: heavy=\d+/limit 12 free=7(\.0)? GB') "status 7GB: $($o.Trim())"
$env:KD_GATE_FREEGB_OVERRIDE = '2.5'; $o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutMin 0 | Out-String)
Ok ($o -match 'gate timeout: free RAM 2.5 GB') "closed at 2.5GB: $($o.Trim())"
# memory budget (default 14 GB, env KD_GATE_BUDGET_GB / -BudgetGB)
Ok ((Get-BudgetGB) -eq 14) 'budget default 14 GB'
Ok ((Get-BudgetGB 20) -eq 20) 'fixed budget overrides'
Ok (-not (Test-BudgetOk 15 14)) 'budget: 15 GB heavy RAM closes'
Ok (Test-BudgetOk 13 14) 'budget: 13 GB heavy RAM open'
Ok (Test-BudgetOk 14 14) 'budget: exactly 14 GB open'
$env:KD_GATE_FREEGB_OVERRIDE = '7'; $env:KD_GATE_HEAVYRAM_OVERRIDE = '15'
$o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutMin 0 | Out-String)
Ok ($o -match 'gate timeout: .*heavyRAM=15 GB') "heavy RAM 15GB closed: $($o.Trim())"
$env:KD_GATE_HEAVYRAM_OVERRIDE = '13'
$o = (& powershell -NoProfile -ExecutionPolicy Bypass -File $gate -TimeoutMin 1 | Out-String)
Ok ($o -match 'gate open: heavy=\d+/limit 12 free=7(\.0)? GB heavyRAM=13(\.0)? GB') "heavy RAM 13GB open: $($o.Trim())"
Remove-Item Env:KD_GATE_HEAVYRAM_OVERRIDE
Remove-Item Env:KD_GATE_FREEGB_OVERRIDE
if ($fail) { "FAILED $fail"; exit 1 } else { 'ALL PASS' }
