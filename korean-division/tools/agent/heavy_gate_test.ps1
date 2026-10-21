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
Ok ((Get-HeavyLane 'python.exe' 'python rerender96.py') -eq 'render') 'rerender96=render'
Ok ($null -eq (Get-HeavyLane 'python.exe' 'python other.py')) 'other python ignored'
Ok (Test-GateOpen (C 3 3 1) 'render' 8 $false) 'render below limit passes when others full'
Ok (-not (Test-GateOpen (C 3 3 2) 'validate' 8 $false)) 'total 8 blocks'
Ok (Test-GateOpen (C 3 0 0) 'validate' 8 $false) 'validate borrows free slots'
Ok (-not (Test-GateOpen (C 3 0 0) 'validate' 8 $false @('measure'))) 'borrower yields to waiting owner'
Ok (Test-GateOpen (C 2 3 2) 'validate' 8 $false) 'validate below limit passes'
Ok (-not (Test-GateOpen (C 3 3 2) 'validate' 8 $true) -eq $false) 'priority passes at 8 (limit+3)'
Ok (-not (Test-GateOpen (C 4 4 3) 'validate' 8 $true)) 'priority blocked at 11'
if ($fail) { "FAILED $fail"; exit 1 } else { 'ALL PASS' }
