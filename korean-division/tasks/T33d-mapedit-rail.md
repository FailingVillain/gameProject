# T33d (선택) 지도 편집기 철도 지형 도구
- id: T33d | 역할: feature | 등급: D中 H2 (R1 S Wlight)
- 선행: T33b + T07 (rail 브랜치 wip-ready) -- **rail 브랜치 위에 스택**, main 단독 불가 | 후행: 없음
- 핫스팟/만질 파일: game-tools/map-editor/ 직렬화 (line op), 팔레트 (rail); 게임 MapFile line 은 rail 브랜치 것 그대로 (수정 금지)
- 브랜치: kd/t33d-mapedit-rail (rail 위) | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- "게임 안 기능이 아니라 별도 도구로 만든다" (사용자 2026-10-19, game-tools/mission-editor/DESIGN.md) -- 질문 1 에서 재확인 중
- "가벼운 지도 편집기 = 로드맵: 자동 생성 지도를 사용자가 조금 수정 -- 지형 칠하기 · 참호/철조망 · 목표 · 시작 위치 · 저장" (사용자 2026-10-21); 보류 해제 2026-10-22 (지휘자 전달, 원문 확인 필요)
- 편집기 출력은 데이터 JSON 뿐: 시뮬 · FormatVersion · 리플레이 · 체크섬 불변
- "철도 지형은 추가를 하는걸로 하자" (사용자, 날짜 확인 필요 -- T07 카드와 동일)
## 범위 밖
장갑열차/화물열차 · 선로 파괴 · 다리/여울 전용 타일 (N52) · rail 병합 자체
## 수용 기준 (측정 가능)
- 꺾은선 도구 -> paint terrain rail + line 저장, 로드 후 rail 타일 격자가 편집기와 동일
- rail 포함 지도 열기/저장 왕복 동일; T33a 골든 전부 유지
- RailRenderer 표시 (꺾임/대각 연결) 가 편집기와 게임에서 같음 (비교 시트)
## 측정
rail 브랜치 TerrainSelfTest + 편집기 왕복 시험
## 결과 (3~5줄)
-
