# T43 축소 화면 병종 기호 정비 (세력 휘장 + 병종 이미지)
- id: T43 | 역할: feature | 등급: D中 H1 (R1 S<=80k Wlight) | 버전 0.2
- 선행: t27-cmd-ui2 (ColorBlind 표식과 같은 파일 Render/UnitSymbols.cs 를 만짐) | 후행: -
- 파일: Render/UnitSymbols.cs · themes/kd1920 icons(branch-*) · Tests(uiicons 그룹) · docs 시트. 시뮬/저장 무변경(FV 불변).
- 브랜치 `t43-branch-symbols` (t27-cmd-ui2 위) | 상태: wip-ready (kd/t43-branch-symbols)
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
- Branch 에 Howitzer(곡사 무기 + category gun) · Sniper(무기 PreferOfficer, 직사) 추가, 이름 하드코딩 없음. 기호 틀 NATO 폴백 · 툴팁 이름(곡사포|저격) 추가. 아이콘 파일/테마 키는 이미 있어 신규 그리기 없음.
- 가정한 답: Q1 = 무기 속성 (PreferOfficer), Q2 = 다수결 1종 유지(동수는 낮은 열거값), Q3 = 열차/공병 후속 카드.
- 검증: build 0 오류 · uiicons PASS (누락 0) · uifuzz 30 크래시 0 · detcheck GOLDEN 일치 8변형. 시트 docs/art_review/t43_branch_symbols_sheet.png.
- 미확인: 수용 4(ColorBlind 겹침) = t27-cmd-ui2 미병합이라 확인 불가.
- T43b (Haiku, 2026-10-22): `Of` 에 `d.Mounted ? Cavalry` 추가 (정찰 분기 뒤, 승마 정찰 cavalry_scout 은 정찰 유지). cavalry_default · cavalry_cossack · cavalry_mongol = Cavalry. 테스트 2개 (uiicons). detcheck GOLDEN 일치, build 0 오류, uiicons PASS. 시트 png 는 갱신 안 함 (아이콘 그림 자체는 불변, 판정만 바뀜).
- 노트 (병합 후 확인): 무기 `wireCutter` 보유 + 수류탄 보병 (infantry_pioneer, t32a-pioneer) 은 기존 규칙 (수류탄 보병 = Assault) 으로 Assault 가 된다. 이름 특례 없음 (자료 기준). 공병 아이콘이 필요하면 후속 카드 (질문 3).
