# T38d 종이 UI 2단계: 밝은 종이+어두운 글자(중앙 팔레트) · 사령관 책상 · 세부 · 소해상도 감사
- id: T38d | 버전: 0.2 (UI) | 역할: feature | 등급: D中 H1 (R1 M<=150k Wlight) - 크면 d1(팔레트)/d2(책상·감사)로 분할
- 선행(blockedBy): T38c T38e (HUD 변경 끝난 뒤 한 번에) | 후행: T38g
- 직렬: Hud.cs 는 T38e 병합 뒤에 시작 (T38e 와 동시 수정 금지)
- 핫스팟: UI/Palette.cs · Paper.cs · Ui.cs(Text 색 호출 334곳: Palette.Text/TextDim/Accent) · Hud.cs · MainMenu · CampaignMenu · CodexMenu · BattlePanel/MissionMenu(전과 보고) · tools/ui_mock. 시뮬 무변경
- 브랜치: ui/t38d-paper2 (base = 마지막 UI 카드 브랜치; 캠페인 홈 확인은 t19c 병합 뒤 재적용) | 상태: queued
## 이미 있음
T08: Paper.cs 알갱이 + 따뜻한 갈색 톤 팔레트. 보류 = '밝은 종이 + 어두운 글자' (글자 색이 여러 곳에서 직접 지정돼서). 시안 docs/art_review/ui_paper_mock.png (33eed0f) 승인.
## 확정 결정
- 종이 질감 방향 확정 (사용자 2026-10-21): "현재는 너무 웹게임스러운 UI 형식", "종이 질감을 내는 걸 선호"; 사령관 책상 시안 승인 (2026-10-21): "사령관 책상은 나쁘지 않다. 일단은 이정도로 하고, 추후 지도 이미지의 개선 및 세부 사항 수정을 들어가자"
- 실제 구현은 10-23 주간 한도 초기화 뒤 (ROADMAP_LONG 2026-10-21). 작전 지도 이미지 개선 범위는 확인 필요 Q4.
- 적용 범위 (사용자 결정 2026-10-22): (a) 구현된 캠페인 홈 (t19c-home-tabs · t38f-home-readonly-tabs 화면 3개, 탭 15개) + (b) 전투 HUD/전투 인터페이스. 두 곳 모두 종이 질감 스킨 적용.
- 시안 승인 범위 (사용자 2026-10-22): 사령관 책상 시안(docs/art_review/ui_paper_mock.png, 설계 §단계5 = docs/CAMPAIGN_HOME_DESIGN.md)은 방향만 승인. 수정 사항 다수 — 사용자가 시트 검수 후 수집, 이후 개선은 ROADMAP_LONG '## 장기 — UI' 항목 '[추후] UI 종이 질감: 사령관 책상 시안 개선'으로 추적.## 범위 밖
캠페인 홈 새 탭(T38f) · 전투 월드 렌더(지형/유닛) · 새 기능 · 사령관 책상 시안 세부 개선(추후 로드맵 항목).
## 수용 기준
1) 글자/패널 색이 Palette 의 의미 토큰만 거침(직접 new Color/Palette.Text 우회 grep 0, 예외 목록 문서화). 2) 밝은 종이+어두운 글자 전환 후 대비비 >= 4.5:1(자동 계산 테스트). 3) 사기/피로 줄 간격 · 보충 버튼 잘림 수정. 4) MinScreen(1024x640) 포함 3해상도에서 메인/설정/도감/미션/캠페인 홈/전과 보고/HUD 가 잘림 없음(스크린샷 시트로 확인). 5) uifuzz 30 예외 0 · detcheck 불변.
## 측정
스크린샷 시트(화면 x 3해상도) 사용자 검수 - 시안 -> 확인 -> 확정. tools/agent/run_game.ps1 (보조 모니터 규칙).
## 결과
-
