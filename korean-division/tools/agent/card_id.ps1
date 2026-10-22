# card_id.ps1 - card id allocator / checker (PowerShell 5.1). UTF-8 with BOM.
#   card_id.ps1 -Prefix T -Count 3 -Title "..."            reserve next 3 free numbers (T58 T59 T60), print them
#   card_id.ps1 -Prefix T -Count 3 -Series -Title "..."    reserve ONE number with letters (T58a T58b T58c)
#   card_id.ps1 -Id T58x -Title "..."                       reserve an explicit id (rejected if already used)
#   card_id.ps1 -Check T49a                                 report only
# Used ids = ops/CARD_REGISTRY.md  +  card files (Tnn-slug.md) in korean-division/tasks of EVERY ref and EVERY worktree
#            +  first-column ids of ops/QUEUE.md and ops/CARD_INDEX.md. Newly seen ids are appended to the registry (cache).
# Exit codes: reserve: 0 ok, 2 rejected (id already used), 4 lock timeout.
#             -Check:  0 FREE, 3 USED (one owner; fine for an existing card), 1 CONFLICT (>1 distinct card files share the id).
# Concurrency: an exclusive lock on ops/CARD_REGISTRY.md.lock is held across scan + append (OS releases it if the process dies).
#   -Top <dir>  repo top (default: main worktree via git common dir, so the registry is shared by all worktrees)
[CmdletBinding()]
param(
    [string]$Prefix = '',
    [int]$Count = 1,
    [string]$Title = '',
    [string]$Id = '',
    [string]$Check = '',
    [switch]$Series,
    [string]$Who = '',
    [string]$Branch = '',
    [string]$Top = '',
    [int]$LockSec = 60
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
$Ops = Join-Path $Top 'ops'
$RegPath = Join-Path $Ops 'CARD_REGISTRY.md'
$LockPath = Join-Path $Ops 'CARD_REGISTRY.md.lock'
$TasksRel = 'korean-division/tasks'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$Sep = [string][char]0x00B7   # middle dot

function Get-CardId([string]$file) {
    if ($file -match '^(T\d+[A-Za-z0-9]*)-') { return $Matches[1] }
    if ($file -match '^([A-Z]+\d*[A-Z0-9]*-[A-Za-z]\d?)-') { return $Matches[1] }
    if ($file -match '^([A-Z]+\d+)-') { return $Matches[1] }
    return $null
}
function Get-Slug([string]$file, [string]$cid) {
    $s = [System.IO.Path]::GetFileNameWithoutExtension($file)
    if ($cid -and $s.StartsWith($cid + '-')) { $s = $s.Substring($cid.Length + 1) }
    return $s
}
function Invoke-GitBytes([string[]]$GitArgs, [string]$stdin) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'git'
    $psi.Arguments = ($GitArgs | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' '
    $psi.UseShellExecute = $false; $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true; $psi.WorkingDirectory = $Top
    $p = [System.Diagnostics.Process]::Start($psi)
    $ms = New-Object System.IO.MemoryStream
    $copy = $p.StandardOutput.BaseStream.CopyToAsync($ms)
    $null = $p.StandardError.ReadToEndAsync()
    $in = $utf8.GetBytes($stdin)
    $p.StandardInput.BaseStream.Write($in, 0, $in.Length); $p.StandardInput.BaseStream.Flush(); $p.StandardInput.Close()
    $copy.Wait(); $p.WaitForExit()
    return ,$ms.ToArray()
}
function Add-Seen($map, [string]$cid, [string]$file, [string]$where) {
    $k = $cid.ToUpper()
    if (-not $map.ContainsKey($k)) { $map[$k] = @{ Id = $cid; Files = @{}; Where = $where } }
    if ($file -ne '') { $map[$k].Files[$file] = $true }
}

# ---- scan (refs via cat-file --batch: no per-ref process) ----
function Scan-Cards {
    $seen = @{}
    $refs = @(& git -C $Top for-each-ref --format='%(refname)' 2>$null | Where-Object { $_ -notmatch '^refs/stash' })
    if ($refs.Count -gt 0) {
        $inp = (($refs | ForEach-Object { $_ + ':' + $TasksRel }) -join "`n") + "`n"
        $raw = Invoke-GitBytes @('cat-file', '--batch-check') $inp
        $lines = $utf8.GetString($raw) -split "`n" | Where-Object { $_ -ne '' }
        $treeRefs = @{}   # tree sha -> first ref short name
        for ($i = 0; $i -lt $lines.Count -and $i -lt $refs.Count; $i++) {
            if ($lines[$i] -match '^([0-9a-f]{40}) tree ') {
                $sha = $Matches[1]
                if (-not $treeRefs.ContainsKey($sha)) { $treeRefs[$sha] = ($refs[$i] -replace '^refs/(heads|remotes|tags)/', '') }
            }
        }
        if ($treeRefs.Count -gt 0) {
            $raw = Invoke-GitBytes @('cat-file', '--batch') ((($treeRefs.Keys) -join "`n") + "`n")
            $pos = 0
            while ($pos -lt $raw.Length) {
                $nl = [Array]::IndexOf($raw, [byte]10, $pos)
                if ($nl -lt 0) { break }
                $hdr = $utf8.GetString($raw, $pos, $nl - $pos).Split(' ')
                $pos = $nl + 1
                if ($hdr.Count -lt 3 -or $hdr[1] -ne 'tree') { if ($hdr.Count -ge 3 -and $hdr[2] -match '^\d+$') { $pos += [int]$hdr[2] + 1 }; continue }
                $size = [int]$hdr[2]; $end = $pos + $size
                $refName = $treeRefs[$hdr[0]]
                while ($pos -lt $end) {
                    $sp = [Array]::IndexOf($raw, [byte]32, $pos)
                    $z = [Array]::IndexOf($raw, [byte]0, $sp)
                    $mode = $utf8.GetString($raw, $pos, $sp - $pos)
                    $name = $utf8.GetString($raw, $sp + 1, $z - $sp - 1)
                    $pos = $z + 1 + 20
                    if ($mode -ne '40000') { $cid = Get-CardId $name; if ($cid) { Add-Seen $seen $cid $name ('ref:' + $refName) } }
                }
                $pos = $end + 1
            }
        }
    }
    # worktrees (uncommitted / not yet committed cards)
    $wts = @(& git -C $Top worktree list --porcelain 2>$null | Where-Object { $_ -match '^worktree ' } | ForEach-Object { $_.Substring(9) })
    foreach ($w in $wts) {
        $d = Join-Path $w 'korean-division/tasks'
        if (Test-Path $d) {
            foreach ($f in [System.IO.Directory]::GetFiles($d)) {
                $name = [System.IO.Path]::GetFileName($f); $cid = Get-CardId $name
                if ($cid) { Add-Seen $seen $cid $name ('worktree:' + (Split-Path -Leaf $w)) }
            }
        }
    }
    # QUEUE / CARD_INDEX first column
    foreach ($qf in 'QUEUE.md', 'CARD_INDEX.md') {
        $p = Join-Path $Ops $qf
        if (Test-Path $p) {
            foreach ($l in [System.IO.File]::ReadAllLines($p, $utf8)) {
                if ($l -match '^\|\s*(T\d+[A-Za-z0-9]*|[A-Z]+\d*[A-Z0-9]*-[A-Za-z]\d?)\s*\|') { Add-Seen $seen $Matches[1] '' $qf }
            }
        }
    }
    return $seen
}
function Read-Registry {
    $reg = @{}
    if (Test-Path $RegPath) {
        foreach ($l in [System.IO.File]::ReadAllLines($RegPath, $utf8)) {
            if ($l -match ('^- (\S+) ' + [regex]::Escape($Sep) + ' (.*)$')) { $reg[$Matches[1].ToUpper()] = $Matches[2] }
        }
    }
    return $reg
}
function Fmt-Line([string]$cid, [string]$title, [string]$who, [string]$br) {
    $t = ($title -replace '[\r\n]+', ' ').Trim()
    return ('- {0} {1} {2} {1} {3} {1} {4} {1} {5}' -f $cid, $Sep, $t, $who, (Get-Date -Format 'yyyy-MM-dd'), $br)
}
function Ensure-Header {
    if (-not (Test-Path $RegPath)) {
        $h = @(
            '# CARD_REGISTRY - 카드 id 예약 장부 (append-only)',
            '',
            '> 한 줄 = `- <id> · <제목> · <예약자> · <날짜> · <브랜치>`. 제목 앞 `[충돌]` = 스캔 시 서로 다른 카드 파일이 같은 id 를 쓰고 있었다는 표시(이동·개명 뒤에도 장부에는 남는다; 현재 충돌 여부는 `card_id.ps1 -Check <id>`).',
            '> 줄을 고치거나 지우지 않는다. id 는 `tools/agent/card_id.ps1` 로만 예약한다 (직접 번호 붙이기 금지 - ops/PIPELINE.md ''로드맵/카드 추가 전 절차'').',
            '> 예약자 `scan` = 도구가 기존 카드 파일/QUEUE/CARD_INDEX 에서 발견해 장부에 옮긴 id.',
            ''
        )
        [System.IO.File]::WriteAllText($RegPath, (($h -join "`r`n") + "`r`n"), $utf8)
    }
}
function Open-Lock {
    if (-not (Test-Path $Ops)) { New-Item -ItemType Directory -Path $Ops | Out-Null }
    $deadline = (Get-Date).AddSeconds($LockSec)
    while ($true) {
        try { return [System.IO.File]::Open($LockPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None) }
        catch [System.IO.IOException] { if ((Get-Date) -gt $deadline) { return $null }; Start-Sleep -Milliseconds (80 + (Get-Random -Maximum 120)) }
    }
}
function Where-Used([string]$k, $reg, $seen) {
    $w = @()
    if ($reg.ContainsKey($k)) { $w += 'registry' }
    if ($seen.ContainsKey($k)) { $w += $seen[$k].Where; if ($seen[$k].Files.Count -gt 1) { $w += ('files: ' + (($seen[$k].Files.Keys | Sort-Object) -join ' | ')) } }
    return $w
}

# ---- main ----
$lock = Open-Lock
if ($null -eq $lock) { Write-Host "card_id: lock timeout ($LockSec s) on $LockPath"; exit 4 }
try {
    $seen = Scan-Cards
    $reg = Read-Registry
    # cache: append ids seen but not yet in the registry (and mark conflicts)
    $newLines = @()
    foreach ($k in ($seen.Keys | Sort-Object)) {
        $s = $seen[$k]
        $conflict = ($s.Files.Count -gt 1)
        $files = @($s.Files.Keys | Sort-Object)
        $title = if ($files.Count -gt 0) { Get-Slug $files[0] $s.Id } else { '(QUEUE/CARD_INDEX row)' }
        if (-not $reg.ContainsKey($k)) {
            if ($conflict) { $title = '[충돌] ' + ($files -join ' | ') }
            $newLines += (Fmt-Line $s.Id $title 'scan' ($s.Where -replace '^(ref|worktree):', ''))
            $reg[$k] = $title
        } elseif ($conflict -and $reg[$k] -notmatch '^\[충돌\]') {
            $newLines += (Fmt-Line $s.Id ('[충돌] ' + ($files -join ' | ')) 'scan' ($s.Where -replace '^(ref|worktree):', ''))
            $reg[$k] = '[충돌] ' + ($files -join ' | ')
        }
    }
    $writeNeeded = ($newLines.Count -gt 0)

    if ($Check -ne '') {
        $k = $Check.ToUpper()
        $w = Where-Used $k $reg $seen
        if ($writeNeeded) { Ensure-Header; [System.IO.File]::AppendAllText($RegPath, (($newLines -join "`r`n") + "`r`n"), $utf8) }
        if ($seen.ContainsKey($k) -and $seen[$k].Files.Count -gt 1) { Write-Host "CONFLICT $Check : $($w -join ' ; ')"; exit 1 }
        if ($w.Count -gt 0) { Write-Host "USED $Check : $($w -join ' ; ')"; exit 3 }
        Write-Host "FREE $Check"; exit 0
    }

    if ($Who -eq '') { $Who = if ($env:KD_AGENT) { $env:KD_AGENT } else { 'agent' } }
    if ($Branch -eq '') { $Branch = (& git rev-parse --abbrev-ref HEAD 2>$null); if (-not $Branch) { $Branch = '-' } }
    if ($Title -eq '') { Write-Host 'card_id: -Title is required when reserving'; exit 2 }

    $ids = @()
    if ($Id -ne '') {
        $ids = @($Id)
    } else {
        if ($Prefix -eq '') { Write-Host 'card_id: need -Prefix (or -Id / -Check)'; exit 2 }
        $max = 0
        foreach ($k in $reg.Keys) {
            if ($k -match ('^' + [regex]::Escape($Prefix.ToUpper()) + '(\d+)')) { $n = [int]$Matches[1]; if ($n -gt $max) { $max = $n } }
        }
        if ($Series) {
            $ids = @(0..($Count - 1) | ForEach-Object { '{0}{1:D2}{2}' -f $Prefix.ToUpper(), ($max + 1), [char](97 + $_) })
        } else {
            $ids = @(1..$Count | ForEach-Object { '{0}{1:D2}' -f $Prefix.ToUpper(), ($max + $_) })
        }
    }
    $bad = @($ids | Where-Object { $reg.ContainsKey($_.ToUpper()) })
    if ($bad.Count -gt 0) {
        foreach ($b in $bad) { Write-Host "REJECT $b : already used ($((Where-Used $b.ToUpper() $reg $seen) -join ' ; '))" }
        if ($writeNeeded) { Ensure-Header; [System.IO.File]::AppendAllText($RegPath, (($newLines -join "`r`n") + "`r`n"), $utf8) }
        exit 2
    }
    Ensure-Header
    $out = @($newLines)
    foreach ($i in $ids) { $out += (Fmt-Line $i $Title $Who $Branch) }
    [System.IO.File]::AppendAllText($RegPath, (($out -join "`r`n") + "`r`n"), $utf8)
    foreach ($i in $ids) { Write-Host $i }
    exit 0
} finally {
    $lock.Close()
}
