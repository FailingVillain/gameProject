# HTML -> PDF (Edge headless). 사용: make_summary_pdf.ps1 -Html <입력.html> -Pdf <출력.pdf>
param(
  [Parameter(Mandatory=$true)][string]$Html,
  [Parameter(Mandatory=$true)][string]$Pdf
)
$ErrorActionPreference = 'Stop'
$edge = @(
  'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
  'C:\Program Files\Microsoft\Edge\Application\msedge.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'msedge.exe not found' }
$htmlFull = (Resolve-Path $Html).Path
$pdfFull  = [System.IO.Path]::GetFullPath($Pdf)
$uri = ([System.Uri]$htmlFull).AbsoluteUri
$profile = Join-Path $env:TEMP 'kd_pdf_edge_profile'
& $edge --headless=new --disable-gpu --no-pdf-header-footer "--user-data-dir=$profile" "--print-to-pdf=$pdfFull" $uri | Out-Null
$deadline = (Get-Date).AddSeconds(30)
while (-not (Test-Path $pdfFull) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
if (-not (Test-Path $pdfFull)) { throw 'PDF was not created' }
Write-Output $pdfFull
