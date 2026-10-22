# TC-B SK2 위력 정찰 — 경보 상태 (잠입)
- id: TC-B | 역할: sim | 등급: D上 H3
- 선행: 없음(캠페인 스택과 독립 시험 가능) | 후행: -
- 핫스팟/만질 파일: Fog/Detection · 이벤트 스크립트 · Snapshot · Replay
- 브랜치: kd/tcb-alert | 상태: wip-ready | 담당 에이전트: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22: SK2 위력 정찰은 은신/시야 규칙이 필요(사용자 "필요하다"). 소초 70명(순찰조 · 경계병 포함).
- 설계 안(제안): 순찰조/경계가 정찰 분대를 탐지하면 경보 → 증원 1파 + 문서 보관소 경계 강화. 기존 안개 · 지형 시야 · 은폐 탐지(Detection)를 재사용하고 새 규칙은 최소로.
- 2026-10-22 대체안(승인, spec R9): 1단계 = 정찰 분대가 소초 존에 들어가면 경보 스크립트 발동. 최후 보루 = 문서 점령 시 타이머 증원 1파. 완전판(Detection 기반 경보)은 후속. 변수 이름 동일.
## 범위 밖
일반 잠입 게임플레이 확장.
## 수용 기준 (측정 가능)
- 같은 시드 리플레이 해시 동일(결정성). 경보 전/후 시험. 새 상태 = 체크섬 + Audit + FormatVersion↑(번호 예약).
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
selftest · detcheck · snaptest.
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
- 감지 경보 seenBy/seenTag + spawn delay + patrol 구현 (MissionAlert.cs), FV 67 임시 (병합 때 fv_renumber), Replay 불변. selftest tcb 통과 · snapshot · snaptest · detcheck 672804207fc4e486 3c42aea49324248e 불변. 교체 안 = ENGINE_NOTES TC-B 절. 상태: wip-ready (kd/tcb-alert)
