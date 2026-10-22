# T39b 하천 2: 길찾기 · 도하 AI · NearWater 정리 (+ ClearFor 물 위 사선 측정 선행)
- id: T39b | 역할: sim | 등급: D上 H2 (R2 SM Wlight)
- 선행: T39a (스택: `river-r1` 위) | 후행: T39c(병행 가능), T39d
- 근거 문서: docs/RIVER_DESIGN.md (브랜치 spec/river-design @26e0b88; 이 카드 시작 시 해당 파일을 `git show spec/river-design:korean-division/docs/RIVER_DESIGN.md` 로 읽는다). 사용자는 세부 요구를 주지 않았다 -> 제안서 "권장" 답을 가정(아래 가정 목록). 로드맵 한 줄(ROADMAP_LONG 항목 8 "강 소형 포함")을 넘는 메커니즘 추가 금지.
- 핫스팟/만질 파일: GameMapTerrain.ClearFor, GameMap.IsWalkable/길찾기 비용(MoveMul 사용 여부), TacticalAi.Crossing.cs FindCrossing, Suppression.NearWater, Surrender, tactics.json(minObstacle 등 데이터 조정만), CrossingSelfTest
- 브랜치: `river-r2` (base `river-r1`) | 상태: queued | 담당: -
## 확정 결정
- 없음(ROADMAP_LONG 항목 8 한 줄). 권장 답 가정 목록 참조.
## 범위 밖
하천 전용 AI 교리 · 깊은 물 퇴각 금지 신규 판단 · 수영 · 다리 파괴 · 밸런스 튜닝(보류 정책)
## 수용 기준
1. **먼저 측정**(코드 변경 전, 결과를 카드 `결과`에 수치로): 물 칸 한 줄을 사이에 둔 두 사수 사이에서 `ClearFor` 가 막는지, 강둑 교전 AI 가 쓰는 시야 경로 확인. 막으면 Q6 가정(건너 사격 허용)에 따라 `Water` 를 사선 비차단으로 바꾸는 최소 변경 + 기존 지도 골든 영향(c0_02_yalu 등)을 별도로 보고 — 골든이 바뀌면 변경하지 말고 `docs/DECISIONS_PENDING.md` 에 질문 한 줄.
2. 길찾기: 여울/다리 경유 경로 존재, 깊은 물 비통과; MoveMul 이 경로 비용에 쓰이는지 기록(안 쓰면 허용, 그대로).
3. FindCrossing: 여울 있는 폭 1~6칸 하천 지도에서 병목을 잡음(좁은 띠가 minObstacle 에 걸리면 tactics.json 값만 조정), 넓은 여울(>maxLaneWidth)은 일반 공격.
4. NearWater/Surrender 는 `Water` 만 인식(Ford · 다리 제외); 기존 지도에서 동작 불변.
5. selftest crossing 회귀 통과 + 단위 시험 1건, detcheck 골든 불변(기존 지도), tactics-obs --choke 기준선 대비 불변(n=8 동작 확인만, 승률 튜닝 안 함).
## 측정
KD_SELFTEST_ONLY=crossing, tactics-obs n=8, detcheck.
## 가정한 답
T39a 목록과 동일 (특히 Q6 허용, Q4 감속만).
## 결과
-
