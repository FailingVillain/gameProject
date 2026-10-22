# T39c 하천 3: 렌더(좁은 물 띠 해안 · 여울 오버레이) + 하천 테스트 지도 + 접촉 시트
- id: T39c | 역할: feature | 등급: D中 H1 (R1 SM Wlight)
- 선행: T39a (T39b 와 병행 가능; 둘 다 `river-r1` 위에서 분기) | 후행: T39d
- 근거 문서: docs/RIVER_DESIGN.md (브랜치 spec/river-design @26e0b88; 이 카드 시작 시 해당 파일을 `git show spec/river-design:korean-division/docs/RIVER_DESIGN.md` 로 읽는다). 사용자는 세부 요구를 주지 않았다 -> 제안서 "권장" 답을 가정(아래 가정 목록). 로드맵 한 줄(ROADMAP_LONG 항목 8 "강 소형 포함")을 넘는 메커니즘 추가 금지.
- 핫스팟/만질 파일: MapRenderer(Water 해안선 처리 ~117 · 656~; 폭 1~2칸 띠에서 깨지지 않게), 신규 FordRenderer 또는 MapRenderer 분기(RailRenderer 와 같이 절차 오버레이 + TerrainVersion 캐시, 정적 — 흐름 애니 없음), 하천 시험 지도 `assets/maps/test_river.json`(곡선 하천 · 여울 2 · 흙 다리 1), 접촉 시트
- 브랜치: `river-r3` (base `river-r1`) | 상태: queued | 담당: -
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
-
