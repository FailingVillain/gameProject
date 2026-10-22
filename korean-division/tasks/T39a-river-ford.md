# T39a 하천 1: Ford 지형 바이트 + 이동 배수 + 지도 paint 폭
- id: T39a | 역할: feature | 등급: D中 H2 (R2 S<=60k Wlight)
- 선행: 브랜치 `rail`(T07 wip-ready 9f513e7) 위 스택 | 후행: T39b T39c T39d
- 근거 문서: docs/RIVER_DESIGN.md (브랜치 spec/river-design @26e0b88; 이 카드 시작 시 해당 파일을 `git show spec/river-design:korean-division/docs/RIVER_DESIGN.md` 로 읽는다). 사용자는 세부 요구를 주지 않았다 -> 제안서 "권장" 답을 가정(아래 가정 목록). 로드맵 한 줄(ROADMAP_LONG 항목 8 "강 소형 포함")을 넘는 메커니즘 추가 금지.
- 핫스팟/만질 파일: Terrain.cs(enum 끝 Rail 다음 `Ford`), GameMapTerrain.MoveMul(`Ford => r.FordMove`), balance.json terrain.fordMove(가칭 0.6), MapFile.cs(paint `"width":N`, `LineTiles` 정사각 확장), Snapshot.FormatVersion +1, TerrainSelfTest
- 브랜치: `river-r1` (base `rail`), 작업 폴더 kd-develop/kd-river-r1 | 상태: queued | 담당: -
- FV: rail=53, 하천은 그 위 +1(제안서 ≈54). ops/FV_RESERVATIONS.md 는 이 카드에서 **수정 금지**; 지휘자가 행 추가(필요 사항: 기반=rail, FV=rail+1, Replay 변경 없음, breach2 53 / orderq2 54 / T30a 58 / T30b 59 와 겹침 -> 병합 시 재번호).
## 확정 결정
- 사용자 원문: "강 소형 포함 (하천 포함)" (ROADMAP_LONG 항목 8, 2026-10-21 진행 대기열). 그 외 세부 확정 없음.
## 범위 밖
수영 · 익사 · 홍수 · 다리 파괴 · 흐름 애니 · 여울 사격 보정 · 기존 지도(c0_02_yalu) 변경 · FV_RESERVATIONS 편집
## 수용 기준
- Ford: walkable=true, 모든 병종 통과, 엄폐 (1,1), 사선 불막음, 이동 x fordMove(기본 0.6; 경사 곱 유지).
- paint `{"terrain":"water","line":[...],"width":1..6}`, `{"terrain":"ford","rect":[..]}` 지도 로드; width 생략 시 현행과 동일(기존 지도 바이트 불변).
- 저장→불러오기 체크섬 일치, Audit 추가 없음, 기존 지도 detcheck 골든 불변(FV 만 +1 로 재생성 필요 시 사유 기록), snaptest 0 실패.
- `KD_SELFTEST_ONLY=terrain` 에 ford 블록(이동 배수 · 통행 · 사선 불변 · 곡선 line · 저장/로드).
## 측정
dotnet build + terrain selftest + detcheck + snaptest (기준선 docs/BASELINE_CURRENT.md); 밸런스 측정 보류(fordMove 는 가칭 유지).
## 가정한 답 (제안서 권장)
Q1 깊은 물 폭 1~6칸 · Q2 다리=현행 흙 덮기 유지(Bridge 는 T39d 선택) · Q3 여울 전 병종 통과 · Q4 감속만 · Q5 깊은 물 통행 불가 유지 · Q6 강 건너 사격 허용(T39b 가 측정) · Q7 하천 시험 지도만 · Q8 장갑열차와 별개(철교 제외).
## 결과
-
