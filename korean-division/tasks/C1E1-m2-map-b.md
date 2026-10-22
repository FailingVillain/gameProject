# C1E1-m2 MAP-B 지도 생성 (안동 일대, large)
- id: C1E1-m2 | 역할: feature | 유형: map | 모델: sonnet | 등급: D中 H0 (W light)
- 선행(blockedBy): 없음 (후행: s4 s5 s6) | 병렬/직렬: 병렬
- 핫스팟/만질 파일: tools/c1e1/gen_map_b.py (신규) · assets/maps/c1e1_map_b.json (신규) · docs/C1E1_MAPS.md (MAP-B 절)
- 브랜치: kd/c1e1-m2-map-b (base: main) | 상태: wip-ready | 담당 에이전트: feature(sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22 지형 초안 승인: 무성곽 시가지(불규칙 골목·화상가·목재 야적장·부두) = 성내 800 구역, 진강산(서북 독립 구릉 약 130m, 수비 야포 4문 포대), 외곽 방어선(북·서 참호 + 도로 차단) = 외곽 참호 500 구역.
- 2026-10-22 일본 철도 부속지(격자형 신시가지)는 발포 금지 중립 구역 [확정]. 철도 지형 없이 일반 지형 격자 도로로 표시 (R9 대체안).
- 북부 마을·북부 평야(SK3·SK4). 포위 차단 지점 2곳 = 도로 차단(봉황성 방면) + 부두 봉쇄. 지도 크기 large (Config.MapTiers 1200).
- 원문: campaign/spec_c1e1.txt (v0.2) · 계획 docs/C1E1_IMPLEMENTATION_PLAN.md. 명세에 없는 것은 만들지 말고 docs/DECISIONS_PENDING.md 끝에 한 줄(= ops/DECISIONS 후보) 적고 속행.
## 범위 밖
미션 JSON · 철로(T07) · 부속지 규칙 코드(TC-G) · 사용자 구술 반영 · 호감·사기 값(보류) · 서사 문구(층 3) · E2 설계 · 밸런스 조정(보류, 사용자 2026-10-22)
## 수용 기준 (측정 가능)
- 생성 스크립트 하나로 재생성 가능 (인자: 가로세로).
- 구역 표 (b_city b_outskirts b_north_village b_north_plain b_annex b_jingang_hill b_dock b_road_block b_hq b_flank_path b_sw_start) 좌표를 문서화.
- --map c1e1_map_b --shot 1장 + 구역 시트, MapFile.LoadText 통과.
- 부속지 폴리곤이 시가지와 분리된 연결 영역임을 시험 (TC-G 가 읽을 좌표).
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
dotnet build · selftest(관련 묶음) · detcheck · snaptest · 기준선 docs/BASELINE_CURRENT.md
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
- gen_map_b.py(1200x1200, --check/--areas/--sheet) + assets/maps/c1e1_map_b.json + docs/C1E1_MAPS.md(MAP-B 절). 구역 11개 좌표표 문서화, 부속지(792,264,216x192)는 시가지와 24칸 분리·연결 영역 (--check PASS, 시작->지휘소/부속지 도보 연결).
- 시험: dotnet build 오류 0, --map c1e1_map_b --shot 로드 OK, KD_SELFTEST_ONLY=mapsizes PASS. 시뮬 코드 무변경이라 detcheck/snaptest 생략. 사진: docs/c1e1_shots/.
- 질문(DECISIONS_PENDING): 부속지 위치(시가 북동), SK1 우회로 모양, 목재 야적장=rubble 은 임의 가정.
