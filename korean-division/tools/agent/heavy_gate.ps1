# Heavy-job gate: waits until fewer than 8 heavy processes run (builds, game sims, renders).
# Usage (PowerShell): powershell -ExecutionPolicy Bypass -File <this> ; then start your heavy step.
param([int]$Max = 8, [int]$TimeoutMin = 60, [switch]$Render, [switch]$Priority)
# One slot is reserved for unit re-renders (user priority 2026-10-22): non-render callers see Max-1.
if (-not $Render) { $Max = $Max - 1 }
# Merge-lock holder: runs ahead of the queue (priority inversion fix, user 2026-10-22).
if ($Priority) { $Max = $Max + 3 }
$deadline = (Get-Date).AddMinutes($TimeoutMin)
while ($true) {
  $h = @(Get-CimInstance Win32_Process | Where-Object {
    ($_.Name -eq 'KoreanDivision.exe' -and $_.CommandLine -notmatch '\\dist\\') -or
    ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'build|KoreanDivision\.dll|MSBuild\.dll') -or
    ($_.Name -eq 'python.exe' -and $_.CommandLine -match 'make_unit') })
  if ($h.Count -lt $Max) { "gate open: heavy=$($h.Count)"; exit 0 }
  if ((Get-Date) -gt $deadline) { "gate timeout: heavy=$($h.Count) - proceed carefully"; exit 1 }
  Start-Sleep -Seconds 20
}
