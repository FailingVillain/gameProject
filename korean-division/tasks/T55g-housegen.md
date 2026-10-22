# T55g 파라메트릭 집 생성기
- id: T55g | 역할: art(model) | 등급: D中 H0 (지붕 형태가 안 풀리면 上 1회)
- 선행: 재렌더 레인 여유 (실측 때만 heavy_gate -Render) | 후행: T55a T55b
- 만질 파일: game-tools/image/tools/buildings/housegen.py(새) house_specs/*.json(새, 스펙은 JSON 파일로 — 생성기 무수정으로 T55a/b 가 추가) bcommon.py(MATS 램프 추가만, append-only) make_building.py(registry/--set/--shard)
- 브랜치: gt-house-gen (gt-develop/gt-house-gen, base main 323c2da; 전부 game-tools + docs 라 kd 워크트리 불필요) | 상태: wip-ready
## 확정 결정
기본 42종은 파라미터 사전(손으로 정한 스펙). 기존 4종 코드 유지 (회귀 방지).
## 범위 밖
42종 전체 렌더 (T55a/b) · 게임 코드 · 러시아식
## 수용 기준
- 지붕 6형 (thatch_round gable_curved hip flat_earth shed gable_plain) 샘플 + 기존 4종 재현 비교 시트 docs/art_review/housegen_proto.png (사용자 검수).
- 3-LOD 렌더 시간 1종 실측을 PLAN 4.3 에 반영.
- palette 사상과 ruin=True 파생 동작.
## 결과
- 생성기 `game-tools/image/tools/buildings/housegen.py` + JSON 스키마 `house_specs/HOUSEGEN_SCHEMA.md` (팔레트 `_palettes.json`, 런타임 틴트 `_tints.json`, 샘플 `proto.json`). 지붕 6형 · 발자국 3x3/4x3/4x4/5x3 · 벽/문창/굴뚝/툇마루/부속 · 팔레트 변형 · ruin 파생 구현. `make_building.py` 에 `--set` `--shard` `gen` 추가, `house_sheet.py` 검수 시트. 기존 4종 코드 무변경 (bcommon 에 재질 6개 append + RUIN_MAP 6줄).
- 기존 4종 "재현 비교"는 하지 않음 (기존 4종 유지, 요청 범위가 6 샘플 시트). 필요하면 후속.
- 시트: `korean-division/docs/art_review/housegen_proto.png` (한국 초가/기와 맞배+담/팔작 + 만주 벽돌 평지붕+담/토벽/붉은벽돌 긴 집 + 팔레트 변형 3). 사용자 검수 대기. 에이전트는 이미지를 직접 못 봄 (수치 검사만: 테두리 접촉 0, 변형 색 평균 차이 확인).
- 실측: 집 1개(2상태·3LOD) 평균 ≈ 10 s, 최대 ≈ 20 s (PLAN 4.3 갱신). import / theme.json 키는 T55m 담당.
