# T47 캠페인 작전 지도 탭 확대
- T47 | feature | D中 · H1 (UI 만; sim · FormatVersion 불변) | 선행: T38i (3e41444) | 후행: -
- 만진 파일: src/UI/CampaignMapView.cs(신규) · MapZoomView.cs(신규) · CampaignMenu.cs(DrawTabMap 노선도 부분만) · StringsCampaign.cs · CampaignFuzzHooks.cs · Tests/UiFuzz.cs · Tests/MapZoomSelfTest.cs(신규) · SaveCompatSelfTest.cs(호출 1줄)
- 브랜치 t47-campaign-map-zoom / 작업 폴더 kd-develop/kd-t47 | 상태: wip-ready (NO-MERGE 스택, T38i 위) | 담당: feature(Sonnet)
## 확정 결정
- 사용자 결정 AZ (2026-10-22): 지도 탭 = 책상 지도 + 확대. 확대 상태는 세션 한정, 저장에 아무것도 안 남김.
## 범위 밖
- 지형 · 실제 지도 그림, 새 작전 · 새 메커니즘, 저장 · 시뮬 변경.
## 수용 기준
배율 [1,8] 클램프, 휠은 커서 기준, [-][+][맞춤], 끌기 이동(가장자리 클램프), 키 = - 0, 배율 표시, 1024x640~1920x1080 레이아웃 유지, 어느 배율에서나 거점 클릭으로 작전 고르기(끌기 5px 미만이면 클릭), 라벨 겹침 없음(작은 배율에서 보조 라벨 숨김), 색은 Palette 만, CampaignState 불변.
## 측정
savecompat(MapZoomSelfTest 6건) · missions · uiicons · --uifuzz 30 --fuzz-campaign(1280x720 / 1024x640, 예외 0) · detcheck GOLDEN match.
## 결과
- 노선도(8거점)를 확대 뷰포트로 교체(높이 150~280 가변, 아래 작전 목록 유지). 거점 클릭 = 목록의 [출격 준비] 와 같은 조건으로 PreOp 진입. 확대 시 거점 반지름 · 선 굵기 소폭 증가, 배율 2.5 이상에서 작전 이름 병기.
- 퍼즈: 지도 탭에서 휠 · 끌기 · 키 주입 + 배율/중심 범위 · 캠페인 상태 JSON 불변 대조 (1280x720: 휠 18 · 끌기 15 · 키 10; 1024x640: 13 · 8 · 9; 예외 0).
- 시각 확인 스크린샷: docs/art_review/t47_map_overview_1280x720.png · t47_map_zoomed_1280x720.png (KD_CAMPAIGN_MAPZOOM=3:1 확인용 환경변수). 사용자 눈 검수 필요(에이전트는 이미지를 열지 않음).
