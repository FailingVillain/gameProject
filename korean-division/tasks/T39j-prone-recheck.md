# T39j 엎드려쏴(prone) 재확인
- id: T39j | 역할: art | 등급: D中 H0 (R1 S Wlight)
- 선행: T11 재렌더 완료 (슬림화 반영 뒤) | 후행: -
- **의존(필수)**: T11 재렌더(브랜치 `kd-rerender`, 진행 중)가 끝나기 전에는 `make_unit.py` · `rerender96.py` · `tools/parts` · `tools/bases` · `tune/*.json` · `anims/*.json` 편집 금지. 시작 전 T11 상태 확인; 아니면 `blocked(T11)`. T39h 와 같은 파일을 만질 수 있어 **직렬**.
- 근거: ROADMAP_LONG 47 (사용자 "엎드려쏴 자세에 문제가 너무 많다. 총을 옆으로 쏘는 건 뭔 이슈고, 개머리판은 여전히 통과하는 부분이 있다") · 168 (재렌더 결과로 다시 확인) · GRAPHICS_ROADMAP "엎드려쏴 재설계 08 확정"; 진단 docs/PRONE_SPRITE_REVIEW.md (총구 방향 불일치 · 개머리판 관통 · 칸 가장자리 여백 <=1px).
- 핫스팟/만질 파일: 재렌더 산출물 aim_prone / fire_prone / go_prone (읽기·측정 먼저); 결함이 확인된 유닛 군에 한해 poses.json prone_weapon, tools/bases/* 수정.
- 브랜치: `t39j-prone-recheck` (base `kd-rerender`) | 상태: queued (blocked: T11) | 담당: -
## 확정 결정
- 재설계 08 (2026-10-21 GRAPHICS_ROADMAP). 이 카드는 **확인 우선**: 결함 없으면 수정 없이 종료(closed).
## 범위 밖
sim(엎드린 병사 대열 끌림은 별도 항목) · 새 자세
## 수용 기준
- 측정표: 17+종 x 8방향 총구 방향 vs 몸 축 일치, 개머리판-몸/지면 관통 픽셀 0(collide 도구), 칸 가장자리 여백 >=2px @32/64.
- 접촉 시트 `docs/art_review/prone_recheck.png` (합격 표시 또는 전/후).
- 결함 시 수정 + 재렌더 + 같은 측정 재통과.
## 미리보기 · 공통 규칙
게임 창은 `tools/agent/run_game.ps1` (75a1712) 로만. 접촉 시트 항상 작성, 승인 전 import/병합 금지. sim · FV 변경 없음. 무거운 렌더 동시 최대 2건.
## 가정한 답
T11 산출물의 prone 이 재설계 08 포즈를 이미 쓴다고 가정하고 측정으로 검증.
## 결과
-
