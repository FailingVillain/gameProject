# T33d (선택) 지도 편집기 철도 지형 도구
- id: T33d | 역할: feature | 등급: D中 H2 (R1 S Wlight)
- 선행: T33b + T07 (rail 브랜치 wip-ready) -- **rail 브랜치 위에 스택**, main 단독 불가 | 후행: 없음
- 핫스팟/만질 파일: game-tools/map-editor/ 직렬화 (line op), 팔레트 (rail); 게임 MapFile line 은 rail 브랜치 것 그대로 (수정 금지)
- 브랜치: kd/t33d-mapedit-rail (rail 위) | 상태: wip-ready (브랜치 kd/t33d-rail, kd/t33c-placement 위 스택 -- 지시로 rail 위가 아니라 T33c 위) | 담당: feature(sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "게임 안 기능이 아니라 별도 도구로 만든다" (사용자 2026-10-19, game-tools/mission-editor/DESIGN.md) -- 질문 1 에서 재확인 중
- "가벼운 지도 편집기 = 로드맵: 자동 생성 지도를 사용자가 조금 수정 -- 지형 칠하기 · 참호/철조망 · 목표 · 시작 위치 · 저장" (사용자 2026-10-21); 보류 해제 2026-10-22 (지휘자 전달, 원문 확인 필요)
- 편집기 출력은 데이터 JSON 뿐: 시뮬 · FormatVersion · 리플레이 · 체크섬 불변
- "철도 지형은 추가를 하는걸로 하자" (사용자, 날짜 확인 필요 -- T07 카드와 동일)
## 범위 밖
장갑열차/화물열차 · 선로 파괴 · 다리/여울 전용 타일 (N52) · rail 병합 자체
## 수용 기준 (측정 가능)
- 꺾은선 도구 -> paint terrain rail + line 저장, 로드 후 rail 타일 격자가 편집기와 동일
- rail 포함 지도 열기/저장 왕복 동일; T33a 골든 전부 유지
- RailRenderer 표시 (꺾임/대각 연결) 가 편집기와 게임에서 같음 (비교 시트)
## 측정
rail 브랜치 TerrainSelfTest + 편집기 왕복 시험
## 가정한 답 (카드 질문에 사용자 답이 없어 아래로 진행 -- 뒤집으면 알려 주세요)
1. 철도는 별도 도구(편집기) 안의 지형 도구: 게임 기능 아님 (확정 결정 그대로).
2. 도구 = 꺾은선 (클릭으로 꺾임점, Enter/우클릭 확정, Backspace 점 취소, Esc 취소, 키 N). 저장 형식 = 게임 paint `line` (rail 브랜치 형식) -- tiles 로 풀지 않음.
3. 지형 = rail 고정 (팔레트에서도 칠할 수 있음), 폭 UI 없음 (`width` 키는 river-r2 형식대로 읽고 그대로 쓰고 래스터에만 반영, 1~6 검사).
4. 편집기 안 선로 표시는 게임 RailRenderer 와 같은 규칙 · 치수로 편집기가 직접 덧그림 (게임은 BuildingRenderer 가 그림 -> 편집기 렌더러가 못 그림).
5. ford(12) · bridge(13) 는 일반 지형 이름/팔레트로만 (전용 도구 없음, N52 범위 밖 유지).
## 결과
- MapDoc: PaintOp.Line/Width, PaintPolyline, MapGrid.RailSegments, TerrainNames += rail ford bridge (바이트 11/12/13), Editor Tool.Rail (+ 팔레트 3개, 키 N), View 선로 덧그리기. 시험: MapEditor.Tests pass 599 fail 0, 편집기 --selftest pass fail 0 (rail.kds, buttons.kds 갱신).
- 게임 코드 불변. rail + river-r4-bridge 를 합친 임시 통합본(미커밋 워크트리)에서: 게임 LoadText 격자 = 편집기 격자, 저장 시 게임 검증 통과, test_river 격자 일치 확인. 스크린샷 docs/art_review/t33d (game_rail.png = 게임, editor_rail_zoom.png = 편집기: 같은 모양).
## 병합 시 다시 맞출 것 (rail · river 브랜치가 main 에 들어온 뒤)
- 이 브랜치는 rail 이 없는 게임 위라 편집기에서 rail/ford/bridge 를 칠하면 미리보기 게임 로드가 실패 (상태줄 "게임 로드 실패", 마지막 성공 화면 유지) 하고 저장(게임 검증)이 거부됨 -- rail, river-r4-bridge(= r1~r3 포함) 병합 뒤에는 정상. 순서: rail -> river r1..r4 -> T33a..d.
- MapDoc.TerrainNames 번호 11/12/13 이 게임 TerrainType 과 같은지 확인 (rail=11, ford=12, bridge=13).
- Editor.SwatchColor 의 rail/ford/bridge 색은 각 브랜치 Terrain.cs 값 복사: 병합 뒤 Terrains.Get 으로 바꿔도 됨. View.DrawRails 는 RailRenderer.Draw 복제: 병합 뒤 RailRenderer.Draw(PreviewMap, ...) 로 교체 가능.
- RailTests 의 게임 대조는 rail 을 아는 게임에서만 자동으로 켜짐 (모르면 건너뜀 문구).
- 알려진 선행 이슈 (T33c 시험 vs river 브랜치 자산): assets/missions/test_river_crossing.json 은 `"map"` 참조 미션이라 PlacementTests 의 "편집 없이 ToText = 원문 바이트 동일" / mapData 가정이 깨짐 (미션 쪽 T33c 시험, rail/ford 와 무관) -- 병합 때 시험 쪽에서 map 참조 미션을 건너뛰게 고칠 것.
- 지도에 `line` 을 쓰면 rail 브랜치가 없는 main 게임은 그 지도를 못 읽음 (선로 도구는 rail 병합 후에만 쓸 것).
- 리뷰 수정 (9b72f2f 후속): (1) 게임 원본 에셋 경로(`korean-division/assets/`, assets/maps · missions)에 대한 저장은 `--allow-source-overwrite` 없이는 거부 (`--file` 로 원본을 열어도 안전); (2) 테스트 실행 임시 id = `_editor_test_<pid>` 프로세스 고유, 편집기 종료 때 사본 삭제; (3) 문구 정정: 지도는 저장 때 정규화(바이트 보존 아님), 미션은 mapData 등 바뀐 속성 밖 바이트 유지 · 바뀐 속성 안 주석은 사라지며 상태줄 경고.
