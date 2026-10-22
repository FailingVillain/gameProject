# T38i 튜토리얼 연결 (캠페인 홈)
상태: wip-ready (브랜치 t38i-tutorial-link, 기반 t38h-fuzz-campaign f126551, NO-MERGE 스택)

## 한 일
- 지도 탭 제목줄에 [튜토리얼 0-1 연습] · [튜토리얼 0-2 연습] 버튼 (항상 가능). 기존 출격 경로 재사용: CampaignMenu.OpenPractice -> MainMenu -> MissionMenu(practice:true) 브리핑 -> 배치/바로 시작.
- 연습 = GameOptions.NoResultsFile=true. 정산은 Game.SettleMission(정적 추출)에서 한 곳: 리플레이/결과 없음이면 결과 json · CommitResult 모두 건너뜀. 출격 준비(BeginOperation, 보급 소모, 자동 저장) 도 안 거침.
- 시험(missions 묶음 TutorialChecks): 임시 KD_CAMPAIGN_DIR, 연습 정산 후 campaign 저장 바이트 · 파일 목록 동일 + 대조(일반 정산은 바뀜).

## 가정한 답
1. 위치 = 지도 탭(작전 지도), 별도 탭 아님. 2. 0-1 · 0-2 둘 다 연습 가능. 3. 연습은 결과 파일 · 리플레이 외 아무 기록 없음 (리플레이는 기존대로 저장). 4. 연습 중 캠페인 난이도 읽기(읽기 전용)는 유지. 5. 기존 지도 목록의 0-1/0-2 [출격 준비] (정식 캠페인 진행)는 그대로.
6. 색은 Palette 토큰과 기존 Button 헬퍼만 사용 (예외 없음).

## 검증
빌드 0 오류, missions/savecompat/uiicons PASS, --uifuzz 30 --fuzz-campaign 예외 0, detcheck GOLDEN match (시뮬 · FormatVersion 불변). 스크린샷 docs/art_review/t38i_tutorial_link.png (1280x720).
