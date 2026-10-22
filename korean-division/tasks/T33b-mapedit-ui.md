# T33b 지도 편집기 창 + 지형 브러시 UI + 스크립트/스크린샷 시험
- id: T33b | 역할: feature | 등급: D中 H1 (R1 SM Wlight)
- 선행: T33a (병합 전이면 그 브랜치 위 스택) | 후행: T33c
- 핫스팟/만질 파일: game-tools/map-editor/ (Raylib 창, 카메라, 도구 막대, 브러시 점/선/사각/원/채움, 지형 팔레트, 고지 토글, 격자, undo 단축키); 게임 지형 렌더 소스 링크
- 브랜치: kd/t33b-map-ui (kd/t33a-map-model 위 스택) | 상태: wip-ready | 담당: feature
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "게임 안 기능이 아니라 별도 도구로 만든다" (사용자 2026-10-19, game-tools/mission-editor/DESIGN.md) -- 질문 1 에서 재확인 중
- "가벼운 지도 편집기 = 로드맵: 자동 생성 지도를 사용자가 조금 수정 -- 지형 칠하기 · 참호/철조망 · 목표 · 시작 위치 · 저장" (사용자 2026-10-21); 보류 해제 2026-10-22 (지휘자 전달, 원문 확인 필요)
- 편집기 출력은 데이터 JSON 뿐: 시뮬 · FormatVersion · 리플레이 · 체크섬 불변
## 범위 밖
소품/시설/시작/구역/유닛 (T33c) · 테스트 실행 · 철도 (T33d) · 위젯 라이브러리 도입
## 수용 기준 (측정 가능)
- 지도 열기/저장/되돌리기, 지형 11종 + 고지가 게임 렌더와 같은 색/무늬로 표시
- --script <file> 입력 재생 (tool/drag/key) + --shot out.png 고정 프레임 스크린샷; 스크립트 결과 지도 해시 안정
- 시험 스크립트 3개 (브러시/사각+원/undo) PNG contact sheet 를 사용자 검수용으로 저장
- 편집 저장 지도를 게임 --map <id> --shot 으로 렌더한 비교 시트 1장
- 위젯은 버튼/숫자/목록만; 게임 빌드/detcheck 불변
## 측정
dotnet build · 스크립트 재생 해시 · 게임 --shot 비교 시트
## 결과 (3~5줄)
- game-tools/map-editor/MapEditor.App (Raylib exe MapEditor): 게임 MapRenderer/Theme/폰트 재사용(같은 색·무늬), 도구 6(브러시/선/사각/원/채움/지우개+우클릭), 팔레트 지형 11+고지 올림/내림, 격자·고지 보기, 크기 [ ], 되돌리기/다시/저장(^S, 임시 파일 후 교체 + 게임 로더 재적재)/열기 목록. 획은 마우스 뗄 때 확정(문서 연산 1개 = undo 1), 드래그 중 래스터 안 건드림.
- --script(tool/terrain/button/drag/path/key/assert/expect/shot) + --shot, --selftest(창 없음): 스크립트 4개 2회 재생 해시 동일(고정), 버튼 배치(3해상도)·모든 버튼 클릭 덮개·저장 왕복. 게임 코드 불변(detcheck 영향 없음).
- 스크린샷 docs/art_review/t33b/ (contact_sheet.png, compare_editor_vs_game.png). 게임 지도는 assets 에 덮어쓰지 않음(저장 기본 ./maps_edit; run_game.ps1 은 --out 을 PowerShell 인자로 오해해 --savedir 사용). 미션 mapData 열기는 T33c 이후. T33a 리뷰 수정 c5b30f4 체리픽 포함.
