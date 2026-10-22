# T33c 배치 층 (소품 · 시설 · 시작 · 구역 · 유닛) + 검사 패널 + 테스트 실행
- id: T33c | 역할: feature | 등급: D中 H1 (R1 SM Wlight)
- 선행: T33b | 후행: T33d (선택), 무거운 미션 편집기 (보류)
- 핫스팟/만질 파일: game-tools/map-editor/; 게임 Program.cs --map-file/--mission-file 플래그 (질문 3 승인 시, sim 영향 없음)
- 브랜치: kd/t33c-placement (kd/t33b-map-ui 5207b8f 위 스택) | 상태: wip-ready | 담당: feature (sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "게임 안 기능이 아니라 별도 도구로 만든다" (사용자 2026-10-19, game-tools/mission-editor/DESIGN.md) -- 질문 1 에서 재확인 중
- "가벼운 지도 편집기 = 로드맵: 자동 생성 지도를 사용자가 조금 수정 -- 지형 칠하기 · 참호/철조망 · 목표 · 시작 위치 · 저장" (사용자 2026-10-21); 보류 해제 2026-10-22 (지휘자 전달, 원문 확인 필요)
- 편집기 출력은 데이터 JSON 뿐: 시뮬 · FormatVersion · 리플레이 · 체크섬 불변
## 범위 밖
미션 forces[] 편성 · events · 목표 종류/텍스트 편집 · 열차 · 장식 독립 건물 형식 신설 (질문 4) · 철도
## 수용 기준 (측정 가능)
- props[] 배치/삭제 (props.json 종류 팔레트), propScatter/density/cell 숫자 + ScatterProps 미리보기 (같은 시드 = 같은 소품)
- sites[] 배치 (capturable 키, owner), starts[] 드래그, areas{}/deployZones 사각·원, 지도 units[] 배치
- 검사 패널: 시작 지도 밖/통행불가/갇힘, 구역 참조 끊김, 소품·시설 겹침 -- 항목 클릭 시 이동
- F5 = 저장 후 게임 --map / --mission --mission-start 실행; 편집 -> 저장 -> 게임 로드 왕복 시험 3건 + 스크린샷 시트
- 미션 JSON 저장 시 바뀐 속성(mapData/props/areas/deployZones) 값만 다시 쓰고 그 밖 바이트는 그대로 (지도 파일은 저장 때 정규화되어 바이트 보존이 아님; 바뀐 속성 값 안 // 주석은 사라지고 상태줄에 경고)
## 측정
T33a 골든 + 스크립트 시험 + 게임 detcheck 불변
## 질문 기본값 (설계 문서 9절, 무응답 기본값 채택)
- Q1 별도 exe (게임 안 편집 모드 아님) · Q2 미션 `//` 주석: mapData 안 paint 줄 앞 주석은 보존, 바뀐 속성 값(props/areas/deployZones) 안 주석만 사라짐 (CommentsAtRisk 로 센다), 바뀌지 않은 속성은 원문 바이트 그대로 · Q4 건물 = sites[] + House 지형 칸만 (장식 건물 형식 신설 안 함) · Q5 유닛은 지도 units[] 만 (forces[] / 열차 범위 밖) · Q8 위치 game-tools/map-editor/.
- Q3 (게임 `--map-file` / `--mission-file` 플래그) 는 **채택하지 않음**: 이 카드는 게임 코드 불변이 조건이라, 편집기 폴더의 assets 가 게임과 같은 에셋 폴더라는 점을 이용해 임시 id `_editor_test` 로 써 놓고 기존 `--map` / `--mission --mission-start` 를 그대로 호출한다 (게임 플래그 추가 시 이 방식으로 되돌리면 됨).
## 결과 (3~5줄)
- game-tools/map-editor: 배치 도구 6개 (소품 P · 시설 S · 시작 T(끌기) · 구역 Z · 배치구역 X · 유닛 U), 검사 패널 (시작 지도 밖/통행불가/갇힘/이어짐/가까움, 시설·소품 겹침, 구역 참조 끊김·목표 도달성, 종류·세력 범위; 항목 클릭 = 이동), 테스트 실행 F5 (저장 -> `_editor_test` 임시 사본 -> run_game.ps1 으로 보조 모니터에 `--map` / `--mission --mission-start`; 오류가 있으면 실행 안 함), 열기 목록에 게임 미션 · 편집본 탭. 게임 코드 불변 (git diff korean-division/ = 카드 · 문서 · 스크린샷만).
- 미션 편집: MissionDoc 이 mapData 또는 map 참조(저장 시 내장) + props · propScatter/Density/Cell · areas · deployZones 를 편집하고 바뀐 속성의 값만 원문에 치환 (모르는 키 보존, 바뀐 속성 밖 바이트 불변, 편집 없이 저장 = 원문 동일; 바뀐 속성 값 안 // 주석은 사라지며 상태줄 경고. 지도 파일은 정규화 저장이라 바이트 보존이 아님). 흩뿌리기 미리보기 = 게임 `ScatterProps` 를 미션 시드로 호출 (병력 둘레 비우기만 at 위치로 근사).
- 시험: MapEditor.Tests pass 582 / fail 0 (전 미션 17개 + 지도 6개: 열기 -> 저장 (미션: 편집 없으면 원문 바이트 동일 · 지도: 정규화 후 의미 동일), 편집 후 편집 밖 속성 원문 동일, undo = 원문, 게임 MissionDef.Parse · MapFile.Load 왕복 3건, 검사기 오류 0 · 합성 오류 검출). 편집기 `--selftest` pass 69341 / fail 0 (스크립트 12개 x2 재생 해시 동일, 3 해상도 x 도구 12 x 지도/미션 버튼 배치, 모든 버튼 id 클릭 덮개, 미션 저장 왕복, F5 파일).
- 편집기 전용 배율 최소 0.08 (게임 카메라 0.35 는 큰 지도 전체가 안 보임; Camera2D 필드 직접 사용). brush.kds 기대 해시 갱신 (지도 영역이 검사 패널만큼 줄어 배율이 달라지며 꺾은선 표본 칸이 바뀜; 의미 assert 는 그대로 통과).
- 스크린샷 · 시트: docs/art_review/t33c/ (contact_sheet.png, compare_editor_vs_game.png + 개별 PNG). 실제 창 실행 확인: `MapEditor --script ... --real-launch` 로 `KoreanDivision.exe --mission _editor_test --mission-start` 가 보조 모니터(DISPLAY1)에 뜸.
