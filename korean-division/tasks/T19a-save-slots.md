# T19a 저장 정리 · 자동 저장/재시도 · 슬롯 9개 (T19 분할 1/4)
- id: T19a | 역할: sim | 등급: D上 H2 (선택 메모: R3 S<=100k Wlight)
- 선행: T09 | 후행: T19b
- 핫스팟/만질 파일: Core/Campaign/CampaignState.cs(스키마·저장), UI/CampaignMenu.cs(슬롯 UI), StringsCampaign.cs, Tests/MissionSelfTest.cs(savecompat)
- 브랜치: `t19a-save-slots` (**t09-camp-slice 위 스택**, 작업 폴더 kd-develop/kd-t19a) | 상태: queued | 담당: sim(Opus)
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
