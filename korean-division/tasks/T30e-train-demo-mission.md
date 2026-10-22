# T30e 시연 미션 1개 (장갑열차 + 화물열차) — T30c 위 스택
- id: T30e | 역할: feature | 등급: D中 H1 (R2 S W=light)
- 선행: T30c (T30d 있으면 그림 적용, 없으면 자리표시) | 후행: -
- 핫스팟: assets/missions/ 신규 json, assets/maps/ 신규 지도(선로 포함), SelfTest 미션 시험, golden 1건 추가
- 브랜치 `t30e-demo-mission` (kd/t30c-freight-objective 위) | 상태: wip-ready | 담당: -
## 확정 결정
TRAIN_PLAN. 시연용이며 캠페인 순서 연결은 하지 않는다(캠페인 연결·스토리는 사용자 결정 뒤).
## 범위 밖
캠페인 편입, 새 규칙, 밸런스 조정, 적 열차 AI, 선로 파괴.
## 수용 기준
1. 단일 지도 1개 + 미션 1개: 아군 장갑열차(조종) + 화물열차 호송 또는 탈취 목표(Q-E1), 적 방해 부대.
2. 미션 검증 통과, AI 판 전체 플레이(헤드리스) 완주 1회: 승패 모두 결과 화면 표시.
3. 시드 8 반복에서 크래시 0, 체크섬 2회 일치; 새 golden 1건 등록.
4. 기존 golden · SelfTest 회귀 0.
## 측정
헤드리스 미션 실행 시드 8 + golden. 결과는 BASELINE 에 쓰지 않고 카드에만 기록.
## 질문
- Q-E1 시연의 목표는 호송인가 탈취인가? (권장: 호송, 가장 단순)
- Q-E2 지도는 기존 지도 개조인가 신규인가? (권장: 신규 소형)
## 결과
- 브랜치 t30e-demo-mission (t30c-freight-objective 위, 리뷰 수정 3de60d5 cherry-pick 포함). Q-E1 = 호송 (카드 권장), Q-E2 = 신소형 지도 (미션 json 안 mapData 150x64, 단선 2줄 y=30 장갑 / y=36 화물). 사용자 확인 대기.
- 미션 `assets/missions/train_escort_demo.json` (캠페인 미연결): 아군 장갑열차 (기관차+포차+기관총차) + 화물열차 (기관차+화차3) 스크립트 이동, 목표 = 화차 2량 이상 보급역, 시간 제한 900초. 적 마적 79 (저지선 20 · 매복 34 · 수비대 24, 대전차병 10 포함); 아군 호위 중대 31 (대전차병 4). 열차 밸런스 · 새 규칙 · 새 상태 없음 (FV 59 그대로).
- 승패율 (미션 AI, 시드 1~16): 승 16 / 패 0, 완료 117~174초, 아군 손실 0~3. 패배 경로 = 시간 제한 40초 변형으로 패배 확인. 열차 8-0 문제는 이 미션에서 아군 열차가 유리하게 작용 (적 열차 없음).
- 시험: `KD_SELFTEST_ONLY=traindemo` (src/Tests/TrainDemoSelfTest.cs: 로드 · 완주 승리 · 같은 시드 2회 체크섬 일치 · golden `assets/data/train_demo.golden` · 패배 변형 · 결과 기록) PASS. missions · train · trainweapons · freight PASS, --mission-run --verify 리플레이 OK, uifuzz 30 예외 0, detcheck GOLDEN match.
- 그림: T30d 블록아웃은 연결하지 않음 (자리표시 그대로; 연결은 art 의 가져오기 단계 필요).
