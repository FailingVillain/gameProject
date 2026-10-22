# T33a 지도 편집 문서 모델 + 직렬화 + undo (헤드리스)
- id: T33a | 역할: feature | 등급: D中 H1 (R1 S Wlight)
- 선행: 없음 (설계 docs/MAP_EDITOR_DESIGN.md) | 후행: T33b
- 핫스팟/만질 파일: game-tools/map-editor/ (신규 csproj + Core 소스 링크, 게임 csproj 불변); src/Core/MapFile.cs 는 읽기만 (접근 노출 최소)
- 브랜치: kd/t33a-map-model (main 기준) | 상태: wip-ready | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "게임 안 기능이 아니라 별도 도구로 만든다" (사용자 2026-10-19, game-tools/mission-editor/DESIGN.md) -- 질문 1 에서 재확인 중
- "가벼운 지도 편집기 = 로드맵: 자동 생성 지도를 사용자가 조금 수정 -- 지형 칠하기 · 참호/철조망 · 목표 · 시작 위치 · 저장" (사용자 2026-10-21); 보류 해제 2026-10-22 (지휘자 전달, 원문 확인 필요)
- 편집기 출력은 데이터 JSON 뿐: 시뮬 · FormatVersion · 리플레이 · 체크섬 불변
## 범위 밖
창/UI · 소품/시설 · 열차 · 미션 forces/events · line/rail (T33d) · 게임 코드 변경
## 수용 기준 (측정 가능)
- 문서 모델: paint op (rect/circle/tiles/high) + 선 래스터(tiles) + 채우기 + base; 연산 = 가역 레코드, undo/redo
- assets/maps/*.json 6개 전부 열기 -> 저장 -> MapFile.LoadText 한 타일/고지/시작/시설 격자가 원본과 동일
- 같은 연산열 -> 같은 출력 바이트 (골든 해시), undo 전부 = 원본 해시
- 미션 mapData 치환 (c0_02_yalu): 나머지 필드 불변, MissionDef.Load 통과
- 헤드리스 시험 통과, 게임 빌드/detcheck 불변
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
dotnet build 게임 + 편집기 · 새 시험 · 게임 --detcheck 불변
## 결과 (3~5줄)
- game-tools/map-editor/{MapEditor.Model, MapEditor.Tests}: MapDoc (paint/starts/sites/units + 알 수 없는 필드 원문 보존, 가역 명령 undo/redo, 획 묶음, 선=tiles, 채우기=1행 rect) + MissionMapData (mapData 만 텍스트 치환, map 참조 내장 복사). 게임 csproj/코드 불변.
- 시험 `dotnet build` 후 MapEditor.Tests.dll: pass 204 fail 0 (지도 6개 왕복 + 래스터=게임 격자, 미션 mapData 17개 치환/MissionDef.Parse, 편집 골든 5BDE9FE433DD6FD1, undo 전부=원본 해시). 게임 --detcheck GOLDEN match.
- 설계 8질문 기본값: 1 별도 exe 2 paint 줄 앞 // 주석 보존, 나머지 주석은 DroppedComments 로 경고 3 게임 플래그 추가 안 함(T33c) 4 sites+House 만 5 units 는 지도 파일 units 만 6 tiles 로 굳히기 7 T33d 는 rail 위 스택 8 game-tools/map-editor/.
시작 전: ops/MERGE_QUEUE.md 병합 완료 표 + docs/legacy/GIT_HISTORY_DIGEST.md 를 grep 해 이미 구현됐는지 확인.
## 리뷰 수정 (2차 커밋)
- 앞으로 호환: PaintOp/SiteDef/UnitDef 가 모르는 키 (rail line/width, ford 등) 를 원문 Extra 로 보존해 그대로 재출력; 모르는 지형 줄은 Raster 에서만 건너뜀 + Validate(warnings) 경고 (파일에서는 안 지움). 여분 키 안 // 주석은 보존으로 셈.
- 입력 검증: 크기 1..1024 (Raster 도 거부), 원 반지름 0..1024 + long 연산, tiles 원소 길이 2, Parse 는 InvalidDataException 하나로만 실패 + TryParse, 중복 최상위 키 (대소문자 무시) Validate 오류.
- 시험: battle_flat 인라인 fixture (CRLF 기준, 골든 5BDE9FE433DD6FD1 동일), 시험 264 pass 0 fail. 리플렉션 유지: InternalsVisibleTo 는 게임 csproj/MapFile 변경이 필요해 보류 (게임 불변 원칙).
