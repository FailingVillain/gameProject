# T41 충돌 로그 (0.2 설정/배포)
- id: T41 | 역할: feature | 등급: D中 H1 (선택 메모: R1 S Wlight)
- 선행: 없음 | 후행: -
- 핫스팟/만질 파일: src/UI/CrashLog.cs(신규), src/Tests/CrashLogSelfTest.cs(신규), Program.cs(훅 · 컨텍스트), src/Game.cs(ShowNotice 한 줄), src/SelfTest.cs(그룹 한 줄). 시뮬 · 저장 형식 변경 없음.
- 브랜치: kd/crash-log (kd-develop/kd-crash-log) | 상태: wip-ready | 담당 에이전트: feature(Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 로드맵 '설정/배포 … 충돌 로그' (버전 0.2): 처리 안 된 예외 · 셀프테스트 치명 오류 시 시각 이름 로그 파일, 최근 10개 순환, 예외는 그대로 전파, 로그 쓰기는 절대 던지지 않음, 시뮬 변경 없음 (2026-10-22)
- 초안 값 확인 필요: 폴더 = KD_CRASH_DIR → KD_CAMPAIGN_DIR/crash → %LOCALAPPDATA%/KoreanDivision/crash (사용자 설정 파일이 따로 없어 캠페인 저장 폴더 옆에 둠)
## 범위 밖
충돌 시 사용자 대화상자 · 자동 업로드 · 로그 뷰어 UI · 시뮬 로그
## 수용 기준 (측정 가능)
로그에 예외 형식/메시지/스택, 게임 버전, Snapshot/Replay FormatVersion, 화면/미션/틱/시드, 자동 저장 경로, 최근 알림 50줄. 10개 초과 시 오래된 것 삭제. KD_TEST_CRASH=1 시험 훅. `KD_SELFTEST_ONLY=crashlog` PASS.
## 측정
빌드 · crashlog 그룹 · uiicons · uifuzz 30 · detcheck GOLDEN 불변.
## 결과
빌드 OK · crashlog 그룹 9/9 PASS · uiicons PASS · uifuzz 30 크래시 0 · detcheck 8변형 동일 GOLDEN match · KD_TEST_CRASH=1 실행 시 crash/ 로그 생성 확인.
