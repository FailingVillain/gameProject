# T39i 가옥 폐허 새 그림
- id: T39i | 역할: art | 등급: D中 H0 (R1 S Wlight)
- 선행: T11 재렌더 완료 (같은 export/tune 파일을 쓰는 경우) | 후행: -
- **의존**: `make_unit.py`/`rerender96.py` 는 건드리지 않는다(건물은 make_building.py). 그래도 공용 export · tune 파일을 고쳐야 하면 T11 wip-ready 뒤에 시작; 시작 전 T11 상태 확인.
- 근거: ROADMAP_LONG ~151 "후속: 가옥 폐허 그림"; 건물 크기 확대(T02, 민가 3x3~3x4 · 큰 건물 4x6, main 병합 8d22ad5)에 맞출 것.
- 핫스팟/만질 파일: game-tools/image/make_building.py 및 건물 정의(폐허 변형), 임포트. 게임 쪽은 폐허 스프라이트가 이미 참조되는지 읽어서 확인만(코드 수정 없음).
- 브랜치: `t39i-ruined-houses` (base main) | 상태: wip-ready | 담당: -
## 확정 결정
- 가옥 폐허 그림 후속 항목(ROADMAP_LONG ~151). 스타일 = 기존 승인 지형/건물 톤.
## 범위 밖
엄폐/통행 수치 · 파괴 로직 · 큰 건물 폐허(요청 시) · 불 효과
## 수용 기준
- 민가 크기별(3x3 · 3x4 최소) 폐허 2변형씩(무너진 벽 + 잔해), 칸 정렬 일치, 기존 건물과 같은 광원.
- 접촉 시트 `docs/art_review/ruined_house.png` (정상 vs 폐허, 줌 3단계).
- 게임 창에서 테스트 지도 배치 캡처 1장.
## 미리보기 · 공통 규칙
게임 창은 `tools/agent/run_game.ps1` (75a1712) 로만. 접촉 시트 항상 작성, 승인 전 import/병합 금지. sim · FV 변경 없음.
## 가정한 답
폐허 = 기존 건물 파괴 후 표시용 스프라이트(엄폐 값 그대로). 신규 메커니즘 없음.
## 결과
가옥 4종 x 폐허 2변형(ruined=a, ruined_b) 렌더 완료 (game-tools tools/buildings/ruins.py, make_building.py). 시트 docs/art_review/ruined_house.png (사용자 검수 대기). 미완: 게임 창 캡처 1장 (승인 전 import 금지라 보류), 3x3/3x4 별도 변형 없음(게임이 발자국에 늘림). 이미지 import 안 함.
