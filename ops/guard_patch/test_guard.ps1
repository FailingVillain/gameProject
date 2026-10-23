# Test harness for a patched kd_guard.ps1 copy. Usage: powershell -File test_guard.ps1 -HookDir <dir with patched kd_guard.ps1 + kd_scope.json + kd_delete_allow.txt>
param([string]$HookDir)
function T([string]$cmd, [string]$expect) {
  $j = @{tool_name='Bash'; cwd='C:/gameProject'; tool_input=@{command=$cmd}} | ConvertTo-Json -Compress
  $out = $j | powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $HookDir 'kd_guard.ps1') 2>&1 | Out-String
  $code = $LASTEXITCODE
  $res = if ($code -eq 2) { 'BLOCK' } elseif ($out -match '"ask"') { 'ASK' } else { 'ALLOW' }
  $ok = if ($res -eq $expect) { 'ok  ' } else { 'FAIL' }
  "{0} {1,-5} (want {2,-5}) {3}" -f $ok, $res, $expect, $cmd
}
$wt = 'git worktree'; $br = 'git branch'
T "$wt remove --force kd-develop/kd-perf-bench" 'ALLOW'
T "$br -D tools/perf-bench" 'ALLOW'
T "$wt unlock kd-develop/kd-rerender-5" 'ALLOW'
T "$wt remove --force kd-develop/kd-rerender-5" 'ALLOW'
T "$wt remove --force kd-develop/kd-final" 'BLOCK'
T "$br -D main" 'BLOCK'
T "$wt remove --force kd-develop/kd-perf-bench && $br -D main" 'BLOCK'
T "$wt remove --force kd-develop/kd-perf-bench; echo x" 'BLOCK'
T 'git reset --hard HEAD' 'BLOCK'
T 'git push --force origin main' 'BLOCK'
T '"C:\Program Files\dotnet\dotnet.exe" build src/x.csproj > build.log' 'ALLOW'
T '"C:\Program Files\dotnet\dotnet.exe" build src/x.csproj' 'ALLOW'
T 'echo hi > "C:\Program Files\foo.txt"' 'BLOCK'
T 'cp a.txt "C:\Program Files\dotnet\x.txt"' 'ALLOW'
