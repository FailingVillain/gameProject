# C1E1-m1 MAP-A 지도 생성 (압록강 서쪽 어귀, medium)
- id: C1E1-m1 | 역할: feature | 유형: map | 모델: sonnet | 등급: D中 H0 (W light)
- 선행(blockedBy): 없음 (후행: s1 s2 s3) | 병렬/직렬: 병렬
- 핫스팟/만질 파일: tools/c1e1/gen_map_a.py (신규) · assets/maps/c1e1_map_a.json (신규) · docs/C1E1_MAPS.md (구역 표)
- 브랜치: kd/c1e1-m1-map-a (base: main) | 상태: wip-ready | 담당 에이전트: feature(sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22 지형 초안 승인 (spec '지형 초안'): 남안 평야·갈대·제방, 강 동서(폭 20~30칸 축약), 여울 2곳(상류 모래톱·하류 갈대섬) + 주 도하 지점 1곳(임시 부교), 압록강철교는 일본 관리 구간이라 도하 불가(외교 문제).
- A-북안: 갈대밭·제방·소 나루 마을 1(가옥 약 10)·서쪽 소구릉 1(기관총 거점)·동쪽 소나무 숲. A-내륙: 마을 1(가옥 8~12)·밭·외곽 숲. SK2 소초 = A-북안 외곽(창고·연락소).
- 지도 크기 medium (Config.MapTiers 800). 지도는 자동 생성 후 사용자 수정, 지형 세부는 [미정] 구술 대기.
- 원문: campaign/spec_c1e1.txt (v0.2) · 계획 docs/C1E1_IMPLEMENTATION_PLAN.md. 명세에 없는 것은 만들지 말고 docs/DECISIONS_PENDING.md 끝에 한 줄(= ops/DECISIONS 후보) 적고 속행.
## 범위 밖
미션 JSON(forces/events) · Ford/Bridge 지형(river 병합 전이라 water + sand 틈으로 여울) · 철도 · 사용자 구술 반영 · 편집기 작업 · 호감·사기 값(보류) · 서사 문구(층 3) · E2 설계 · 밸런스 조정(보류, 사용자 2026-10-22)
## 수용 기준 (측정 가능)
- 생성 스크립트 하나로 c1e1_map_a.json 재생성 가능 (가로세로 인자, 같은 입력 = 같은 바이트).
- MapFile.LoadText 통과, --map c1e1_map_a --shot 으로 1장 렌더 + 구역 표시 시트(사용자 검수용).
- docs/C1E1_MAPS.md 에 구역 이름 표 (a_south a_river a_north_bank a_inland a_ford_up a_ford_down a_main_crossing a_village a_hill_mg a_pine_east a_outpost a_escape) 와 좌표.
- 여울 3지점(2 + 주 도하) 통행 가능, 그 밖 강 구간 통행 불가를 경로 시험으로 확인.
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
dotnet build · selftest(관련 묶음) · detcheck · snaptest · 기준선 docs/BASELINE_CURRENT.md
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
- gen_map_a.py -> c1e1_map_a.json 800x800 (같은 입력 = 같은 md5), 구역 표 13개 docs/C1E1_MAPS.md. 여울 3곳(폭 14) 외 강 전폭 water 통행 불가: `KD_SELFTEST_ONLY=c1e1maps` 12/12 OK (신규 src/Tests/C1e1MapsSelfTest.cs). build 0/0, --shot 1장 + 구역 시트 PNG(워크트리 루트 c1e1_map_a_shot.png / c1e1_map_a_zones.png, 미커밋). detcheck 는 시뮬 무변경이라 생략.
- 질문(DECISIONS_PENDING): 갈대 지형 신설 여부, a_escape 위치 기본값(북단 중앙).
