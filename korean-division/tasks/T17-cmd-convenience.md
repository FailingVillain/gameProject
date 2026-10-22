# T17 지휘 편의 (미션 중 저장 · 전과 보고)
- id: T17 | 역할: feature | 등급: D中 H2 (선택 메모: R2 SM Wlight)
- 선행: T08 (스택: t08-ui-bundle 위) | 후행: -
- 핫스팟/만질 파일: Game.cs(+22줄, T04 orderq 와 충돌 주의) · GameMission.cs · SaveManager.cs · MissionResults.cs · MoveStats.cs · Combat3.cs · StringsMission.cs · CommandSelfTest.cs. 시뮬/저장 형식 무변경(FV 불변).
- 브랜치: t17-cmd-convenience (kd-develop/kd-t17, t08-ui-bundle 위 스택) | 상태: wip-ready | 담당: feature(Sonnet)
## 확정 결정 (ROADMAP_LONG '지휘 편의' 2026-10-21 승인)
- ① 미션 중 저장 + 큰 교전 직전 자동 저장 (HUD 저장 버튼/F6 는 기존, 자동 저장 = BattleAutosave).
- ⑤ 전과 보고 화면 (= 작전 후 화면). wip/battlereport 패치 복원.
- ⑦ 교전 알림 · 미니맵 표시 = '추후 심도 논의' (미승인) -> 이번 범위 밖, 구현 안 함. 제안만.
## 범위 밖
교전 알림/미니맵(⑦), 속도 조절(②), 부대 목록(③), 명령 대기열(④=T04), 병종 도감(⑥).
## 수용 기준
빌드 OK, selftest 관련 그룹 + savecompat PASS, uifuzz 30 PASS, detcheck 불변.
## 측정
KD_SELFTEST_ONLY=<command/mission 그룹>, savecompat, --uifuzz 30.
## 결과
battlereport 패치 복원(ENGINE_NOTES 충돌만 수동 추가), Game.cs +22줄. 빌드 OK · command/savecompat/missions PASS · uifuzz 30 예외 0 · detcheck GOLDEN match. ⑦ 교전 알림/미니맵은 미승인이라 미구현(제안: 승인 시 별도 카드).

리뷰 보강(Sonnet): BattleAutosave 판정을 SaveManager.BattleSaveDue(순수 함수, 동작 무변경)로 추출 + command 그룹에 자동 저장 테스트(트리거/2분 간격/재무장/실패 시 반복 없음) 및 LossCause 합계 단언 추가, MoveStats.cs summary 위치 수정. 빌드 OK · command PASS · uifuzz 30 예외 0 · detcheck GOLDEN match.
