# roadmap_check.ps1 - "does this already exist / is it already planned?" check BEFORE adding a roadmap item or card (PowerShell 5.1).
# UTF-8 with BOM.
#   roadmap_check.ps1 -Keywords "날씨","weather" [-Card T58]
# Sources: (1) ROADMAP_SHORT/LONG  (2) legacy docs  (3) ops docs  (4) docs/specs + tasks of every ref  (5) code (src, assets/data) of merge line refs
# Output <= 40 lines (last line is always the marker 'ROADMAP HITS' or 'ROADMAP NONE'): "source . file:line . summary" + conclusion. Exit 1 when any hit (= "already exists / related candidate"), 0 when none.
#   -Top <dir>        repo top (default: main worktree via git common dir; docs/ops are read from there)
#   -RefTimeSec <n>   time cap for the all-refs docs/tasks search (default 40; recent refs first)
#   -Card <id>        the card being drafted: its own tasks/<id>-*.md file is excluded from hits
#   -NoRefs           skip the all-refs search (source 4)
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)][string[]]$Keywords,
    [string]$Card = '',
    [string]$Top = '',
    [int]$RefTimeSec = 40,
    [switch]$NoRefs,
    [string[]]$CodeRefs = @('merge/0.2-final', 'merge/0.2', 'merge/0.2-ui', 'main')
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if ($Top -eq '') {
    $Top = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
    $cd = (& git -C $Top rev-parse --git-common-dir 2>$null)
    if ($cd) {
        if (-not [System.IO.Path]::IsPathRooted($cd)) { $cd = Join-Path $Top $cd }
        $Top = Split-Path -Parent (Resolve-Path $cd).Path
    }
}
$Top = (Resolve-Path $Top).Path
$utf8 = New-Object System.Text.UTF8Encoding($false)
$KD = Join-Path $Top 'korean-division'

# keyword list + identifier variants (multi-word -> joined / underscore forms; case-insensitive anyway)
$kws = @()
foreach ($k in $Keywords) {
    foreach ($p in ($k -split ',')) {
        $p = $p.Trim(); if ($p -eq '') { continue }
        $kws += $p
        if ($p -match '[\s_-]') { $kws += ($p -replace '[\s_-]+', ''); $kws += ($p -replace '[\s-]+', '_') }
    }
}
$kws = @($kws | Select-Object -Unique)
if ($kws.Count -eq 0) { Write-Host 'roadmap_check: no keywords'; exit 2 }

function Short([string]$s, [int]$n = 90) { $s = ($s -replace '\s+', ' ').Trim(); if ($s.Length -gt $n) { return $s.Substring(0, $n - 3) + '...' } return $s }
function Rel([string]$p) { if ($p.StartsWith($Top)) { return $p.Substring($Top.Length).TrimStart('\', '/').Replace('\', '/') } return $p.Replace('\', '/') }
function Grep-Files([string[]]$files) {
    $hits = New-Object System.Collections.ArrayList
    foreach ($f in $files) {
        if (-not (Test-Path -LiteralPath $f -PathType Leaf)) { continue }
        if ($Card -ne '' -and (Split-Path -Leaf $f) -like ($Card + '-*')) { continue }
        $lines = $null
        try { $lines = [System.IO.File]::ReadAllLines($f, $utf8) } catch { continue }
        for ($i = 0; $i -lt $lines.Length; $i++) {
            foreach ($k in $kws) {
                if ($lines[$i].IndexOf($k, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    [void]$hits.Add([pscustomobject]@{ Where = ((Rel $f) + ':' + ($i + 1)); Text = (Short $lines[$i]) }); break
                }
            }
        }
    }
    return $hits
}
function Run-Git([string[]]$GitArgs, [int]$timeoutMs) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'git'
    $psi.Arguments = ($GitArgs | ForEach-Object { if ($_ -match '[\s"]' -or $_ -eq '') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' '
    $psi.UseShellExecute = $false; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = $utf8; $psi.CreateNoWindow = $true; $psi.WorkingDirectory = $Top
    $p = [System.Diagnostics.Process]::Start($psi)
    $o = $p.StandardOutput.ReadToEndAsync(); $null = $p.StandardError.ReadToEndAsync()
    $timedOut = $false
    if (-not $p.WaitForExit($timeoutMs)) { $timedOut = $true; try { $p.Kill() } catch { } }
    $text = ''
    if (-not $timedOut) { $text = $o.Result } else { try { $text = $o.Result } catch { } }
    return [pscustomobject]@{ Lines = @($text -split "`n" | Where-Object { $_ -ne '' } | ForEach-Object { $_.TrimEnd("`r") }); TimedOut = $timedOut }
}
# git grep over several refs; collapse identical (path:line text) across refs -> "(+N refs)"
function Grep-Refs([string[]]$refs, [string[]]$paths, [int]$timeoutMs, [switch]$ExcludeCard) {
    $collapsed = [ordered]@{}; $timedOut = $false
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    $chunk = 25
    for ($i = 0; $i -lt $refs.Count; $i += $chunk) {
        $left = [int](($deadline - (Get-Date)).TotalMilliseconds)
        if ($left -lt 500) { $timedOut = $true; break }
        $sub = $refs[$i..([Math]::Min($i + $chunk, $refs.Count) - 1)]
        $a = @('-c', 'core.quotepath=false', 'grep', '-I', '-n', '-i', '-F')
        foreach ($k in $kws) { $a += '-e'; $a += $k }
        $a += $sub; $a += '--'; $a += $paths
        $r = Run-Git $a $left
        if ($r.TimedOut) { $timedOut = $true }
        foreach ($l in $r.Lines) {
            # "<ref>:<path>:<line>:<text>"; ref may contain '/', path starts with korean-division/
            if ($l -match '^(.+?):(korean-division/[^:]+):(\d+):(.*)$') {
                $ref = $Matches[1]; $path = $Matches[2]; $ln = $Matches[3]; $tx = $Matches[4]
                if ($Card -ne '' -and (($path -split '/')[-1]) -like ($Card + '-*')) { continue }
                $key = $path + ':' + $ln + ':' + $tx.Trim()
                if (-not $collapsed.Contains($key)) { $collapsed[$key] = [pscustomobject]@{ Ref = $ref; Path = $path; Ln = $ln; Text = (Short $tx); N = 1 } }
                else { $collapsed[$key].N++ }
            }
        }
        if ($r.TimedOut) { break }
    }
    return [pscustomobject]@{ Hits = @($collapsed.Values); TimedOut = $timedOut }
}
function Existing-Refs([string[]]$names) {
    $all = @(& git -C $Top for-each-ref --format='%(refname:short)' refs/heads refs/remotes 2>$null)
    return @($names | Where-Object { $all -contains $_ })
}

$out = New-Object System.Collections.ArrayList
$docHits = 0; $codeHits = 0
function Add-Section([string]$title, $items, [int]$cap, [string]$note = '') {
    $n = @($items).Count
    [void]$out.Add(('[{0}] {1} hit(s){2}' -f $title, $n, $note))
    $shown = 0
    foreach ($it in $items) { if ($shown -ge $cap) { break }; [void]$out.Add('  ' + $it); $shown++ }
    if ($n -gt $cap) { $out[$out.Count - 1] = $out[$out.Count - 1] + ('  (+{0} more)' -f ($n - $cap)) }
}

if ($Card -ne '') { [void]$out.Add("roadmap_check card=$Card keywords: $($kws -join ', ')") }
# (1) roadmaps
$f1 = @('ROADMAP_SHORT.md', 'ROADMAP_LONG.md') | ForEach-Object { Join-Path $KD ('docs/' + $_) }
$h1 = @(Grep-Files $f1)
Add-Section '1 roadmap' @($h1 | ForEach-Object { 'roadmap . {0} . {1}' -f $_.Where, $_.Text }) 6

# (2) legacy
$leg = New-Object System.Collections.ArrayList
$legDir = Join-Path $KD 'docs/legacy'
if (Test-Path $legDir) { Get-ChildItem -Path $legDir -Recurse -File -Include *.md, *.txt | ForEach-Object { [void]$leg.Add($_.FullName) } }
Get-ChildItem -Path (Join-Path $KD 'docs') -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '(?i)legacy|archive' -and $_.Extension -in '.md', '.txt' } | ForEach-Object { [void]$leg.Add($_.FullName) }
Get-ChildItem -Path (Join-Path $Top 'ops') -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '(?i)legacy|archive' } | ForEach-Object { [void]$leg.Add($_.FullName) }
foreach ($n in 'PROJECT_NOTES.md', 'HANDOFF.md') { [void]$leg.Add((Join-Path $KD ('docs/' + $n))) }
$h2 = @(Grep-Files @($leg | Select-Object -Unique))
Add-Section '2 legacy' @($h2 | ForEach-Object { 'legacy . {0} . {1}' -f $_.Where, $_.Text }) 6

# (3) ops
$f3 = 'QUEUE.md', 'CARD_INDEX.md', 'MERGE_QUEUE.md', 'DECISIONS.md', 'REPORT_2026-10-22.md', 'FOLLOWUPS.md' | ForEach-Object { Join-Path $Top ('ops/' + $_) }
$h3 = @(Grep-Files $f3)
Add-Section '3 ops' @($h3 | ForEach-Object { 'ops . {0} . {1}' -f $_.Where, $_.Text }) 6

# (4) docs/specs + tasks of every ref (dedupe refs with identical docs+tasks trees, most recent first)
$h4 = @(); $note4 = ''
if (-not $NoRefs) {
    $refInfo = @(& git -C $Top for-each-ref --sort=-committerdate --format='%(refname)' refs/heads refs/remotes 2>$null)
    $refInfo = @($refInfo | Where-Object { $_ -notmatch '^refs/remotes/.*/HEAD$' })
    $uniq = @(); $seenKey = @{}; $nameTrees = @{}
    if ($refInfo.Count -gt 0) {
        $inp = New-Object System.Text.StringBuilder
        foreach ($r in $refInfo) { [void]$inp.Append($r + ':korean-division/docs/specs' + "`n"); [void]$inp.Append($r + ':korean-division/tasks' + "`n") }
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'git'; $psi.Arguments = 'cat-file --batch-check'; $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
        $psi.StandardOutputEncoding = $utf8; $psi.CreateNoWindow = $true; $psi.WorkingDirectory = $Top
        $p = [System.Diagnostics.Process]::Start($psi)
        $o = $p.StandardOutput.ReadToEndAsync(); $null = $p.StandardError.ReadToEndAsync()
        $b = $utf8.GetBytes($inp.ToString()); $p.StandardInput.BaseStream.Write($b, 0, $b.Length); $p.StandardInput.Close()
        $p.WaitForExit(); $res = @($o.Result -split "`n" | Where-Object { $_ -ne '' })
        for ($i = 0; $i -lt $refInfo.Count; $i++) {
            $a = if (2 * $i -lt $res.Count) { ($res[2 * $i] -split ' ')[0] } else { '' }
            $t = if (2 * $i + 1 -lt $res.Count) { ($res[2 * $i + 1] -split ' ')[0] } else { '' }
            $key = $a + '/' + $t
            if (-not $seenKey.ContainsKey($key)) {
                $seenKey[$key] = $true; $short = ($refInfo[$i] -replace '^refs/(heads|remotes)/', ''); $uniq += $short
                if ($a -match '^[0-9a-f]{40}$' -and -not $nameTrees.ContainsKey($a)) { $nameTrees[$a] = @($short, 'docs/specs') }
                if ($t -match '^[0-9a-f]{40}$' -and -not $nameTrees.ContainsKey($t)) { $nameTrees[$t] = @($short, 'tasks') }
            }
        }
    }
    $g = Grep-Refs $uniq @('korean-division/docs/specs', 'korean-division/tasks') ($RefTimeSec * 1000)
    $h4 = @($g.Hits)
    # file-name matches (card slugs, spec names): parse the tree objects in one cat-file --batch
    if ($nameTrees.Count -gt 0) {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'git'; $psi.Arguments = 'cat-file --batch'; $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true; $psi.WorkingDirectory = $Top
        $p = [System.Diagnostics.Process]::Start($psi)
        $ms = New-Object System.IO.MemoryStream; $cp = $p.StandardOutput.BaseStream.CopyToAsync($ms); $null = $p.StandardError.ReadToEndAsync()
        $b = $utf8.GetBytes((($nameTrees.Keys) -join "`n") + "`n"); $p.StandardInput.BaseStream.Write($b, 0, $b.Length); $p.StandardInput.Close()
        $cp.Wait(); $p.WaitForExit(); $raw = $ms.ToArray(); $pos = 0
        while ($pos -lt $raw.Length) {
            $nl = [Array]::IndexOf($raw, [byte]10, $pos); if ($nl -lt 0) { break }
            $hdr = $utf8.GetString($raw, $pos, $nl - $pos).Split(' '); $pos = $nl + 1
            if ($hdr.Count -lt 3 -or $hdr[1] -ne 'tree') { if ($hdr.Count -ge 3 -and $hdr[2] -match '^\d+$') { $pos += [int]$hdr[2] + 1 }; continue }
            $end = $pos + [int]$hdr[2]; $info = $nameTrees[$hdr[0]]
            while ($pos -lt $end) {
                $sp = [Array]::IndexOf($raw, [byte]32, $pos); $z = [Array]::IndexOf($raw, [byte]0, $sp)
                $name = $utf8.GetString($raw, $sp + 1, $z - $sp - 1); $pos = $z + 21
                if ($Card -ne '' -and $name -like ($Card + '-*')) { continue }
                foreach ($k in $kws) { if ($name.IndexOf($k, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $h4 += [pscustomobject]@{ Ref = $info[0]; Path = ('korean-division/' + $info[1] + '/' + $name); Ln = 'filename'; Text = '(file name matches)'; N = 1 }; break } }
            }
            $pos = $end + 1
        }
    }
    $note4 = ' ({0} distinct refs{1})' -f $uniq.Count, $(if ($g.TimedOut) { ', TIME CAP - partial, newest refs first' } else { '' })
}
Add-Section '4 refs docs/specs+tasks' @($h4 | ForEach-Object { 'refs . {0}:{1}:{2}{3} . {4}' -f $_.Ref, ($_.Path -replace '^korean-division/', ''), $_.Ln, $(if ($_.N -gt 1) { " (+$($_.N - 1) refs)" } else { '' }), $_.Text }) 7 $note4

# (5) code
$cr = @(Existing-Refs $CodeRefs)
$h5 = @(); $note5 = ''
if ($cr.Count -gt 0) {
    $g = Grep-Refs $cr @('korean-division/src', 'korean-division/assets/data') 60000
    $h5 = @($g.Hits)
    $note5 = ' (refs: {0}{1})' -f ($cr -join ','), $(if ($g.TimedOut) { ', TIME CAP' } else { '' })
} else {
    $note5 = ' (no merge-line ref found; searched working tree)'
    $files = @(); foreach ($d in 'src', 'assets/data') { $dd = Join-Path $KD $d; if (Test-Path $dd) { $files += (Get-ChildItem -Path $dd -Recurse -File -Include *.cs, *.json | ForEach-Object { $_.FullName }) } }
    $h5 = @(Grep-Files $files | ForEach-Object { [pscustomobject]@{ Ref = 'worktree'; Path = $_.Where; Ln = ''; Text = $_.Text; N = 1 } })
}
# one line per file (name matches first), with line count
$byFile = [ordered]@{}
foreach ($h in $h5) { if (-not $byFile.Contains($h.Path)) { $byFile[$h.Path] = [pscustomobject]@{ Ref = $h.Ref; Path = $h.Path; Ln = $h.Ln; Text = $h.Text; Lines = 0 } }; $byFile[$h.Path].Lines++ }
$fileList = @($byFile.Values | Sort-Object @{ Expression = { $leaf = (($_.Path -split '/')[-1]); $m = 0; foreach ($k in $kws) { if ($leaf.IndexOf($k, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $m = 1 } }; -$m } })
Add-Section '5 code src+assets/data' @($fileList | ForEach-Object { 'code . {0}:{1}:{2} ({3} lines) . {4}' -f $_.Ref, ($_.Path -replace '^korean-division/', ''), $_.Ln, $_.Lines, $_.Text }) 9 ($note5 + (' {0} files' -f $fileList.Count))
$docHits = $h1.Count + $h2.Count + $h3.Count + $h4.Count
$codeHits = $h5.Count
$concl = if ($codeHits -gt 0) { '결론: 구현 존재 가능 - 이미 있음/관련 있음 후보 (코드 {0}건, 문서 {1}건). 추가 전에 위 코드를 읽고 카드 머리말 ''중복 점검'' 줄에 기록' -f $codeHits, $docHits }
          elseif ($docHits -gt 0) { '결론: 문서 계획만 - 이미 있음/관련 있음 후보 (문서 {0}건, 코드 히트 없음). 해당 계획과 합칠지 확인' -f $docHits }
          else { '결론: 히트 없음 (키워드: {0})' -f ($kws -join ', ') }
[void]$out.Add($concl)
# enforce <= 40 lines (drop trailing example lines, never the section headers / conclusion)
while ($out.Count -gt 39) {
    $idx = -1; for ($i = $out.Count - 2; $i -ge 0; $i--) { if ($out[$i].StartsWith('  ') -and -not $out[$i].StartsWith('  (')) { $idx = $i; break } }
    if ($idx -lt 0) { break }; $out.RemoveAt($idx)
}
foreach ($l in $out) { Write-Host $l }
# last line = machine marker for dispatch_guard / dispatch.ps1 (DISPATCH spec section 5/8)
if (($docHits + $codeHits) -gt 0) { Write-Host 'ROADMAP HITS'; exit 1 }
Write-Host 'ROADMAP NONE'
exit 0
