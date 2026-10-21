# T19a 저장 정리 · 자동 저장/재시도 · 슬롯 9개 (T19 분할 1/4)
- id: T19a | 역할: sim | 등급: D上 H2 (선택 메모: R3 S<=100k Wlight)
- 선행: T09 | 후행: T19b
- 핫스팟/만질 파일: Core/Campaign/CampaignState.cs(스키마·저장), UI/CampaignMenu.cs(슬롯 UI), StringsCampaign.cs, Tests/MissionSelfTest.cs(savecompat)
- 브랜치: `t19a-save-slots` (**t09-camp-slice 위 스택**, 작업 폴더 kd-develop/kd-t19a) | 상태: wip-ready | 담당: sim(Opus)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 캠페인 저장 슬롯 9개 + 자동 저장(재도전 가능); 자동 저장 = 작전 시작 직전 · 전과 정산 직후, 실패 시 재시도(실패 알림 + 다시 시도); 재도전 = 작전 시작 직전 자동 저장으로 복귀 (사용자 2026-10-22, 설계 §3 · §0, C2 Q2/Q3)
- 시점·재시도 방식은 Claude 제안(F2), 무응답 시 그대로.
- 설계 정본: `docs/CAMPAIGN_HOME_DESIGN.md` v0.4 (브랜치 spec/t23-campaign-home 42fa456 에서 읽기: `git show spec/t23-campaign-home:korean-division/docs/CAMPAIGN_HOME_DESIGN.md`). 답변 정본 = ops/DECISIONS.md C2 (Q1~Q10) · F (무응답 기본값).
## 범위 밖
탭 확장, 구경별 탄약(T19b), 소형 패배 분기 기록 구조(F4: 슬롯+자동 저장으로 대체, 별도 구조 없음)
## 수용 기준 (측정 가능)
1) T09 가 이미 넣은 자동 저장/재시도 범위를 먼저 확인(grep CampaignState)하고 **빠진 부분만** 구현 2) 슬롯 9칸 저장/불러오기/덮어쓰기, 옛 슬롯 1개 저장 호환(savecompat 통과) 3) 저장 실패 주입 시 알림+재시도, 반쪽 파일 0 4) 작전 직전 자동 저장으로 재도전 복귀 1회 수동 확인
## 측정
build 오류 0 · missions · savecompat · `--snaptest` · `--detcheck`(GOLDEN 불변, 기준 docs/BASELINE_CURRENT.md)
## 병합 순서 주의
T29(첫 승리만 정산·경제 JSON, gemini-import 위)가 CampaignEconomy.cs/CampaignState.cs 를 건드려 T09 정산 변경(+37/+34줄)과 충돌 소지. 이 카드는 정산 로직을 건드리지 않음. T29 정리는 DECISIONS_PENDING 질문 1.
## 결과 (에이전트가 끝에 3~5줄만 기록)
- T09 확인: 자동 저장 · 재시도 · 슬롯 없음 (단일 campaign.json 원자 저장만, .tmp 는 실패 시 남음) → 전부 새로 구현. 새 `Core/Campaign/CampaignSaves.cs`: slot1~9.json · auto_preop.json(작전 직전 = 재도전) · auto_settle.json(정산 직후), WriteAtomic(실패 시 .tmp 삭제), 실패 주입 `KD_SAVE_FAIL=N`/InjectFailures. 정산 로직 불변.
- UI: 홈 상단 [저장 / 불러오기] 창(9칸 저장·덮어쓰기 확인·불러오기, 자동 2칸 불러오기), 메뉴 저장 실패 → 알림 창 [다시 시도] (출격은 작전 직전 자동 저장 성공 뒤에만), 결과 화면 정산 저장 실패 → [캠페인 저장 실패 — 다시 시도]. 63080df 에서 최소 이식: 읽기 실패 시 메뉴 저장 차단 · 출격 잠금 · 초기화 전 .bak (BackupBroken).
- 검증: build 오류 0 · savecompat PASS(새 CampaignSlotsSelfTest 5 check) · missions PASS · detcheck GOLDEN match · snaptest(quick) OK · uifuzz 30 예외 0. 수용 4) 수동 GUI 확인은 미실시(자동 시험으로 대체) — 사용자 확인 필요.
- 리뷰 후속(1커밋): ① 슬롯 저장은 기존 칸이 깨짐/새 버전(Error)이면 SaveSlot 이 거부(원본 보존, 버튼 잠금 + 안내 문구) ② 결과 화면은 campaignUnsaved 동안 Enter/[재도전]/[메뉴] 첫 입력은 경고만, 두 번째 입력에서 떠남 ③ 저장 실패 창이 만들어진 프레임의 클릭은 무시(saveFresh) ④ BackupBroken 은 기존 .bak 를 덮어쓰지 않고 .bak.1, .bak.2… 시험 2개 추가(CampaignSlotsSelfTest). savecompat · missions PASS, detcheck GOLDEN match, uifuzz 30 예외 0.
