# M1 아군 사기 ↔ 제압 연동 (sim)
- M1 | sim | 등급: D 上 · H 3 (Snapshot · SelfTest · Defs · balance · Combat3 · Replay 각 1~3줄)
- 선행: merge/0.2 4e4b405 | 후행: M2 (캠페인 층 탈영 · 미션 시작 때 사기 설정)
- 브랜치/작업 폴더: kd/m1-morale-suppress, kd-develop/kd-m1-morale | 상태: wip-ready | 담당: sim (Opus 5.5)
## 확정 결정 (ops/DECISIONS BH1~BH3, 사용자 2026-10-22)
- 적 사기 시스템 없음(모랄빵 금지). 세력 사기 0~100, 기본 50. 사기 높음 → 제압 하락 빠름, 낮음 → 느림. 50 = 정확히 중립.
- 탈영은 전투 종료 후 정산/캠페인 층(M2)만 — M1 은 구현하지 않음.
## 결과
- Faction.Morale(정수, Morale.cs) + World.SetFactionMorale(틱 0 전 진입점) · 배수 World.MoraleSuppressDecayMul (50→1 정확, 정수 비교로 건너뜀; 0/100 → balance combat.moraleSuppressDecayAt0/At100 = 0.75/1.25 자리표시, 구간 선형).
- 적용 지점 1곳: Combat3.DecaySuppression. 저장 FactionSnapshot 끝 정수, 체크섬은 50 아닐 때만, 리플레이 머리말 Morale(null = 전부 50).
- 임시 번호 FV 66→67 · Replay 24→25 — 병합 때 fv_renumber 필요.
- 검증: morale 6/6 · snapshot · replay PASS, snaptest QUICK 86 OK/0 FAIL, detcheck 672804207fc4e486 3c42aea49324248e 불변 (트리 golden 파일은 merge 라인에서 원래 미갱신 → MISMATCH 표시는 기존과 동일).
- 메모: 0.25초 정수 반올림 때문에 실효 배수가 계단(이병 기준 0→×0.72, 100→×1.15). AI 세력도 같은 필드를 갖지만 기본 50 이라 효과 없음(캠페인이 아군만 설정).
