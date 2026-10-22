<#
day_close.ps1 - manual end-of-day close (run on the user's "situation save" signal). No scheduler.
Default: daily tag daily/YYYY-MM-DD (skipped if it exists) + LOCAL bundle in -LocalBackup. Nothing goes to G: by default.
  -Milestone v0.2.0   (user command only) create tag if missing (never moves an existing tag), bundle it, copy to G:
  -UploadDaily        also copy today's daily bundle to G: (explicit only)
  -WhatIf             print what would happen, change nothing
Repo changes are limited to tags. Never deletes anything: over the G: limit it only lists oldest daily bundles as candidates.
#>
[CmdletBinding(SupportsShouldProcess)]
param([string]$Milestone, [switch]$UploadDaily, [string]$Ref = 'main', [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),
      [string]$LocalBackup = 'C:/works/_backup/KoreanDivision', [string]$DriveBackup = 'G:\내 드라이브\KoreanDivisionBackup',
      [double]$DriveLimitGB = 150)
$ErrorActionPreference = 'Stop'
$cd = (& git rev-parse --path-format=absolute --git-common-dir)
$Root = Split-Path $cd -Parent
function G { & git -C $Root @args }
function Hit($m) { if ($WhatIfPreference) { Write-Host "[WhatIf] $m" } else { Write-Host $m } }

if ($Milestone -and $Milestone -notmatch '^v\d+\.\d+\.\d+$') { throw "milestone tag must look like v0.2.0: $Milestone" }
G rev-parse --verify "$Ref^{commit}" *> $null; if ($LASTEXITCODE) { throw "ref not found: $Ref" }

# (a) daily tag
$daily = "daily/$Date"
if (G tag -l $daily) { Write-Host "daily tag exists, skip: $daily" }
elseif ($PSCmdlet.ShouldProcess($daily, "git tag at $Ref")) { G tag $daily $Ref; Write-Host "tagged $daily" } else { Hit "tag $daily at $Ref" }

# (b) local daily bundle (--all)
function MakeBundle([string]$file, [string[]]$refs) {
  if (Test-Path $file) { Write-Host "bundle exists, skip: $file"; return $false }
  if ($PSCmdlet.ShouldProcess($file, "git bundle create $($refs -join ' ')")) {
    $d = Split-Path $file -Parent; if (-not (Test-Path $d)) { New-Item -ItemType Directory $d | Out-Null }
    G bundle create $file @refs 2>&1 | Out-Null; if ($LASTEXITCODE) { throw "bundle failed: $file" }
    G bundle verify $file 2>&1 | Out-Null
    Write-Host ("bundle {0} ({1:N1} MB)" -f $file, ((Get-Item $file).Length / 1MB)); return $true
  } else { Hit "bundle $file ($($refs -join ' '))"; return $false }
}
$dailyFile = Join-Path $LocalBackup "daily-$Date.bundle"
MakeBundle $dailyFile @('--all') | Out-Null

function ToDrive([string]$file) {
  if (-not (Test-Path (Split-Path $DriveBackup -Qualifier))) { Write-Host "G: not available - upload skipped: $file"; return }
  if ($PSCmdlet.ShouldProcess($DriveBackup, "copy $(Split-Path $file -Leaf)")) {
    if (-not (Test-Path $DriveBackup)) { New-Item -ItemType Directory $DriveBackup | Out-Null }
    Copy-Item $file $DriveBackup -Force; Write-Host "copied to G: $(Split-Path $file -Leaf)"
  } else { Hit "copy $file -> $DriveBackup" }
}
if ($UploadDaily) { ToDrive $dailyFile }

# (c) milestone (explicit only)
if ($Milestone) {
  if (G tag -l $Milestone) { Write-Host "milestone tag exists (kept as is): $Milestone" }
  elseif ($PSCmdlet.ShouldProcess($Milestone, "git tag at $Ref")) { G tag $Milestone $Ref; Write-Host "tagged $Milestone" } else { Hit "tag $Milestone at $Ref" }
  $mf = Join-Path $LocalBackup "$Milestone.bundle"
  MakeBundle $mf @($Milestone) | Out-Null
  ToDrive $mf
}

# (d) G: size check (list only) + project folder
if (Test-Path $DriveBackup) {
  $files = @(Get-ChildItem $DriveBackup -Filter *.bundle -File)
  $gb = [math]::Round((($files | Measure-Object Length -Sum).Sum) / 1GB, 2)
  Write-Host "G: backup $gb GB / limit $DriveLimitGB GB"
  if ($gb -gt $DriveLimitGB) {
    Write-Host 'OVER LIMIT - deletion CANDIDATES (oldest daily first; nothing deleted):'
    $files | Where-Object Name -like 'daily-*' | Sort-Object Name | ForEach-Object { Write-Host ("  {0}  {1:N2} GB" -f $_.FullName, ($_.Length / 1GB)) }
  }
} else { Write-Host 'G: backup folder not present yet (created on first upload)' }
$dg = Join-Path $PSScriptRoot 'disk_guard.ps1'
if (Test-Path $dg) { & $dg -Fast }
