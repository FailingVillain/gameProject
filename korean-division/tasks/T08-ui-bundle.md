# T08 조작 UI 묶음 + UI 종이 질감 개편
- id: T08 | 역할: feature | 등급: D中 H1 (선택 메모: R1 SM Wlight)
- 선행: T01 (gemini-import 병합 뒤) | 후행: T17
- 핫스팟/만질 파일: UI 코드, 단축키
- 브랜치: t08-ui-bundle (gemini-import 위 스택) | 상태: wip-ready | 담당 에이전트: feature(sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 연대 선택 키는 플레이어 연대 편제 설계 포함 (HANDOFF 2026-10-22)
- 전투 중 숫자=연대, Alt+숫자=대대 (규칙 결정 9 안 1, 확인 필요)
- 종이 질감 개편 우선, UI 묶음 뒤 (확인 필요)
## 범위 밖
저장 연대 배속 (D09 보류)
## 수용 기준
UI 확인 시트 사용자 검수
## 측정
스크린샷 시트
## 결과 (에이전트가 끝에 3~5줄만)
- 연대=숫자/대대=Alt+숫자/중대 편성=Ctrl+Alt+숫자는 T01 가 이미 구현 -> CONTROLS.md · Bindings.cs 만 동기화. 명령 카드 넘침 = 둘째 쪽 이미 있음.
- 종이 질감: src/UI/Paper.cs (코드 생성 알갱이 타일, 렌더 전용) + Palette 따뜻한 갈색 톤 + Ui.Panel/Button/TopBar/Bottom 에 적용. 스크린샷 docs/art_review/t08_battle_hud.png (사용자 검수 대기).
- 검증: build 0 오류, selftest uiicons PASS, uifuzz 30 크래시 0. 결정성 무관(렌더 전용).
- 제안(미구현): 연대 편입(battalionRegiment)은 화면 상태라 저장/불러오기 시 사라짐 -> 저장하려면 sim 상태+FormatVersion 필요(D 결정). 캠페인 홈/라이트 종이(밝은 바탕+어두운 글씨)는 별도.
