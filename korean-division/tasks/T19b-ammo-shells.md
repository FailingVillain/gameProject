# T19b 구경별 탄약 5종 · 포탄 규칙 · 노획 (T19 분할 2/4)
- id: T19b | 역할: sim | 등급: D上 H2 (선택 메모: R3 S<=120k Wlight)
- 선행: T19a | 후행: T19c
- 핫스팟/만질 파일: Core/Campaign/CampaignEconomy.cs · CampaignState.cs(Stock 스키마 v3) · CampaignCarry.cs(ammoScale 적재), data/campaign 수치(T29 가 JSON화했으면 거기), Tests, StringsCampaign
- 브랜치: `t19b-ammo-shells` (**t19a-save-slots 위 스택**) | 상태: queued | 담당: sim(Opus)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- Q5 구경별 탄약 5종을 MVP 부터 반영 (구경: 노식 모신·맥심 / 일식 아리사카·3년식 / 독식 마우저·한양88 / 불식 르벨·쇼샤 / 구식 일본 무라타). 구경 구분은 캠페인 단위만, 전투 안은 구분 안 함 (2026-10-22)
- Q6 포탄 = 탄약과 같은 방식: 작전 시작 때 종류별(보병포·야전포·곡사포) 재고에서 휴대량을 채우고(모자라면 기본 휴대량 50%), 전투 뒤 **실제 발사 수**를 재고에서 뺌, 노획 포탄은 적 손실 비례 가산, 별도 규칙 없음 (사용자 2026-10-22 "추천안대로")
- 규칙 1~3: 예산 없음·보급품 한계 / 노획 / 사상자 25% 부상 복귀·치료 0.5 (ROADMAP_LONG 승인 2026-10-21, 이미 WIP 구현 → 검증만)
- Q7 이벤트 수치 · Q8 cmd_favor 는 T01 복원값 그대로.
- 설계 정본: `docs/CAMPAIGN_HOME_DESIGN.md` v0.4 (브랜치 spec/t23-campaign-home 42fa456 에서 읽기: `git show spec/t23-campaign-home:korean-division/docs/CAMPAIGN_HOME_DESIGN.md`). 답변 정본 = ops/DECISIONS.md C2 (Q1~Q10) · F (무응답 기본값).
## 범위 밖
훈련 효과(Q4 MVP 제외), 장비 개량, 탭 UI(T19c), 신규 수치 발명
## 수용 기준 (측정 가능)
1) 구경 5종 재고가 저장 v3 로 왕복, v2 저장 자동 이행(savecompat) 2) 출격 휴대 = 구경별 재고에서 채움, 부족 시 50% 3) 포탄 3종 동일 경로, 발사 수 차감 · 노획 가산 단위 테스트 4) 대포 없는 미션은 무영향 5) 전투 시뮬 결정성 불변
## 측정
build 오류 0 · missions · savecompat · `--snaptest` · `--detcheck`(GOLDEN 불변) · 설계 §13-1(e) 확인
## 병합 순서 주의 (T29)
T29 가 정산을 "첫 승리만 · 경제 수치 JSON" 으로 바꾸고 T09 도 정산을 바꿈. **이 카드 착수 전에 T29 와 T09 의 CampaignEconomy 정산 충돌을 정리**(제안: T29 를 t09-camp-slice 위로 재스택 후 이 카드가 그 위). 질문 1.
## 결과 (에이전트가 끝에 3~5줄만 기록)
