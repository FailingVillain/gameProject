# 게이트: 검증 / 측정 / 렌더 (현재 규칙 그대로; 최종값은 별도 결정)
> 요약: 무거운 일은 전부 heavy_gate 통과. 동시 한도 = `ops/LIMITS.md`.

- **검증**: `--selftest` (FAIL/ERROR 줄만 grep, 타임아웃 >=15분, 포어그라운드 590000), `--detcheck` (시뮬 변경 시만 `--update` 후 8변형), `--snaptest`(상태 변경), `--uifuzz 30`(UI 변경). 개발 중에는 `KD_SELFTEST_ONLY=<group>` 만, 전체는 큐 끝 release.
- **측정**: 빌드 · 시험 · 시뮬 직전 `powershell -ExecutionPolicy Bypass -File "C:\gameProject\korean-division\tools\agent\heavy_gate.ps1"` (자신은 한 번에 무거운 프로세스 1개; 병합 잠금 보유 시에만 `-Priority`).
- **렌더**: 유닛 재렌더(`make_unit.py`)는 직전 `heavy_gate.ps1 -Render` 로 한 칸 예약, 그 외 모든 작업은 `-Render` 없이(= 한도 -1). 필요한 유닛만, 작은 표본 먼저.
