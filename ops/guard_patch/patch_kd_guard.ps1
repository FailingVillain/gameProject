# Patches kd_guard.ps1: (1) dotnet folder is not a "protected path"; (2) approved-deletion allowlist (kd_delete_allow.txt).
# Run in an ADMIN PowerShell:  powershell -ExecutionPolicy Bypass -File C:\gameProject\ops\guard_patch\patch_kd_guard.ps1
# Test on a copy first:        ... -File ...\patch_kd_guard.ps1 -HookDir <folder containing a copy of kd_guard.ps1>
param([string]$HookDir = 'C:\Program Files\ClaudeCode\hooks')
$ErrorActionPreference = 'Stop'
$f = Join-Path $HookDir 'kd_guard.ps1'
$src = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
if ($src.Contains('DeleteAllowed')) { Write-Host 'already patched'; exit 0 }
Copy-Item $f ($f + '.bak_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))

# (1) dotnet: skip protected/outside-scope checks for the dotnet install folder (it is executed, not written)
$old1 = '    foreach ($ap in $paths) {' + "`r`n" + '      if (InNever $ap $never)'
$new1 = '    foreach ($ap in $paths) {' + "`r`n" + '      if ($ap.StartsWith(''c:/program files/dotnet'')) { continue }' + "`r`n" + '      if (InNever $ap $never)'
if (-not $src.Contains($old1)) { $old1 = $old1.Replace("`r`n", "`n"); $new1 = $new1.Replace("`r`n", "`n") }
if (-not $src.Contains($old1)) { throw 'patch point 1 not found' }
$src = $src.Replace($old1, $new1)

# (2) approved-deletion allowlist
$old2 = "if (`$cmd -match '\bgit\b[^;&|]*\b(reset"
$i2 = $src.IndexOf($old2)
if ($i2 -lt 0) { throw 'patch point 2 not found' }
$eol = $src.IndexOf("`n", $i2)
$line = $src.Substring($i2, $eol - $i2).TrimEnd("`r")
# line looks like: if ($cmd -match '...') { Block 'destructive git command' }
$tail = " { Block 'destructive git command' }"
if (-not $line.EndsWith($tail)) { throw 'unexpected destructive git line' }
$cond = $line.Substring(0, $line.Length - $tail.Length)
$newLine = $cond + " { if (-not (DeleteAllowed `$cmd)) { Block 'destructive git command (not in kd_delete_allow.txt)' } }"
$src = $src.Replace($line, $newLine)

$func = @'
# Approved deletions: kd_delete_allow.txt (admin-writable only, one target per line, lower-case, forward slashes; # = comment).
# Allowed only for ONE simple git command (no ; & | ` $ ( ) chaining) of the kinds: worktree remove/unlock, branch -d/-D, whose LAST argument equals an entry.
function DeleteAllowed([string]$c) {
  $af = Join-Path $PSScriptRoot 'kd_delete_allow.txt'
  if (-not (Test-Path $af)) { return $false }
  if ($c -match '[;&|`$()]') { return $false }
  if ($c -notmatch '^\s*git(\s+-c\s+\S+|\s+-c\S+)*(\s+-c\s+\S+)?\s+(worktree\s+(remove|unlock)|branch\s+(-d|--delete))\b') { return $false }
  $last = ($c.Trim() -split '\s+')[-1].Trim('"', "'").TrimEnd('/')
  foreach ($e in (Get-Content $af -Encoding UTF8)) {
    $t = (Norm $e).Trim().TrimEnd('/')
    if (-not $t -or $t.StartsWith('#')) { continue }
    if ($last -eq $t) { return $true }
  }
  return $false
}
'@
$marker = "try {`r`n  `$scope"
if (-not $src.Contains($marker)) { $marker = "try {`n  `$scope" }
if (-not $src.Contains($marker)) { throw 'insert point not found' }
$src = $src.Replace($marker, $func + "`r`n" + $marker)
[System.IO.File]::WriteAllText($f, $src, (New-Object System.Text.UTF8Encoding($true)))

$af = Join-Path $HookDir 'kd_delete_allow.txt'
if (-not (Test-Path $af)) {
  Set-Content -Path $af -Encoding UTF8 -Value @(
    '# Approved deletions (user). One target per line = LAST argument of the git command. Lower-case, forward slashes.',
    '# worktree path as the agent types it (relative to its cwd) or branch name. Remove a line after it is used.',
    'kd-develop/kd-perf-bench',
    'tools/perf-bench',
    'kd-develop/kd-rerender-5')
}
Write-Host 'patched OK'
