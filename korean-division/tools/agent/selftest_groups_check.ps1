# selftest_groups_check.ps1 - every KD_SELFTEST_ONLY name used by scripts/procedure docs must exist in the src/SelfTest.cs chain.
# (An unknown name is now a fast error in SelfTest.cs; this finds stale names before they hit it.)  -List prints the valid names.
# Exit 0 = all names valid; 1 = some name missing.  Sources scanned: korean-division/tools (*.ps1 *.py *.sh), ops/*.md, docs/procedures/*.md,
# docs/*.md (not docs/legacy).  Detected forms: KD_SELFTEST_ONLY=name | $SelfCheckGroups = @(..) | merge_step 'snapshot' | -Groups a,b examples.
# Not detected: names built at run time (selftest_parallel reads the chain itself; merge_step passes -Groups through).
param([switch]$List)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path      # korean-division/
$repo = (Resolve-Path (Join-Path $root '..')).Path
$src = Get-Content (Join-Path $root 'src/SelfTest.cs') -Raw -Encoding UTF8
$valid = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in [regex]::Matches($src, 'KD_SELFTEST_ONLY"\)\s*==\s*"([A-Za-z0-9_]+)"')) { [void]$valid.Add($m.Groups[1].Value) }
$i = $src.IndexOf('string only = Environment'); $j = $src.IndexOf('default: core = false', $i)
if ($i -ge 0 -and $j -gt $i) { foreach ($m in [regex]::Matches($src.Substring($i, $j - $i), 'case "([A-Za-z0-9_]+)":')) { [void]$valid.Add($m.Groups[1].Value) } }
if ($List) { $valid | Sort-Object; exit 0 }
# Names intentionally NOT in the chain (handled other ways) - explain each here.
#   detcheck : selftest_parallel runs it as the separate '--detcheck' mode (env var removed), not as a KD_SELFTEST_ONLY group.
#   g1,g2    : placeholders in the merge_step.ps1 usage comment.
#   isai     : example group in the merge_step.ps1 comment; the group lives on the unmerged branch isai-restore (drop when merged or the example changes).
#   findtarget: named in ops/PERF_OPT_PLAN.md as a group the planned findtarget card will add (drop when that card merges).
$allow = @('detcheck', 'g1', 'g2', 'isai', 'findtarget')
$files = @()
$files += Get-ChildItem (Join-Path $root 'tools') -Recurse -Include *.ps1, *.py, *.sh -File | Where-Object { $_.Name -ne 'selftest_groups_check.ps1' }
$files += Get-ChildItem (Join-Path $repo 'ops') -Filter *.md -File -ErrorAction SilentlyContinue
$files += Get-ChildItem (Join-Path $repo 'docs/procedures') -Filter *.md -File -ErrorAction SilentlyContinue
$files += Get-ChildItem (Join-Path $repo 'docs') -Filter *.md -File -ErrorAction SilentlyContinue
$used = @{}   # name -> first file
function Add-Use([string]$n, [string]$f) { if ($n -and -not $used.ContainsKey($n)) { $used[$n] = $f } }
foreach ($f in $files) {
    $t = Get-Content $f.FullName -Raw -Encoding UTF8; $rel = $f.FullName.Substring($repo.Length + 1)
    foreach ($m in [regex]::Matches($t, 'KD_SELFTEST_ONLY=([A-Za-z0-9_]+)')) { Add-Use $m.Groups[1].Value $rel }
    foreach ($m in [regex]::Matches($t, '\$SelfCheckGroups\s*=\s*@\(([^)]*)\)')) { foreach ($n in [regex]::Matches($m.Groups[1].Value, '"([A-Za-z0-9_]+)"')) { Add-Use $n.Groups[1].Value $rel } }
    foreach ($m in [regex]::Matches($t, "(?:\`$grpText\s*=|foreach \(\`$g in \(@\()\s*'([A-Za-z0-9_]+)'")) { Add-Use $m.Groups[1].Value $rel }
    foreach ($m in [regex]::Matches($t, '-Groups\s+([a-z][A-Za-z0-9_,]*)')) { foreach ($n in $m.Groups[1].Value.Split(',')) { Add-Use $n $rel } }
}
$bad = @($used.Keys | Where-Object { -not $valid.Contains($_) -and $allow -notcontains $_ } | Sort-Object)
Write-Host ("valid groups in SelfTest.cs: {0}; names used by scripts/docs: {1}" -f $valid.Count, $used.Count)
if ($bad.Count) { foreach ($n in $bad) { Write-Host ("MISSING: '{0}' (first seen in {1})" -f $n, $used[$n]) }; exit 1 }
Write-Host 'OK: every used KD_SELFTEST_ONLY name exists in the chain'
exit 0
