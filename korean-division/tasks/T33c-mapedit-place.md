# T33c 배치 층 (소품 · 시설 · 시작 · 구역 · 유닛) + 검사 패널 + 테스트 실행
- id: T33c | 역할: feature | 등급: D中 H1 (R1 SM Wlight)
- 선행: T33b | 후행: T33d (선택), 무거운 미션 편집기 (보류)
- 핫스팟/만질 파일: game-tools/map-editor/; 게임 Program.cs --map-file/--mission-file 플래그 (질문 3 승인 시, sim 영향 없음)
- 브랜치: kd/t33c-mapedit-place | 상태: queued | 담당: -
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
- 미션 JSON 저장 시 mapData/props/areas/deployZones 외 필드 바이트 불변
## 측정
T33a 골든 + 스크립트 시험 + 게임 detcheck 불변
## 결과 (3~5줄)
-
