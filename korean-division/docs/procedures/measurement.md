# 측정 규칙
> 요약: 수용 기준에 필요한 것만, 기본 5시드, 기준선은 BASELINE_CURRENT, 새 집계기 금지.

- 기존 도구 재사용: `--tactics-obs`, `--collide`, `--balance`, `--stress`, `tools/bn_campaign`, ArtyFx, `tools/agent/soak.ps1`, 보관된 집계기는 `tools/_archive/` (`n18_agg` · `n18_cav` · `n18_exchange` 포함). 새 집계기는 꼭 필요할 때만.
- 작업이 정한 시뮬 시간 상한을 지킨다. 시드 기본 5, 수용 확인에만 8. `docs/BASELINE_CURRENT.md` 에 현재 main 커밋 블록이 있으면 그 값을 "before" 로 쓰고 "after" 만 측정.
- 시험 임시 파일은 `TestTemp.Child` 로만; 병렬 실행 안전.
- 기준선 블록(커밋, 시나리오, 시드, 승, K/E, 시간 초과)은 measure/release 가 추가. 밸런스 판정은 야간 soak 보고(`docs/soak/<date>/report.md`) - 자기 시드는 기능 작동만 보인다. 밸런스 보고는 관찰만, 값은 사용자 승인 후.
- 개별 효과 측정은 모아서 `measure` 에이전트 / 통합 측정으로. 긴 로그는 Gemini flash 요약.
