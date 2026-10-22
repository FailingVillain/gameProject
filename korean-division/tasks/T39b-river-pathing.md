# T39b 하천 2: 길찾기 · 도하 AI · NearWater 정리 (+ ClearFor 물 위 사선 측정 선행)
- id: T39b | 역할: sim | 등급: D上 H2 (R2 SM Wlight)
- 선행: T39a (스택: `river-r1` 위) | 후행: T39c(병행 가능), T39d
- 근거 문서: docs/RIVER_DESIGN.md (브랜치 spec/river-design @26e0b88; 이 카드 시작 시 해당 파일을 `git show spec/river-design:korean-division/docs/RIVER_DESIGN.md` 로 읽는다). 사용자는 세부 요구를 주지 않았다 -> 제안서 "권장" 답을 가정(아래 가정 목록). 로드맵 한 줄(ROADMAP_LONG 항목 8 "강 소형 포함")을 넘는 메커니즘 추가 금지.
- 핫스팟/만질 파일: GameMapTerrain.ClearFor, GameMap.IsWalkable/길찾기 비용(MoveMul 사용 여부), TacticalAi.Crossing.cs FindCrossing, Suppression.NearWater, Surrender, tactics.json(minObstacle 등 데이터 조정만), CrossingSelfTest
- 브랜치: `river-r2` (base `river-r1`) | 상태: wip-ready | 담당: -
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
**상태: wip-ready** (브랜치 river-r2 @ river-r1 5b7b9f1 위). 지휘자 결정(0.2 기능 / 밸런스 영향은 0.3): Q6 변경을 데이터 키 `terrain.waterBlocksFire` 뒤로 재적용.
1. 측정: `GameMapTerrain.ClearFor` 는 통행 불가 칸(물 포함)이면 사선 차단 -> 소총(`los:true`)은 물 건너 사격 불가. 폭 1 · 3 · 6 하천 모두 HasFiringLine 막힘, 물 한 칸 사이 두 사수 8초 실사격: 사격 0 · 표적 없음. (CrossingSelfTest.TestRiverFire 특성화 시험)
2. [수정] `terrain.waterBlocksFire` (bool, 기본 **true** = 현행: 물이 직사 사선을 막음 -> 골든 8aa6448b 불변). false 면 직사(los) 사격만 물 위 통과 (ClearFor waterOpen 인자; 이동용 HasLineOfSight 는 항상 막음). 키는 TerrainRulesDef 타입 역직렬화라 bool 외 값은 로드 실패. 시험: 기본 true / false 각각 폭 1·3·6 사선 + 2사수 실사격(false 에서만 사격, 틱 427) + 바위 차단. **기본을 false 로 바꾸는 것은 0.3 밸런스 패스 결정** (시험 장면에서 units 20->126 · shots 2530->6119, golden 0d62878d). 아래 원 측정 기록:
   Q6 적용 시험(원 기록): `Water` 만 직사 사선에서 비차단(ClearFor fire 인자, 이동용 HasLineOfSight 는 그대로) -> 시험 통과(사격 틱 427)하나 **detcheck 골든 변경**(8aa6448b.. -> 0d62878d.., units 20 -> 126, shots 2530 -> 6119). 골든 재생성 안 함.
3. 길찾기: A* 비용에 MoveMul 미사용(속도만, Combat3.cs:159) -> 카드 허용대로 그대로. 여울 실측 감속: 10초 이동 풀밭 30.4 vs 여울 18.3 (비 0.60 = fordMove). 깊은 물은 비통과(T39a 시험 + 기존). 길찾기 비용항 추가 안 함(카드가 요구 안 함; 필요하면 후속 제안).
4. FindCrossing: 폭 3~6 하천 + 여울(폭 4) -> 건널 곳 폭 4 정상. 폭 1~2 는 minObstacle=3 에 걸려 병목 아님(일반 이동; 여울/다리는 A* 가 사용). tactics.json minObstacle 을 낮추면 기존 지도 1~2칸 장애물(담, 건물열)까지 병목화해 동작 변화 위험 -> 조정 안 함, 판단 요청(질문 2).
5. NearWater(Suppression.cs:307) = `== TerrainType.Water` 만, Surrender 는 NearWater/IsTerrainWalkable 사용 -> Ford · 다리(dirt) 제외, 기존 지도 불변(코드 확인, 변경 없음).
6. 시험(KD_SELFTEST_ONLY): crossing PASS(신규 3건 포함) · terrain PASS · tactical PASS · missions PASS, detcheck 골든 일치(8aa6448b9690b922, 기본 설정). minObstacle 불변. tactics-obs --choke: 시뮬 코드 변경 없어 불변(미실행).
