# Self-contained FOLDER publish (win-x64, .NET runtime included; no installed .NET needed to run).
#   powershell -ExecutionPolicy Bypass -File tools/publish_selfcontained.ps1 [-Out dist-sc] [-ConsoleExe] [-Verify]
# - Output: <Out>\KoreanDivision.exe + runtime dlls + assets\  (folder form, NOT single-file: assets/replays live next to the exe)
# - No trimming (JSON loading uses reflection). No installer. ReadyToRun on by default (-NoR2R to turn off).
# - -ConsoleExe : build OutputType=Exe (console window) instead of WinExe, handy for reading --selftest output directly.
# - -Verify     : run `--detcheck` on the published exe and compare against assets/data/detcheck.golden (first hash).
# Does not touch dist\ (publish.ps1, single-file) or bin\.
param(
    [string]$Out = "dist-sc",
    [switch]$ConsoleExe,
    [switch]$NoR2R,
    [switch]$Verify
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }

$args2 = @("publish", "KoreanDivision.csproj", "-c", "Release", "-r", "win-x64", "--self-contained", "true",
    "-p:PublishSingleFile=false", "-p:PublishTrimmed=false", "-p:IncludeNativeLibrariesForSelfExtract=false",
    "-p:PublishReadyToRun=$(if ($NoR2R) { 'false' } else { 'true' })",
    "-p:IntermediateOutputPath=obj/sc/", "-o", $Out)
if ($ConsoleExe) { $args2 += "-p:OutputType=Exe" }
& dotnet @args2
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# assets integrity (same check as publish.ps1)
$ar = (Resolve-Path assets).Path
$bad = @(Get-ChildItem assets -Recurse -File | Where-Object {
    $d = Join-Path "$Out\assets" $_.FullName.Substring($ar.Length + 1)
    -not (Test-Path $d) -or (Get-Item $d).Length -ne $_.Length })
if ($bad.Count -gt 0) {
    foreach ($f in $bad) {
        $rel = $f.FullName.Substring($ar.Length + 1); $dst = Join-Path "$Out\assets" $rel
        New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null; Copy-Item $f.FullName $dst -Force
    }
    $bad2 = @(Get-ChildItem assets -Recurse -File | Where-Object {
        $d = Join-Path "$Out\assets" $_.FullName.Substring($ar.Length + 1)
        -not (Test-Path $d) -or (Get-Item $d).Length -ne $_.Length })
    if ($bad2.Count -gt 0) { throw "$Out\assets mismatch $($bad2.Count) files" }
    Write-Host "assets: filled $($bad.Count) missing files"
}
Write-Host "assets check: OK"

$all = Get-ChildItem $Out -Recurse -File
$tot = ($all | Measure-Object Length -Sum).Sum
$asset = (Get-ChildItem "$Out\assets" -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("size: total {0:N1} MB (assets {1:N1} MB, runtime+app {2:N1} MB), {3} files" -f ($tot/1MB), ($asset/1MB), (($tot-$asset)/1MB), $all.Count)

if ($Verify) {
    $exe = Join-Path $Out "KoreanDivision.exe"
    $o = (& $exe --detcheck | Out-String).Trim()
    $golden = (Get-Content assets/data/detcheck.golden -Raw).Trim().Split(" ")[0]
    Write-Host "detcheck: $o"
    if ($o -notmatch $golden) { throw "detcheck != golden $golden" }
    Write-Host "detcheck: matches golden"
}
Write-Host "done: $root\$Out\KoreanDivision.exe"
