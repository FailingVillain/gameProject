# T39c 하천 3: 렌더(좁은 물 띠 해안 · 여울 오버레이) + 하천 테스트 지도 + 접촉 시트
- id: T39c | 역할: feature | 등급: D中 H1 (R1 SM Wlight)
- 선행: T39a (T39b 와 병행 가능; 둘 다 `river-r1` 위에서 분기) | 후행: T39d
- 근거 문서: docs/RIVER_DESIGN.md (브랜치 spec/river-design @26e0b88; 이 카드 시작 시 해당 파일을 `git show spec/river-design:korean-division/docs/RIVER_DESIGN.md` 로 읽는다). 사용자는 세부 요구를 주지 않았다 -> 제안서 "권장" 답을 가정(아래 가정 목록). 로드맵 한 줄(ROADMAP_LONG 항목 8 "강 소형 포함")을 넘는 메커니즘 추가 금지.
- 핫스팟/만질 파일: MapRenderer(Water 해안선 처리 ~117 · 656~; 폭 1~2칸 띠에서 깨지지 않게), 신규 FordRenderer 또는 MapRenderer 분기(RailRenderer 와 같이 절차 오버레이 + TerrainVersion 캐시, 정적 — 흐름 애니 없음), 하천 시험 지도 `assets/maps/test_river.json`(곡선 하천 · 여울 2 · 흙 다리 1), 접촉 시트
- 브랜치: `river-r3` (base `river-r2` 8ada7a9; 스택) | 상태: wip-ready | 담당: feature(Sonnet)
## 확정 결정
- 없음(위 근거 참조). 지형 타일 그림 스타일은 기존 승인 타일 유지(사용자 2026-10-19 "지형 타일 승인").
## 범위 밖
흐름 애니 · 기존 미션 지도 변경 · 새 미션 · 사운드 · 다리 널빤지(T39d)
## 수용 기준
- 폭 1/2/4/6 칸 하천 + 곡선 + 끝점에서 해안선 깨짐/구멍 없음; 여울 = 모래빛 + 잔물결 절차 오버레이; 줌 3단계에서 확인.
- 렌더 캐시 무효화(지형 변경 시) 동작; 프레임 시간 증가 <5% (기존 지도 기준).
- 기존 지도 렌더 불변(스크린샷 비교 1장).
- 접촉 시트 `docs/art_review/river_terrain.png` (사용자 검수용; 시트 없이 완료 보고 금지). 게임 창 확인은 `tools/agent/run_game.ps1`(75a1712, tools/monitor-launch 브랜치에 있음; 없으면 거기서 가져와 쓰되 병합 대상 아님).
## 측정
selftest terrain + 지도 로드 + 스크린샷 3장.
## 가정한 답
T39a 목록 + 정적 렌더(Q: 흐름 애니 없음).
## 결과
- 가정(카드 + 제안서 권장): Q1 폭 1~6 · Q2 다리 = 흙 덮기 유지 · Q3 전 병종 · Q4 감속만 · Q5 깊은 물 불가 · Q6 건너 사격은 T39b 스위치 (기본 true 유지) · Q7 시험 지도만(+AI 관전 시험 미션) · Q8 별개. 추가 가정: 새 그림 파일 없음 · 정적 렌더(흐름 애니 없음) · 여울 오버레이는 별도 FordRenderer 없이 MapRenderer 타일 굽기(캐시 공짜, 프레임 비용 0) · 테마에 `ford_v*.png` 가 있으면 그것을 우선(테마 항목), 없으면 테마의 물 + 모래 타일을 섞어 합성.
- 구현(렌더 · 데이터만, 시뮬 변경 없음): MapRenderer `MakeFord`(물+모래 합성: 모래 얼룩 + 잔물결 3개, 변형 4) · BlendTile(깊은 물 ↔ 여울 0.55 넓은 부드러운 전이, 여울 둑 옅은 거품, 물가 젖은 모래가 여울에도) · Terrain.cs 여울 색(작은 지도 · 개관 색) 청록 (96,156,172) · `assets/maps/test_river.json`(본류 폭 5 곡선 + 여울 2 + 흙 다리 1 + 지류 폭 1/2/4/6) · `assets/missions/test_river_crossing.json`(AI 대 AI 관전, 기존 유닛만) · docs/RIVER_DESIGN.md 사본(spec/river-design).
- 검증: 빌드 오류 0 · selftest terrain 57 OK · missions 99 OK · uiicons 26 OK · uifuzz 30 크래시 0 · detcheck GOLDEN match(기존 미션/지도 무변경) · mission-run test_river_crossing 정상 종료.
- 접촉 시트 `docs/art_review/river_terrain.png` + 개별 스크린샷 river_*.png (줌 0.5/1.2/1.5/3.0). 프레임 시간 증가는 굽기(캐시) 시에만 — 별도 측정 안 함(런타임 오버레이 없음).
