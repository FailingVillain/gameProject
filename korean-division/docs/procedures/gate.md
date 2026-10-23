# 게이트: 아트 풀 / 비아트 heavy 풀
> 요약: 무거운 일은 전부 heavy_gate(v2) 통과. 한도 = `ops/LIMITS.md`. 풀 분리: 아트(`-Render`) 8, 비아트 3(빌드 `-Build` 1칸 예약). 대기 중인 프로세스는 슬롯을 안 쥔다(실행 중만 셈). 대기 로그 `ops/gate_wait.log`. 어떤 PID 도 종료하지 않는다.

- **검증**: `--selftest` (FAIL/ERROR 줄만 grep, 타임아웃 >=15분, 포어그라운드 590000), `--detcheck` (시뮬 변경 시만 `--update` 후 8변형), `--snaptest`(상태 변경), `--uifuzz 30`(UI 변경). 개발 중에는 `KD_SELFTEST_ONLY=<group>` 만, 전체는 큐 끝 release.
- **측정**: 빌드 · 시험 · 시뮬 직전 `powershell -ExecutionPolicy Bypass -File "C:\gameProject\korean-division\tools\agent\heavy_gate.ps1"` (자신은 한 번에 무거운 프로세스 1개; 병합 잠금 보유 시에만 `-Priority`). **빌드(`dotnet build`) 직전은 `-Build`** (예약 슬롯 사용), selftest/stress/측정은 스위치 없이(비아트 풀 2칸까지). 확인용 `-DryRun`(한 번 판정만, 대기 없음).
- **렌더**: 유닛 재렌더(`make_unit.py`)는 직전 `heavy_gate.ps1 -Render` 로 한 칸 예약, 그 외 모든 작업은 `-Render` 없이. 필요한 유닛만, 작은 표본 먼저.
- **게이트 v3 (2026-10-23)**: 대기 호출은 FIFO 표(`ops/gate_queue/`)에 줄을 서고 머리만 판정한다(이유 `QUEUE`). 대기자가 6명을 넘으면 새 호출은 종료코드 **75** ("gate overloaded") 로 즉시 끝난다 -> 지휘자에게 보고하고 다른 일을 한다(재시도 루프 금지). 한도·바닥값은 `tools/agent/gate_config.json`(예산 20 GB, 빌드/측정 바닥 7->6.5->6 일 단위, 렌더 8, 비상 4). `-Priority` 는 바닥값만 면제(예산·비상 바닥은 적용). 일일 요약: `tools/agent/gate_stats.ps1`.
