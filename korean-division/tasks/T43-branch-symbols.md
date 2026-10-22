# T43 축소 화면 병종 기호 정비 (세력 휘장 + 병종 이미지)
- id: T43 | 역할: feature | 등급: D中 H1 (R1 S<=80k Wlight) | 버전 0.2
- 선행: t27-cmd-ui2 (ColorBlind 표식과 같은 파일 Render/UnitSymbols.cs 를 만짐) | 후행: -
- 파일: Render/UnitSymbols.cs · themes/kd1920 icons(branch-*) · Tests(uiicons 그룹) · docs 시트. 시뮬/저장 무변경(FV 불변).
- 브랜치 `t43-branch-symbols` (t27-cmd-ui2 위) | 상태: queued
## 먼저 할 일 (이미 구현 확인)
rankui(2026-10-19)로 휘장 + 아래 병종 이미지는 **이미 구현됨** (IconName 9종+officer, 5크기 12~32). 이 카드는 **빈틈만**: 아이콘 파일은 있으나 Branch 열거에 없는 howitzer · sniper (현재 포병/보병에 합쳐짐), 혼성 중대의 다수결 한 종류만 표시, 크기별 가독성.
## 확정 결정
- 축소 중대 기호 = 세력 휘장 + 그 아래 병종 이미지, NATO 기호는 옛 테마 대체만 (사용자 2026-10-19 rankui). 중대 지정 안 된 유닛은 점 (사용자 2026-10-21).
## 범위 밖
열차/전투공병 매핑 (다른 스택, 병합 후 후속) · 새 아이콘 그리기 · 혼성 표시 신규 UI(Q2) · 미니맵
## 수용 기준
1) 전수 테스트: 모든 UnitDef -> Branch -> kd1920 의 icon.branch.* 5크기 존재, 누락 0 (누락은 목록 보고) 2) howitzer/sniper 판정은 UnitDef·무기 데이터에서 (이름 하드코딩 금지) 3) 세력 5종 x 병종 x 줌 3단 접촉 시트 1장(사용자 검수) 4) ColorBlind 도형과 겹침 없음 5) uiicons PASS · uifuzz 30 크래시 0 · detcheck 불변
## 측정
KD_SELFTEST_ONLY=uiicons · --uifuzz 30 · 시트 (tools/contact_sheet.py)
## 질문 (DECISIONS_PENDING)
(1) 저격 판정 기준(무기 속성 vs 유닛 플래그)? (2) 혼성 중대(예: 보병+기관총)를 다수결 1종 vs 보조 소형 표시? (3) 열차·공병 아이콘은 T30/T32a 병합 뒤 후속 카드로 두는가?
## 결과
