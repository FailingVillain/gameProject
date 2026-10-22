# T48d 관리 UI (참모진 탭 확장 · 진급 종착 · 연표/일지 줄)
- id: T48d | 역할: feature | 등급: D中 H2 (R1 Wlight) | 크기 M (<=130k)
- 선행(blockedBy): T48b T48c | 후행: T48e (선택)
- 파일/핫스팟: UI/CampaignHomeStaff.cs (탭 8, T19d 읽기 전용 -> 소탭 [연대장][대대장·참모][후보 풀][학파]) · UI/CampaignHomePromotion.cs (탭 9 '종착 배정 대기') · UI/CampaignHomeReadonly.cs (탭 12/14 `commander` 줄 문구) · 신규 UI/CampaignHomeCommanders.cs · UI/StringsCampaignCommanders.cs · assets/ui/commanders/ (자리표시자 초상 1장) · Tests (t38g UI harness). CampaignHome.cs 배선 1~2줄만.
- 브랜치 `t48d-commander-ui` (t48b2-assign 80c11fb 위 스택; 실제 기반) | 상태: wip-ready | 담당: feature(Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22: 관리 UI 는 캠페인 홈 안 (15 탭), 종이 질감 방향, '인사 카드' 스타일.
- 2026-10-22: 연대장 풀에서 기용/해임; 해임 불가 인물 있음; 해임 시 호감 소폭 하락 (확인 대화상자에 예고).
## 범위
- 와이어프레임 = docs/COMMANDERS_DESIGN.md 7장. 연대 1~9 줄 (연대장 카드 · 공석 [기용...] · 해임 불가 도장), 후보 풀 (필터 · 카드 그리드 · 상세: 약력 / 선호기피 / 비용 / 조건 + [기용: 연대 선택]), [해임] 확인 대화상자, 대대장 / 참모 슬롯 배정, 호감 막대 + 단계 문구, 초상 파일 없으면 자리표시자.
- hire/dismiss/배정 버튼은 **T48b 함수 호출만** (UI 에 규칙 없음). 작전 사이 외에는 비활성. `commanders.enabled=false` -> 기존 T19d 화면 + '규칙 꺼짐' 안내.
- 연표/일지 문구는 id 로 찾기 (저장 안 함, T42d 방식).
## 범위 밖
새 탭 번호 추가 · 서사 연출 · 전투 HUD 인사 카드 · 초상 일러스트 제작 (art)
## 수용 기준 (측정 가능)
1) KD_CAMPAIGN_VIEW=right:8 스크린샷으로 소탭 3종 확인 (카드 3열, 수십 명 스크롤, 해임 불가 도장)
2) 기용 -> 해임 -> 재기용이 UI 에서 연표 · 일지 · 호감 막대에 반영
3) 해임 불가 인물에는 해임 버튼 없음
4) 한글 문구 넘침 없음 (t46 카드 넘침 규칙)
5) `--uifuzz 30` 크래시 0 (캠페인 화면 포함)
6) `--detcheck` GOLDEN 불변
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
build · selftest · --uifuzz 30 · 스크린샷 harness (t38g)
## 결과 (에이전트가 끝에 3~5줄만 기록)
- 구현: UI/CommanderHomeModel.cs(표시 자료·버튼 상태 = T48b API 결과 그대로) · UI/CampaignHomeCommanders.cs(소탭 4종·3x3 카드판·후보 풀 3열 스크롤·확인 대화상자·종착 배정) · StringsCampaignCommanders.cs. 탭 9 종착 구역 · 탭 12 인사 일지 · 탭 14 연대장 줄 (id 조회).
- 시험: commanders 그룹에 CommanderUiSelfTest 35건 추가 (T48a 일정 탭 시험 1건은 commander 줄 표시로 갱신). commanders/savecompat/missions/uiicons PASS, --uifuzz 30 크래시 0, detcheck 8변형 GOLDEN 일치.
- 한계: `commanders.enabled` 스위치는 이 기반에 없음 -> 연대장 명단이 비면 '규칙 꺼짐'(기존 T19d 화면). --fuzz-campaign 훅(t38h)은 이 기반에 없어 확장 못 함 (후속). 초상은 assets/ui/commanders/<portrait> 파일이 있으면 쓰고 없으면 실루엣 자리표시자 (이미지 제작 안 함). [학파] 소탭 = 기존 T19d 화면 그대로.
