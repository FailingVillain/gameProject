# T39f 한국 x0.78 / 봉천 x1.22 색 적용 확인·완결
- id: T39f | 역할: art | 등급: D中 H0 (R1 S Wlight)
- 선행: T11 재렌더 완료 | 후행: -
- **의존(필수)**: T11 81유닛 재렌더(브랜치 `kd-rerender`, 진행 중)가 끝나 wip-ready 되기 전에는 `make_unit.py` · `rerender96.py` · `tools/parts` · `tools/bases` · `tune/*.json` · `units/*.json` · `anims/*.json` 을 **편집하지 않는다**(읽기·측정은 가능). 시작 전 T11 상태 확인; 아니면 `blocked(T11)`.
- 근거: T11 카드 "한국 회색 x0.78 / 봉천 x1.22 (미리보기안 확정)"; docs/art_review/kr_ft_colour.png (+ kr_ft_colour_final.png). ART_SPEC.md 는 이미 "418c1dc 에서 materials.json · fengtian.py 반영"이라 기록 — 즉 이 카드는 **새 색을 정하지 않고** 재렌더 결과물 전체에 빠짐없이 적용됐는지 검증하고 누락만 고친다.
- 핫스팟/만질 파일: game-tools/image/tune/materials.json(fgrey_dark 4단 · trouser · wrap), tools/parts/fengtian.py, 같은 재질을 참조하는 장교 · 기병 · 중화기 · 포병 등(누락 시 재질 이름만 연결), 누락 유닛 재렌더.
- 브랜치: `t39f-kr-ft-colour` (base `kd-rerender`) | 상태: queued (blocked: T11) | 담당: -
## 확정 결정
- 한국 x0.78 · 봉천 x1.22 (사용자 미리보기안 확정, 2026-10-21 ART_SPEC 결정란).
## 범위 밖
새 색 제안 · 일본/러시아 색 · 모자/휘장 변경
## 수용 기준
- 한국 · 봉천 소속 전 유닛(보병 · 장교 · 기수 · 저격 · 기병 · 중화기 · 포병)의 대표 색 샘플(RGB 중간값)이 문서 값 (39,43,41)->(76,83,79) / (41,54,73)->(107,124,151) +-3 안; 표를 결과에 첨부.
- 접촉 시트 `docs/art_review/kr_ft_colour_applied.png` (kr_ft_colour_final.png 와 나란히).
## 미리보기 · 공통 규칙
게임 창은 `tools/agent/run_game.ps1` (75a1712) 로만 실행. 접촉 시트 항상 작성, 승인 전 import/병합 금지. sim · FV 변경 없음. 무거운 렌더 동시 최대 2건.
## 가정한 답
미리보기안 = 현 materials.json 값(변경 없음). 값 수정이 필요해 보이면 DECISIONS_PENDING.
## 결과
-
