# 게이트: 아트 풀 / 비아트 heavy 풀
> 요약: 무거운 일은 전부 heavy_gate(v2) 통과. 한도 = `ops/LIMITS.md`. 풀 분리: 아트(`-Render`) 8, 비아트 3(빌드 `-Build` 1칸 예약). 대기 중인 프로세스는 슬롯을 안 쥔다(실행 중만 셈). 대기 로그 `ops/gate_wait.log`. 어떤 PID 도 종료하지 않는다.

- **검증**: `--selftest` (FAIL/ERROR 줄만 grep, 타임아웃 >=15분, 포어그라운드 590000), `--detcheck` (시뮬 변경 시만 `--update` 후 8변형), `--snaptest`(상태 변경), `--uifuzz 30`(UI 변경). 개발 중에는 `KD_SELFTEST_ONLY=<group>` 만, 전체는 큐 끝 release.
- **측정**: 빌드 · 시험 · 시뮬 직전 `powershell -ExecutionPolicy Bypass -File "C:\gameProject\korean-division\tools\agent\heavy_gate.ps1"` (자신은 한 번에 무거운 프로세스 1개; 병합 잠금 보유 시에만 `-Priority`). **빌드(`dotnet build`) 직전은 `-Build`** (예약 슬롯 사용), selftest/stress/측정은 스위치 없이(비아트 풀 2칸까지). 확인용 `-DryRun`(한 번 판정만, 대기 없음).
- **렌더**: 유닛 재렌더(`make_unit.py`)는 직전 `heavy_gate.ps1 -Render` 로 한 칸 예약, 그 외 모든 작업은 `-Render` 없이. 필요한 유닛만, 작은 표본 먼저.
