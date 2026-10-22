# T44 전 미션 기능 스모크 하네스 (로드 · 종료 · 정지 감시)
- id: T44 | 역할: feature (측정 도구) | 등급: D中 H1 (R1 S<=100k Wlight) | 버전 0.2
- 선행: 없음 (base main) | 후행: 통합 리허설에서 상시 사용
- 파일: Tests/MissionSmoke.cs(신규) · SelfTest.cs 그룹 1줄(KD_SELFTEST_ONLY=smoke) · tools/smoke_missions.ps1 · reports/ 출력. 시뮬 코드 무변경.
- 브랜치 `kd/t44-smoke` (main 에서) | 상태: wip-ready
## 확정 결정
- 승률/밸런스는 보고하지 않는다 (밸런스 전부 보류, 사용자 2026-10-22). 기능 상태만.
- 가볍게: 단일 프로세스 직렬, 동시 heavy job 1개 이하 (heavy-job 상한).
## 방식
assets/missions/*.json 전수(동적 열거 -> 열차 시연 · 캠페인 미션 · ai_* 는 스택에 있으면 자동 포함) 를 MissionDef.Load -> World.CreateMissionWorld -> AttachMissionAi(MissionSelfTest.RunCli 와 같은 경로) 로 돌려, 종료(Winner != 0) 또는 시간 제한+30초까지. 미션당 벽시계 상한(기본 240초) 초과 = TIMEOUT. 지도 정의상 최대 인원 지도가 있으면 16인 AI 대 AI 1건 추가(없으면 '없음' 으로 보고).
## 범위 밖
승패 · 사상자 기대값 · 성능 수치(--stress 영역) · 미션 내용 수정 · T32b AI 스모크 대체(겹치면 재사용 우선)
## 수용 기준
1) 미션별 한 줄: LOAD_OK / 예외 없음 / 종료 사유(승자 있음 · 시간 제한 · 정지 · 타임아웃) / 틱 수 2) 정지 = 300 시뮬초 동안 킬 · 발사 · 이동 합이 0 이고 양측 생존 (정지 의심 목록으로 보고, 합법 정지 예: 호송 대기는 목록에 사유 표기) 3) 예외 발생 미션이 있으면 selftest FAIL, 정지/타임아웃은 경고 4) 동일 시드 2회 결과 동일 (결정성 확인 1건) 5) 전체 ≤ 15분, 실행 중 메모리/프로세스 1개
## 측정
KD_SELFTEST_ONLY=smoke · tools/smoke_missions.ps1 (reports/smoke_<날짜>.md)
## 질문
(1) 정지 판정 300초 기준 적절한가? (2) 16인 지도가 없으면 새 지도 추가는 이 카드 밖으로 두는가? (3) 통합 리허설 관문에 넣는가(지휘자 결정)?
## 결과
- 구현(feature/sonnet): src/Tests/MissionSmoke.cs(신규) · SelfTest.cs `smoke` 그룹 1줄 · tools/smoke_missions.ps1 (기본 exe = bin/t44, 공유 bin/Debug 아님). 시뮬 코드 무변경. 빌드 경고 0 오류 0.
- 질문 답 (카드 권고 채택): (1) 정지 기준 300 시뮬초(KD_SMOKE_STALL) (2) 16인 지도 없으면 보고에 'none', 새 지도는 카드 밖 (3) 통합 관문 편입 안 함.
- 정지 판정: 1 시뮬초마다 이동(>0.05)·사망·발사 하나라도 있으면 진행. 호송(escort) id 는 정지 시 '합법 후보' 표기. 벽시계 TIMEOUT(기본 240s)은 PC 부하에 따라 달라 비결정적 — 경고로만 취급.
- 환경 변수: KD_SMOKE_ID(한 미션) · KD_SMOKE_WALL · KD_SMOKE_STALL. 결정성 1건 = 첫 미션 60 시뮬초 2회 체크섬 동일(전체 실행 시).
- **검증 결과: 미실행 (heavy_gate 25분+ 대기로 막힘 — kd-rerender 의 `heavy_gate.ps1 -Render` 대기자 다수가 풀 점유). 미션별 상태 표는 비어 있음.** 게이트가 열리면: `tools/smoke_missions.ps1` 1회 (또는 KD_SELFTEST_ONLY=smoke) -> reports/smoke_<날짜>.md 를 이 표에 옮긴다.
| 미션 | 상태 |
|---|---|
| (미측정) | - |

- 2차 시도: 게이트 열림 직후 1회 실행에서 `.exe`(GUI 서브시스템)가 stdout 없이 즉시 반환해 NO_OUTPUT 로 나옴 -> 스크립트를 `dotnet <dll>` 호출로 수정(콘솔 호스트, 블록). 수정 뒤 재실행은 게이트 재점유(kd-rerender -Render 대기자)로 대기 중 중단 — 표는 여전히 미측정.
