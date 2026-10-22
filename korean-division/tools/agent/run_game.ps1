# run_game.ps1 - launch the game and put its window on the SECONDARY monitor (primary if only one).
# Usage: powershell -File tools/agent/run_game.ps1 <KoreanDivision.exe | project dir | .csproj> [game args...]
#   exe  -> started directly;  dir/.csproj -> `dotnet run --project <p> -c Release --` (any branch build, no code changes)
# Env KD_MONITOR=primary|secondary|<index> overrides (default secondary). Exit code = game's exit code.
# No admin, never kills other processes. Window size/mode kept; only centred on the target screen.
param([Parameter(Mandatory=$true, Position=0)][string]$Target,
      [Parameter(ValueFromRemainingArguments=$true)][string[]]$GameArgs)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class KdWin {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static IntPtr Find(HashSet<uint> pids) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pids.Contains(pid) && IsWindowVisible(h) && GetWindow(h, 4) == IntPtr.Zero) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L > 100 && r.B - r.T > 100) { found = h; return false; }
      }
      return true; }, IntPtr.Zero);
    return found;
  }
}
'@

function Get-Tree([int]$root) {
    $set = New-Object 'System.Collections.Generic.HashSet[uint32]'
    [void]$set.Add([uint32]$root)
    $all = Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId
    $grew = $true
    while ($grew) { $grew = $false
        foreach ($p in $all) { if ($set.Contains([uint32]$p.ParentProcessId) -and $set.Add([uint32]$p.ProcessId)) { $grew = $true } } }
    return $set
}

# --- choose screen ---
$screens = [System.Windows.Forms.Screen]::AllScreens
$mode = if ($env:KD_MONITOR) { $env:KD_MONITOR.ToLowerInvariant() } else { 'secondary' }
$primary = $screens | Where-Object { $_.Primary } | Select-Object -First 1
$secondary = $screens | Where-Object { -not $_.Primary } | Select-Object -First 1
if ($mode -eq 'primary') { $scr = $primary }
elseif ($mode -match '^\d+$' -and [int]$mode -lt $screens.Count) { $scr = $screens[[int]$mode] }
else { $scr = if ($secondary) { $secondary } else { $primary } }
$idx = [array]::IndexOf($screens, $scr)
Write-Host ("[run_game] monitors={0} target=#{1} {2} bounds={3}" -f $screens.Count, $idx, $scr.DeviceName, $scr.Bounds)

# --- start game ---
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.UseShellExecute = $false
function Q([string]$s) { if ($s -match '[\s"]') { '"' + ($s -replace '"','\"') + '"' } else { $s } }
$argList = @()
if ((Test-Path -LiteralPath $Target -PathType Container) -or $Target -like '*.csproj') {
    $psi.FileName = 'dotnet'
    $argList += @('run', '--project', (Q (Resolve-Path -LiteralPath $Target).Path), '-c', 'Release', '--')
} else {
    $exe = (Resolve-Path -LiteralPath $Target).Path
    $psi.FileName = $exe; $psi.WorkingDirectory = Split-Path $exe
}
foreach ($a in $GameArgs) { $argList += (Q $a) }
$psi.Arguments = $argList -join ' '
$proc = [System.Diagnostics.Process]::Start($psi)

# --- wait for window, move once ---
$moved = $false; $deadline = (Get-Date).AddSeconds(90)
while (-not $proc.HasExited -and -not $moved -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 200
    $h = [KdWin]::Find((Get-Tree $proc.Id))
    if ($h -ne [IntPtr]::Zero) {
        $r = New-Object KdWin+RECT; [void][KdWin]::GetWindowRect($h, [ref]$r)
        $w = $r.R - $r.L; $ht = $r.B - $r.T; $b = $scr.Bounds
        $x = $b.X + [int](($b.Width - $w) / 2); $y = $b.Y + [int](($b.Height - $ht) / 2)
        # SWP_NOSIZE|NOZORDER|SHOWWINDOW = 0x1|0x4|0x40
        [void][KdWin]::SetWindowPos($h, [IntPtr]::Zero, $x, $y, 0, 0, 0x45)
        Start-Sleep -Milliseconds 100
        [void][KdWin]::GetWindowRect($h, [ref]$r)
        $cx = ($r.L + $r.R) / 2; $cy = ($r.T + $r.B) / 2
        $on = $screens | Where-Object { $_.Bounds.Contains([int]$cx, [int]$cy) } | Select-Object -First 1
        Write-Host ("[run_game] window {0}x{1} at ({2},{3}) -> monitor #{4} bounds={5}" -f $w, $ht, $r.L, $r.T, [array]::IndexOf($screens, $on), $on.Bounds)
        $moved = $true
    }
}
if (-not $moved -and -not $proc.HasExited) { Write-Host '[run_game] WARN: no window found within 90s; leaving game running unmoved' }
$proc.WaitForExit()
exit $proc.ExitCode
