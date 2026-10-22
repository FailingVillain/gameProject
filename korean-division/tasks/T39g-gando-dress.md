# T39g 간도 의용대(서간도 의용군) 복식
- id: T39g | 역할: art | 등급: D中 H0 (R1 S Wlight)
- 선행: T11 재렌더 완료 | 후행: -
- **의존(필수)**: T11 재렌더(브랜치 `kd-rerender`, 진행 중)가 끝나기 전에는 `make_unit.py` · `rerender96.py` · `tools/parts` · `tools/bases` · `tune/*.json` · `units/*.json` · `anims/*.json` 편집 금지(읽기·측정만). 시작 전 T11 상태 확인; 아니면 `blocked(T11)`.
- 근거: 사용자 2026-10-21 단기 목록 "간도 의용대 복식"(ROADMAP_LONG 305). HANDOFF units 항목에 간도 카키 복식 일부가 병합됨(시트 docs/art_review/units_fix_cossack_gando.png 사용자 확인 대기). ART_SPEC.md:96/107: 현재 카키 면옷(kd_gando_khaki) + 진회색 바지 + 각반 + 황갈 덮개 모자(gando_cap), **가슴 탄띠 미정**.
- 핫스팟/만질 파일: tools/parts/kd_new.py:19-21,45,54, units/infantry-gando.json, 탄띠 부품(봉천 X자 천 탄띠 부품 재사용 우선).
- 브랜치: `t39g-gando-dress` (base `kd-rerender`) | 상태: queued (blocked: T11) | 담당: -
## 확정 결정
- "간도 의용대 복식" 작업 항목(사용자 2026-10-21). 복식 세부는 ART_SPEC 의 기존 기록 범위 안에서만.
## 범위 밖
몽골/마적 구분 · 새 병종 · 휘장 신규 설계 · 다른 세력
## 수용 기준
- 8방향 확인 컷, 한국군(회색)과 한눈에 구분(색 대조 시트), 탄띠 1종, 관통(collide) 0.
- 접촉 시트 `docs/art_review/gando_dress.png` (한국군 · 봉천 · 마적과 나란히).
## 미리보기 · 공통 규칙
게임 창은 `tools/agent/run_game.ps1` (75a1712) 로만. 접촉 시트 항상 작성, 승인 전 import/병합 금지. sim · FV 변경 없음. 무거운 렌더 동시 최대 2건.
## 가정한 답
복식 = ART_SPEC 현재 기록 + 탄띠 1종만. 모자 · 색 변경 요청 없음.
## 결과
-
