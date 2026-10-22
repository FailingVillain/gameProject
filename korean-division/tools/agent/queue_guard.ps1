# queue_guard.ps1 - work branches must not edit ops/QUEUE.md, ops/STATE.md, ops/MERGE_QUEUE.md (PIPELINE.md rule; PowerShell 5.1).
# ASCII-only source. Audit C-3/C-7. NOT a git hook (hooks are not installed): called by preflight.ps1 and merge_step.ps1.
#
#   queue_guard.ps1 [-Branch <ref>] [-Base main] [-Top <dir>] [-Quiet]
#     Files checked = `git diff --name-only <merge-base(Base, Branch)>..<Branch>` (default Branch = HEAD, plus uncommitted
#     changes when Branch is HEAD). Exit 0 = clean. Exit 1 = a forbidden file changed (names printed, one per line, as
#     "QUEUE_GUARD: <file>"). The merge line may then keep its own copy ('ours') - this script only reports.
[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$Branch = 'HEAD',
    [string]$Base = 'main',
    [string]$Top = '',
    [switch]$Quiet
)
$ErrorActionPreference = 'Continue'   # native git probes write to stderr; failures are checked explicitly
if ($Top -eq '') { $Top = (& git rev-parse --show-toplevel).Trim() }
$mb = (& git -C $Top merge-base $Base $Branch 2>$null)
if (-not $mb) { Write-Host "QUEUE_GUARD: cannot find merge-base of $Base and $Branch (skipped)"; exit 0 }
$files = @(& git -C $Top diff --name-only ($mb.Trim() + '..' + $Branch))
if ($Branch -eq 'HEAD') { $files += @(& git -C $Top status --porcelain --untracked-files=all | ForEach-Object { $_.Substring(3).Trim('"') }) }
$bad = @($files | Where-Object { $_ -match '(^|/)ops/(QUEUE|STATE|MERGE_QUEUE)\.md$' } | Sort-Object -Unique)
if ($bad.Count -eq 0) { if (-not $Quiet) { Write-Host "QUEUE_GUARD ok: $Branch does not touch QUEUE/STATE/MERGE_QUEUE" }; exit 0 }
foreach ($b in $bad) { Write-Host "QUEUE_GUARD: $b" }
Write-Host "QUEUE_GUARD FAIL: $Branch changes $($bad.Count) conductor-owned file(s); work agents must not edit them (revert those files on the branch)"
exit 1
