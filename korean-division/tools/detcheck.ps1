# Cross-variant determinism check: runs `KoreanDivision.exe --detcheck` under several JIT / hardware-intrinsic settings
# and verifies every variant prints the same checksum (and matches assets/data/detcheck.golden).
#   powershell -ExecutionPolicy Bypass -File tools/detcheck.ps1 [-Exe bin/main/KoreanDivision.exe] [-X86Exe path/to/x86/KoreanDivision.exe]
# -Exe default: bin/a1/KoreanDivision.exe if the bin/a1 folder exists, otherwise bin/main/KoreanDivision.exe.
# Build the exe first (any config; headless mode does not open a window). A win-x86 build can be passed with -X86Exe:
#   dotnet build -p:PlatformTarget=x86 -p:IntermediateOutputPath=obj/x86/ -p:OutputPath=bin/x86/   (needs the x86 .NET 8 runtime)
param(
    [string]$Exe = "",
    [string]$X86Exe = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if ($Exe -eq "") { $Exe = if (Test-Path "bin/a1") { "bin/a1/KoreanDivision.exe" } else { "bin/main/KoreanDivision.exe" } }

$variants = [ordered]@{
    "default"          = @{}
    "AVX2=0"           = @{ DOTNET_EnableAVX2 = "0" }
    "AVX=0"            = @{ DOTNET_EnableAVX = "0" }
    "HWIntrinsic=0"    = @{ DOTNET_EnableHWIntrinsic = "0" }
    "TieredCompilation=0" = @{ DOTNET_TieredCompilation = "0" }
    "TieredPGO=0"      = @{ DOTNET_TieredPGO = "0" }
    "ReadyToRun=0"     = @{ DOTNET_ReadyToRun = "0" }
    "TC=0+HWIntr=0"    = @{ DOTNET_TieredCompilation = "0"; DOTNET_EnableHWIntrinsic = "0" }
}
$names = @($variants.Keys)
if ($X86Exe -ne "") { $names += "win-x86" }

$results = @()
foreach ($n in $names) {
    $exe = $Exe
    $envs = @{}
    if ($n -eq "win-x86") { $exe = $X86Exe } else { $envs = $variants[$n] }
    $saved = @{}
    foreach ($k in $envs.Keys) { $saved[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $envs[$k]) }
    try {
        $out = & $exe --detcheck 2>&1 | Out-String
    } finally {
        foreach ($k in $envs.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) }
    }
    $line = ($out -split "`n" | Where-Object { $_ -like "DETCHECK *" } | Select-Object -First 1)
    $gold = ($out -split "`n" | Where-Object { $_ -like "GOLDEN*" } | Select-Object -First 1)
    if (-not $line) { $line = "(no output)"; $gold = $out.Trim() }
    $key = ($line -replace "^DETCHECK ", "").Trim()
    $results += [pscustomobject]@{ Variant = $n; Checksum = $key; Golden = ($gold -as [string]).Trim() }
    Write-Host ("{0,-22} {1}  [{2}]" -f $n, $key, ($gold -as [string]).Trim())
}
$distinct = @($results | Select-Object -ExpandProperty Checksum -Unique)
if ($distinct.Count -eq 1 -and $distinct[0] -ne "(no output)") {
    Write-Host "`nALL $($results.Count) VARIANTS IDENTICAL" -ForegroundColor Green
    exit 0
}
Write-Host "`nMISMATCH between variants" -ForegroundColor Red
exit 1
