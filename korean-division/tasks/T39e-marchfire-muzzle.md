# T39e 행진 사격(걸으며 쏘기) 총구 화염 앵커 재계산
- id: T39e | 역할: art | 등급: D中 H0 (R1 S Wheavy)
- 선행: T11 재렌더 완료 (kd-rerender 위 스택) | 후행: -
- **의존(필수)**: T11 81유닛 재렌더(브랜치 `kd-rerender`, 진행 중)가 끝나 wip-ready 되기 전에는 `game-tools/image/make_unit.py` · `rerender96.py` · `tools/parts` · `tools/bases` · `tune/*.json` · `units/*.json` · `anims/*.json` 을 **편집하지 않는다**(읽기·측정은 가능). 시작 전 kd-rerender 의 `RERENDER_PROGRESS.txt` 와 T11 카드 상태가 완료인지 확인; 아니면 `blocked(T11)` 로 두고 보고.
- 근거: ROADMAP_LONG ~302 (사용자 2026-10-21 0시: "돌격 사격 총구 화염 위치가 이상함"); 사용자 2026-10-21 "행진 사격 화염 다른 병종 적용 = 승인".
- 핫스팟/만질 파일: game-tools/image 의 행진 사격 애니(march_fire 계열: tools/bases/{infantry,infantry_heavy,infantry_scout,infantry_motions,assault,fengtian,wagon}.py, anims/*.json), export.py/crew.py 의 muzzle 앵커 출력(anchors.json muzzle), 엎드려쏴 때 쓴 총구 측정 방식 재사용. 게임 쪽은 읽기만: 해당 애니가 앵커를 올바른 프레임 · 방향에서 읽는지 확인(코드 수정이 필요하면 고치지 말고 결과란 + DECISIONS_PENDING 한 줄).
- 브랜치: `t39e-marchfire-muzzle` (base `kd-rerender`) | 상태: queued (blocked: T11) | 담당: -
## 확정 결정
- 위 근거 두 문장(사용자). 해결안 = 앵커를 방향 · 프레임별로 재계산 후 재렌더 · import.
## 범위 밖
새 애니 · 화염 스프라이트 변경 · 사격 로직 · 엎드려쏴(T39j)
## 수용 기준
- 8방향 x 행진 사격 프레임 전부에서 muzzle 앵커 = 소총 총구 픽셀(오차 <=1px @96); 병종별(보병 · 중화기 · 정찰 · 돌격 · 봉천 · 해당 시 기병/마차) 측정 표를 결과에 첨부.
- 접촉 시트 `docs/art_review/marchfire_muzzle.png` (방향별 화염 겹침, 전/후 비교).
- 게임 창에서 한국군 보병 돌격 사격 1장면 캡처 확인.
## 미리보기 · 공통 규칙
게임 창은 `tools/agent/run_game.ps1` (75a1712) 로만 실행. 접촉 시트를 항상 만든다(사용자 검수); 시트 승인 전 import/병합 금지. sim · Snapshot/FV 변경 없음. 무거운 렌더 동시 최대 2건, 유닛당 시간을 결과에 기록.
## 가정한 답
앵커만 수정(스프라이트 불변)이면 재렌더 불필요 -> 앵커 JSON 재생성 + import 만 수행.
## 결과
-
