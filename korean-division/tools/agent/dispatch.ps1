# dispatch.ps1 - assemble the agent order prompt from a card's front-matter (PowerShell 5.1). UTF-8 with BOM.
# Spec: docs/specs/DISPATCH_TEMPLATE_SPEC.md (step 2). No side effects: reads cards/ops, runs preflight.ps1, prints.
#   dispatch.ps1 T58a                 prompt text + Agent-tool parameters as JSON (subagent_type, model, description)
#   dispatch.ps1 T58a,T58b -DryRun    bundle of several cards (rules of spec section 4); -DryRun = prompt text only, no JSON
#   dispatch.ps1 T58a,T58b,T60 -Plan  groups (bundles), order inside a bundle, conflict pairs, parallel-safe sets; no prompt
#   dispatch.ps1 -Selftest            builds a prompt from a throw-away card and checks the 4 mandatory fields
#   -Extra "<text>"   one free line appended to ORDER   -Force  allow same-file cards in different agents (-Plan)
#   -Top <dir>        repo top (default: main worktree via git common dir)
# Exit: 0 ok | 1 preflight BLOCK / plan conflict | 2 bad card (names the missing key) | 3 bundle rule violated
# Card front-matter: flat `key: value` lines and `[a, b]` lists between two `---` lines at the top of tasks/<id>-*.md.
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string[]]$Ids = @(),
    [switch]$DryRun, [switch]$Plan, [switch]$Selftest, [switch]$Force,
    [string]$Extra = '', [string]$Top = ''
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
$KD = Join-Path $Top 'korean-division'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$Required = 'id', 'role', 'grade', 'branch', 'base', 'hotspots', 'read_first', 'gates'
$WorkRoles = 'sim', 'feature', 'art', 'docs', 'reviewer', 'measure', 'spec', 'scout'
$TIER_UP = [string][char]0x4E0A   # tier markers, kept as code points so the source survives any codepage
$TIER_MID = [string][char]0x4E2D
$TIER_DOWN = [string][char]0x4E0B
$DECIDE = ([string][char]0xC784) + [char]0xC758 + ' ' + [char]0xACB0 + [char]0xC815 + ':'   # the report's last-line label

function Fail([int]$code, [string]$msg) { Write-Host "dispatch: $msg"; exit $code }

function Read-Card([string]$id, [string]$tasksDir) {
    $f = @(Get-ChildItem -Path $tasksDir -Filter ($id + '-*.md') -File -ErrorAction SilentlyContinue)
    if ($f.Count -eq 0) { Fail 2 "no card file tasks/$id-*.md under $tasksDir" }
    if ($f.Count -gt 1) { Fail 2 "several card files for ${id}: $($f.Name -join ', ')" }
    $lines = [System.IO.File]::ReadAllLines($f[0].FullName, $utf8)
    if ($lines.Count -lt 2 -or $lines[0].Trim() -ne '---') { Fail 2 "$($f[0].Name): no front-matter (first line must be ---)" }
    $fm = [ordered]@{}; $end = -1
    for ($i = 1; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l.Trim() -eq '---') { $end = $i; break }
        if ($l.Trim() -eq '' -or $l.TrimStart().StartsWith('#')) { continue }
        if ($l -notmatch '^([A-Za-z_][A-Za-z0-9_]*)\s*:\s*(.*)$') { Fail 2 "$($f[0].Name):$($i + 1): not a flat 'key: value' line" }
        $k = $Matches[1]; $v = ($Matches[2] -replace '\s+#.*$', '').Trim()
        if ($v -match '^\[(.*)\]$') { $v = @($Matches[1].Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }) }
        $fm[$k] = $v
    }
    if ($end -lt 0) { Fail 2 "$($f[0].Name): front-matter not closed (second ---)" }
    foreach ($r in $Required) {
        if (-not $fm.Contains($r) -or @($fm[$r]).Count -eq 0 -or "$(@($fm[$r])[0])" -eq '') { Fail 2 "$($f[0].Name): missing required key '$r'" }
    }
    $fm['_file'] = $f[0].FullName
    $fm['_rel'] = 'korean-division/tasks/' + $f[0].Name
    $fm['_bytes'] = $f[0].Length
    if (-not $fm.Contains('model') -or "$($fm['model'])" -eq '') {
        $t = if ($fm.Contains('tier')) { "$($fm['tier'])" } else { '' }
        $fm['model'] = if ($t.Contains($TIER_UP)) { 'opus' } else { 'sonnet' }   # mid/down -> sonnet (haiku avoided, spec Q2)
    }
    $fm['_d'] = if ("$($fm['grade'])" -match 'D(\S)') { $Matches[1] } else { '' }
    $fm['_h'] = if ("$($fm['grade'])" -match 'H(\d)') { [int]$Matches[1] } else { 9 }
    return $fm
}

function Hot([hashtable]$c) { return @($c['hotspots'] | ForEach-Object { $_.Replace('\', '/') }) }
function Dirs([hashtable]$c) { return @(Hot $c | ForEach-Object { $p = $_ -replace '/[^/]*$', ''; if ($p -eq $_) { '.' } else { $p } } | Select-Object -Unique) }
# 'file' | 'folder' | 'none' (spec section 4 matrix columns)
function Overlap([hashtable]$a, [hashtable]$b) {
    $same = @(Hot $a | Where-Object { (Hot $b) -contains $_ })
    if ($same.Count -gt 0) { return 'file' }
    $da = Dirs $a; $db = Dirs $b
    if (@($da | Where-Object { $db -contains $_ }).Count -gt 0) { return 'folder' }
    $ta = @($da | ForEach-Object { ($_ -split '/')[0] }); $tb = @($db | ForEach-Object { ($_ -split '/')[0] })
    if (@($ta | Where-Object { $tb -contains $_ }).Count -gt 0) { return 'folder' }
    return 'none'
}

# bundle rules of spec section 4; returns the list of violated rules (empty = bundle-able)
function Bundle-Problems([object[]]$cs) {
    $p = @()
    if ($cs.Count -gt 4) { $p += 'more than 4 cards' }
    if (@($cs | ForEach-Object { $_['role'] } | Select-Object -Unique).Count -gt 1) { $p += 'roles differ' }
    if (@($cs | ForEach-Object { $_['model'] } | Select-Object -Unique).Count -gt 1) { $p += 'models differ' }
    foreach ($c in $cs) {
        if ($c['_h'] -gt 1) { $p += "$($c['id']): H>1" }
        if ($c['_d'] -eq $TIER_UP) { $p += "$($c['id']): D is the top tier" }
        if ($c['_bytes'] -gt 1536) { $p += "$($c['id']): card over 1.5KB" }
        if ($c['role'] -eq 'art' -and $cs.Count -gt 1) { $p += "$($c['id']): art cards are not bundled (heavy lane)" }
        if ($c['role'] -eq 'reviewer' -and $cs.Count -gt 1) { $p += "$($c['id']): reviewer cards are not bundled" }
    }
    $ids = @($cs | ForEach-Object { $_['id'] })
    $claim = @($cs | Where-Object { "$($_['fv'])" -eq 'claim' })
    if ($claim.Count -gt 1) { $p += 'two FV claimers' }
    for ($i = 0; $i -lt $cs.Count; $i++) {
        for ($j = $i + 1; $j -lt $cs.Count; $j++) {
            if ((Overlap $cs[$i] $cs[$j]) -eq 'none') { $p += "$($ids[$i]) / $($ids[$j]): hotspots disjoint (nothing to gain)" }
        }
    }
    foreach ($c in $cs) {
        if ($c.Contains('blockedBy')) {
            foreach ($b in @($c['blockedBy'])) { if ($ids -contains $b -and $ids.IndexOf($b) -gt $ids.IndexOf($c['id'])) { $p += "$($c['id']) blockedBy $b but listed before it" } }
        }
    }
    return @($p | Select-Object -Unique)
}

# ops/DISPATCH_TAIL.md: body of a '## <name>' section, '- ' bullets or plain lines, trimmed
function Tail-Section([string]$name) {
    $tf = Join-Path $Top 'ops/DISPATCH_TAIL.md'
    if (-not (Test-Path $tf)) { Fail 2 "missing ops/DISPATCH_TAIL.md under $Top" }
    $out = @(); $on = $false
    foreach ($l in [System.IO.File]::ReadAllLines($tf, $utf8)) {
        if ($l -match '^##\s+(.+?)\s*$') { $on = ($Matches[1] -eq $name -or $Matches[1].StartsWith($name + ' (')); continue }
        if ($on -and $l.Trim() -ne '' -and -not $l.StartsWith('(')) { $out += $l.Trim() }
    }
    return $out
}

function Preflight-Line([string]$id) {
    if ($script:FakePreflight) { return [pscustomobject]@{ Code = 0; Last = "PREFLIGHT OK $id (0 warning(s))"; All = @() } }
    $pf = Join-Path $PSScriptRoot 'preflight.ps1'
    $o = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $pf $id -Top $Top 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    $last = if ($o.Count -gt 0) { $o[$o.Count - 1] } else { '' }
    return [pscustomobject]@{ Code = $code; Last = $last; All = $o }
}

function Build-Prompt([object[]]$cs, [string]$extra) {
    $role = $cs[0]['role']
    $sb = New-Object System.Collections.ArrayList
    $cardFiles = ($cs | ForEach-Object { $_['_rel'] }) -join ', '
    [void]$sb.Add("ROLE: $role. Card file(s): $cardFiles. Read the card first, then only READ FIRST.")
    $skipPf = $false
    foreach ($c in $cs) {
        $id = $c['id']
        $hash = (& git -C $Top rev-parse --short=8 ("refs/heads/" + $c['base']) 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $hash) { $hash = (& git -C $Top rev-parse --short=8 $c['base'] 2>$null) }
        if (-not $hash) { Fail 2 "$id : base '$($c['base'])' is not a known ref" }
        $pf = Preflight-Line $id
        if ($pf.Code -ne 0) { Write-Host ($pf.All -join "`n"); Fail 1 "$id : preflight BLOCK - no prompt produced" }
        $fvLine = if ("$($c['fv'])" -eq 'claim') { 'FV: claim with tools/agent/fv_claim.ps1 (temp value = base+1)' } else { 'FV: none' }
        $blocked = if ($c.Contains('blockedBy') -and @($c['blockedBy']).Count -gt 0 -and "$($c['blockedBy'][0])" -ne '') { ($c['blockedBy'] -join ', ') } else { 'none' }
        [void]$sb.Add("ORDER $id : $($c['title'])")
        [void]$sb.Add("- branch $($c['branch']); base $($c['base']) @ $hash; blockedBy $blocked")
        [void]$sb.Add("- $fvLine; hotspots: $((Hot $c) -join ', ')")
        [void]$sb.Add("- READ FIRST: $(@($c['read_first']) -join '; ')")
        if ($c.Contains('forbidden') -and @($c['forbidden']).Count -gt 0) { [void]$sb.Add("- DO NOT TOUCH: $(@($c['forbidden']) -join ', ')") }
        [void]$sb.Add("- GATES: $(@($c['gates']) -join ', ') (heavy jobs via heavy_gate)")
        [void]$sb.Add($pf.Last)
    }
    if ($cs.Count -gt 1) { [void]$sb.Add("BUNDLE: do the cards in the listed order on branch bundle/$($cs[0]['id'])-$($cs.Count); one commit per card, WIP-commit after each.") }
    if ($extra -ne '') { [void]$sb.Add("NOTE: $extra") }
    [void]$sb.Add('TAIL (full text: ops/DISPATCH_TAIL.md, AGENT_RULES.md):')
    foreach ($l in (Tail-Section 'inline')) { [void]$sb.Add($l) }
    $rs = Tail-Section ('role:' + $role)
    if ($rs.Count -gt 0) { [void]$sb.Add("ROLE NOTE: $($rs -join ' ')") }
    $rl = if ($cs[0].Contains('report_lines') -and "$($cs[0]['report_lines'])" -match '^\d+$') { $cs[0]['report_lines'] } else { '8' }
    [void]$sb.Add("REPORT: final report <= $rl lines, Korean. Last line `"$DECIDE`" = every value/assumption/interpretation/deletion/test relaxation you chose yourself (or none).")
    return ($sb -join "`n")
}

function Invoke-Plan([object[]]$cs) {
    $rest = New-Object System.Collections.ArrayList; [void]$rest.AddRange($cs)
    $groups = @()
    # greedy: seed with the first unplaced card, add following cards while the bundle rules still hold
    while ($rest.Count -gt 0) {
        $g = @($rest[0]); $rest.RemoveAt(0)
        $i = 0
        while ($i -lt $rest.Count) {
            $try = @($g) + @($rest[$i])
            if ((Bundle-Problems $try).Count -eq 0) { $g = $try; $rest.RemoveAt($i) } else { $i++ }
        }
        $groups += , $g
    }
    Write-Host "PLAN: $($cs.Count) card(s) -> $($groups.Count) agent(s)"
    $n = 0
    foreach ($g in $groups) { $n++; Write-Host ("  agent {0}: {1}  [{2}, {3}]" -f $n, (($g | ForEach-Object { $_['id'] }) -join ' -> '), $g[0]['role'], $g[0]['model']) }
    $conf = 0; $par = @()
    for ($a = 0; $a -lt $groups.Count; $a++) {
        for ($b = $a + 1; $b -lt $groups.Count; $b++) {
            $worst = 'none'; $hmax = 0
            foreach ($x in $groups[$a]) { foreach ($y in $groups[$b]) {
                $o = Overlap $x $y
                if ($o -eq 'file' -or ($o -eq 'folder' -and $worst -ne 'file')) { $worst = $o }
                $hmax = [Math]::Max($hmax, [Math]::Max($x['_h'], $y['_h']))
            } }
            $la = "agent $($a + 1)"; $lb = "agent $($b + 1)"
            if ($worst -eq 'file') { Write-Host "  CONFLICT $la vs $lb : same file -> serialize"; $conf++ }
            elseif ($worst -eq 'folder' -and $hmax -ge 2) { Write-Host "  WARN $la vs $lb : same folder, H>=2 -> conductor decides" }
            else { $par += "$la + $lb" }
        }
    }
    if ($par.Count -gt 0) { Write-Host ("  parallel-safe pairs: " + ($par -join '; ')) }
    if ($conf -gt 0 -and -not $Force) { Write-Host '  refuse: same file in different agents at the same time (use -Force to override)'; exit 1 }
    exit 0
}

if ($Selftest) {
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('dispatch_selftest_' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    try {
        $card = "---`nid: T999x`ntitle: selftest card`nrole: feature`ntier: mid`ngrade: D H1`nbranch: feat/selftest`nbase: main`nhotspots: [src/A.cs]`nfv: none`nread_first: [src/A.cs:Foo()]`ngates: [build, detcheck]`n---`nbody`n"
        [System.IO.File]::WriteAllText((Join-Path $tmp 'T999x-selftest.md'), $card, $utf8)
        $c = Read-Card 'T999x' $tmp
        $okModel = ($c['model'] -eq 'sonnet')
        $script:FakePreflight = $true   # selftest must not depend on QUEUE state
        $txt = Build-Prompt @($c) ''
        $must = '@ [0-9a-f]{7,}', 'FV: ', 'hotspots: src/A.cs', 'PREFLIGHT OK T999x \(', 'report <= \d+ lines', 'push'
        $miss = @($must | Where-Object { $txt -notmatch $_ })
        $inl = @(Tail-Section 'inline')
        $ok = $okModel -and $miss.Count -eq 0 -and $inl.Count -ge 5 -and $c['hotspots'][0] -eq 'src/A.cs' -and $c['_h'] -eq 1
        Write-Host ("dispatch selftest: model={0} tail-missing=[{1}] inline-lines={2} -> {3}" -f $c['model'], ($miss -join ','), $inl.Count, $(if ($ok) { 'OK' } else { 'FAIL' }))
        if (-not $ok) { exit 1 }
        exit 0
    } finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
}

$all = @($Ids | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
if ($all.Count -eq 0) { Fail 2 'usage: dispatch.ps1 <id>[,<id>...] [-DryRun] [-Plan] [-Extra "text"] | -Selftest' }
$cards = @($all | ForEach-Object { Read-Card $_ (Join-Path $KD 'tasks') })
if ($Plan) { Invoke-Plan $cards }
if ($cards.Count -gt 1) {
    $bp = @(Bundle-Problems $cards)
    if ($bp.Count -gt 0 -and -not $Force) { Write-Host ('dispatch: bundle rule(s) violated: ' + ($bp -join '; ')); exit 3 }
}
$prompt = Build-Prompt $cards $Extra
Write-Host $prompt
if (-not $DryRun) {
    $c0 = $cards[0]
    $desc = ($all -join '+') + ' ' + "$($c0['title'])"
    if ($desc.Length -gt 50) { $desc = $desc.Substring(0, 50) }
    Write-Host ''
    Write-Host (@{ subagent_type = $c0['role']; model = $c0['model']; description = $desc } | ConvertTo-Json -Compress)
}
exit 0
