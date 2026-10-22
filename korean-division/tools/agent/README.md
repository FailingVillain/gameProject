# tools/agent
- heavy_gate.ps1 v2 설치(활성화): 이 브랜치(tools/gate-v2)를 main 에 병합하면 끝 — 호출 경로(`korean-division\tools\agent\heavy_gate.ps1`)는 그대로, 사용 중인 에이전트가 없을 때 지휘자가 병합; 시험 `powershell -File heavy_gate_test.ps1` (ALL PASS), 확인 `heavy_gate.ps1 -DryRun`.
