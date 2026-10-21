# T19c 캠페인 홈 3화면 · MVP 5탭 · 연대 배속 UI (T19 분할 3/4)
- id: T19c | 역할: feature | 등급: D上 H2 (선택 메모: R2 S<=130k Wlight)
- 선행: T19b | 후행: T19d
- 핫스팟/만질 파일: UI/CampaignMenu.cs(Tab 열거형·화면 분할), StringsCampaign.cs, Roster/AssignRecruits(연대 배속), Core/Campaign 읽기 위주
- 브랜치: `t19c-home-tabs` (**t19b-ammo-shells 위 스택**) | 상태: queued | 담당: feature(Sonnet) — 막히면 sim
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- Q1 3개 화면 중앙 9 + 좌 3 + 우 3 = 15탭 (2026-10-22). 배치 제안 F1: 중앙 1·2·3·4·5·6·10·13·15 / 좌 11·12·14 / 우 7·8·9, 무응답 시 제안대로.
- MVP 5탭 = 1 사단 상태 요약(규칙 15) · 2 신병/부대 · 4 병참 · 10 작전 지도(목록) · 13 본국(이벤트). 모두 중앙. 좌·우 화면은 틀만, 후속 탭 추가 때 채움 (설계 §11-1·§14-1).
- Q9 연대 배속 UI MVP 포함 (연대 ≤9). 규칙 9(숫자=연대, Alt+숫자=대대) 키 구현은 별 작업.
- Q10 압록강 시작 · 1-1/1-2 재배치 반영 (T09 에서 지도 탭 반영 확인 후 잔여만).
- 규칙 1~15 대응표 = 설계 §12. 탭 추가는 `Tab` 열거형 + StringsCampaign 만 건드리게 분리 (§14).
- 설계 정본: `docs/CAMPAIGN_HOME_DESIGN.md` v0.4 (브랜치 spec/t23-campaign-home 42fa456 에서 읽기: `git show spec/t23-campaign-home:korean-division/docs/CAMPAIGN_HOME_DESIGN.md`). 답변 정본 = ops/DECISIONS.md C2 (Q1~Q10) · F (무응답 기본값).
## 범위 밖
3 편제·5 장비·6 훈련·7 외교·9 진급·11 정보·12 기록·14 일정·15 연구 탭 본문(후속, 질문 2), 종이 질감 스킨(T08), 시간·계절(규칙 7)
## 수용 기준 (측정 가능)
1) 세 화면 전환 + MVP 5탭 전부 열림, 좌·우 빈 화면 예외 0 2) 병참 탭에 구경별 재고·포탄·보급 미리보기 3) 연대 1~9 배속 저장 왕복 4) uifuzz 예외 0
## 측정
build 오류 0 · missions · savecompat · uifuzz · detcheck GOLDEN 불변 · 수동 UI 1사이클
## 결과 (에이전트가 끝에 3~5줄만 기록)
