# T39h 보병 모션 2차 (기어가기 · 부상자 끌기 · 돌격 수류탄 투척 · 참호 사격)
- id: T39h | 역할: art | 등급: D中 H0 (S L -> 분할 권장: h1 기어가기+참호 사격, h2 부상자 끌기+수류탄) Wheavy
- 선행: T11 재렌더 완료 | 후행: 게임 쪽 연결(별도 feature 카드, 지휘자 발주)
- **의존(필수)**: T11 재렌더(브랜치 `kd-rerender`, 진행 중)가 끝나기 전에는 `make_unit.py` · `rerender96.py` · `tools/parts` · `tools/bases` · `tune/*.json` · `units/*.json` · `anims/*.json` 편집 금지. 시작 전 T11 상태 확인; 아니면 `blocked(T11)`.
- 근거: ROADMAP_LONG ~237 "보병 모션 추가 계획 (2026-10-19 확정): 1차(대기 4 · 축하 2) 완료 후 2차 = 기어가기 · 부상자 끌기 · 수류탄 투척(돌격) · 참호 사격". ROADMAP 150: "모션 1차 승인" (2026-10-19).
- 핫스팟/만질 파일: game-tools/image/tools/bases/infantry_motions.py, anims/infantry-motions.json, export.py 애니 목록. 게임 쪽은 **수정하지 않음**(애니 등록 + 시트까지). T39j(엎드려쏴 재확인)와 포즈 코드가 겹치므로 **직렬**(동시 금지).
- 브랜치: `t39h-motion-r2` (base `kd-rerender`) | 상태: queued (blocked: T11) | 담당: -
## 확정 결정
- 2차 4종 목록(사용자 2026-10-19). 그 외 동작 추가 금지.
## 범위 밖
sim · 사상자 로직 · 게임 쪽 상태기계 연결 · 의무병 · 부상자 전용 병종
## 수용 기준
- 4종 각 8방향 x 프레임(1차 규격), 접촉 시트 `docs/art_review/motion_r2_<이름>.png` 4장(애니별 프레임 스트립), 관통 0, 96/48 LOD 둘 다.
- 기존 1차 애니 불변(회귀 비교 시트 1장). 유닛 1종 기준 렌더 시간 기록.
## 미리보기 · 공통 규칙
게임 창은 `tools/agent/run_game.ps1` (75a1712) 로만. 접촉 시트 항상 작성, 승인 전 import/병합 금지. sim · FV 변경 없음. 무거운 렌더 동시 최대 2건.
## 가정한 답
infantry-regular 만 1차 제작, 세력 변형은 재질 교체로 자동 적용; 부상자 끌기 = 끄는 쪽 애니만(부상자는 기존 prone 스프라이트).
## 결과
-
