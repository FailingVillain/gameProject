# T33e 지도 편집기 큰 지도 (상한 1500)
- id: T33e | 역할: feature | 등급: D中 H1 | 선행: T33d (브랜치 kd/t33d-rail 위 스택) | 상태: wip-ready (브랜치 kd/t33e-map-size) | 담당: feature(sonnet)
- 범위: 편집기 도구 코드만 (game-tools/map-editor), 게임 코드 불변.
## 결과
- MapDoc.MaxDim 1024 -> 1500 (= Config.MaxMapSize; 모델은 게임 비의존이라 값을 베끼고, MapEditor.Tests 가 `MapDoc.MaxDim == Config.MaxMapSize` 를 대조해 게임 상수가 바뀌면 실패).
- 시험 BigMapTests: 에피소드 지도 A(800) · B(1200) 사본(MapEditor.Tests/fixtures, 13KB · 72KB; 없으면 건너뜀) + 합성 1200 / 1500: 검사, 편집기 격자 == 게임 격자, 저장 왕복, 채우기, undo/redo 해시 복원, undo 200획 메모리.
- 고침: Inspector 연결 덩어리 전체 라벨링 (커밋마다 2.25M칸, 대리자 호출 8방향) -> 통행 격자 1회 + 질의된 칸에서만 지연 채움 (결과 동일); Fill BFS 칸마다 배열 할당 제거.
- undo 는 이미 paint 줄 단위 델타 (래스터 스냅샷 아님) -> 변경 없음. 지도 편집기에 미니맵은 없음 (확대/축소/맞춤만).
- KD_EDITOR_PERF=1 환경변수: 편집기 창이 끝날 때 프레임 시간 · Refresh 시간 한 줄 출력 (진단용).
## 측정 (1280x720 창, Release 아님 기본 빌드)
- 헤드리스: 파싱+래스터 1200 = 7ms, 게임 로드 1200 = 32ms / 1500 = 46ms, 채우기 1500 = 12ms, undo+래스터 = 2ms, undo 200획 +1~2.5MB.
- 창: 프레임 중앙값 16.66ms (60fps 유지), 커밋 프레임(Refresh+렌더러 재생성) 1500: 이전 약 340ms -> 약 165ms (inspect 220 -> 85ms, 게임 로드 ~90, 렌더러 ~25); 첫 열기 프레임 1.2~1.6s; 작업 메모리 1200 약 500MB / 1500 약 670MB.
- 시험: MapEditor.Tests pass 625 fail 0, 편집기 --selftest pass 81413 fail 0.
## 남은 것 (제안)
- 커밋마다 게임 지도를 통째로 다시 굽는 Refresh (게임 로드 ~90ms + 렌더러 ~25ms @1500) 는 게임 코드라 그대로. 더 줄이려면 증분 렌더러가 필요 (게임 코드 변경 -> 별도 결정).
