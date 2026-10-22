# T38f 캠페인 홈 후속 탭 -> 읽기 전용 실화면 (순서 제안 + 1차 3탭)
- id: T38f | 역할: spec -> feature | 등급: D中 H1 (R2 M<=150k Wlight)
- 선행(blockedBy): T19c wip-ready(t19c-home-tabs) | 후행: -
- 핫스팟: UI/CampaignHome.cs(Tab 열거형·배치) · StringsCampaignHome.cs · Core/Campaign 읽기만. 저장 형식 무변경 (읽기 전용 뷰)
- 브랜치: t38f-home-readonly-tabs (base = t19d-doctrines 스택, t19c·참모진 탭 포함; 원안 ui/t38f-home-tabs / base t19c; T38d 의 종이 UI 와 CampaignMenu 충돌 -> t19c 병합 정리 뒤 재적용) | 상태: wip-ready
## 이미 있음 (t19c)
3화면 15탭 틀, MVP 5탭(1 요약 · 2 신병/부대 · 4 병참 · 10 작전 지도 목록 · 13 본국) 구현. 나머지 10탭 = 자리표시.
## 확정 결정
- Q1 3개 화면 중앙 9 + 좌 3 + 우 3 = 15탭 (2026-10-22). 3 편제·5 장비·6 훈련·7 외교·9 진급·11 정보·12 기록·14 일정·15 연구 탭 본문 = 후속 (T19c 카드). 설계 정본 docs/CAMPAIGN_HOME_DESIGN.md.
## 범위 밖
쓰기 동작(편제 변경·훈련·연구 실행) · 시간/계절 · 새 규칙 · 종이 스킨.
## 수용 기준
1) 1차: 이미 데이터가 있는 읽기 전용 탭만(후보 12 기록 · 5 장비 재고 · 3 편제 트리). 순서는 확인 필요 Q6. 2) 각 탭 데이터 없음 상태 문구, 예외 0. 3) 작은 [-][+] 버튼이 있다면 T38g 이후 퍼즈 검증. 4) savecompat PASS(스키마 불변) · uifuzz 30 예외 0.
## 측정
build · missions · savecompat · CampaignHomeSelfTest · uifuzz · 탭별 시트.
## 결과
- 2026-10-22 Sonnet: 읽기 전용 3탭 구현 (카드 제안 순서 12 기록 -> 5 장비 -> 3 편제). 표시 자료 = CampaignHomeViews (순수 함수, CampaignState 읽기만), 그리기 = CampaignHomeReadonly.cs, 문구 = StringsCampaignHomeRo.cs. 새 버튼 없음 (탭 버튼은 기존), 저장 형식 · 스키마 무변경.
- 열린 질문 Q6 = 카드 권장대로 12 -> 5 -> 3 (이미 데이터가 있는 순). 12 기록: 작전 일지(최근 8) · 누적 사살/손실 · 이월 사병 사살 상위 3 · 마지막 전과 보고 (전사자 명부 · 훈장은 자료가 없어 '후속' 안내). 5 장비: 구경별 소총/탄약/편성 중대 수 · 포탄 3종 · 중장비 · 군마 (개량 · 도입은 후속). 3 편제: 연대 > 대대 > 중대 트리 (범위 밖 연대 = 직할, 줄 넘치면 '외 N줄'; 편제 틀 편집은 후속).
- 검증: build 0, savecompat (CampaignHomeViewsSelfTest 7건 포함) / missions / doctrines PASS, detcheck GOLDEN match 8aa6448b, uifuzz 30 x3 (KD_CAMPAIGN_VIEW=left:12, center:5, center:3) 예외 0 · 크래시 0. 스크린샷 docs/art_review/t38f_home_{records,equipment,organization}.png (--menu-page campaign --menu-shot, 임시 저장 폴더 KD_CAMPAIGN_DIR).
- 비고: 무거운 작업 게이트 60분 대기 후 timeout (렌더 과밀) -> 빌드는 그 뒤에 진행. 4 정보 / 14 일정 / 15 연구 등 나머지 탭은 자리표시 그대로 (자료 없음). run_game.ps1 은 쓰지 않음 (--menu-shot 으로 충분).
