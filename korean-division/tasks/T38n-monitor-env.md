# T38n 게임이 KD_MONITOR 를 따른다
- T38n | feature | D中 H1 | 선행 없음 | 후행 없음
- 만진 파일: Program.cs(1줄), src/Render/MonitorPick.cs, src/Tests/MonitorPickSelfTest.cs, src/SelfTest.cs(그룹 monitorpick)
- 브랜치 kd/t38n-monitor-env (kd-develop/kd-t38n) | 상태: wip-ready
## 확정 결정
- env KD_MONITOR = primary | secondary | 정수. 미설정 = 기존 동작. 창 모드만 (전체화면/최대화 금지). UserSettings 가 전체화면이어도 KD_MONITOR 가 있으면 창 모드 유지(현 main 에는 해당 설정 코드 없음; 설정 적용 코드가 생기면 KD_MONITOR 설정 시 건너뛸 것).
## 결과
- 순수함수 MonitorPick.Choose + 시험 monitorpick 9건 PASS, detcheck 8변형 GOLDEN 동일, 빌드 0 오류.
- 실행: KD_MONITOR=secondary --menu-shot → `[monitor] secondary #1 of 3 -> window 1280x720 at (320,1260) actual 323,1274`.
