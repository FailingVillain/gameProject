# 배포 빌드:  powershell -ExecutionPolicy Bypass -File publish.ps1   →   dist\KoreanDivision.exe (단일 파일, .NET 설치 불필요)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
dotnet publish KoreanDivision.csproj -c Release -o dist

# 증분 복사가 파일을 빠뜨린 적이 있다 (2026-10-19: 경기관총 LOD0 시트 · 환경음 루프 누락 → 최대 줌에서 사라짐).
# assets 와 dist\assets 를 비교해 빠지거나 크기가 다른 파일은 직접 복사하고, 그래도 다르면 실패.
$root = (Resolve-Path assets).Path
$fixed = 0
Get-ChildItem assets -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($root.Length + 1)
    $dst = Join-Path "dist\assets" $rel
    if (-not (Test-Path $dst) -or (Get-Item $dst).Length -ne $_.Length) {
        New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
        Copy-Item $_.FullName $dst -Force
        $fixed++
        Write-Host "보충: $rel"
    }
}
$bad = @(Get-ChildItem assets -Recurse -File | Where-Object {
    $d = Join-Path "dist\assets" $_.FullName.Substring($root.Length + 1)
    -not (Test-Path $d) -or (Get-Item $d).Length -ne $_.Length })
if ($bad.Count -gt 0) { throw "dist\assets 불일치 $($bad.Count)개 — 배포 실패" }
Write-Host "assets 검사: 일치 (보충 $fixed 개)"
Write-Host "완료: $PSScriptRoot\dist\KoreanDivision.exe"
