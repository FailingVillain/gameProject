# T16 trench_assault 하락 (N32 접근 노출 줄이기, D12 A)
- id: T16 | 역할: sim (scout 진단 완료: docs/TRENCH_ASSAULT_DIAG_2026-10-22.md) | 등급: D上 H2 (선택 메모: R2 M Wheavy)
- 선행: T03 (wip 브랜치 위 스택, 병합 없음) | 후행: T12
- 핫스팟: assets/data/tactics.json trenchAssault 블록 (boundEndDist 12 · prepMinSec 8 · suppFrac 0.5 · aiEnabled) · 필요 시 AI 프로필 `korean+notrenchassault` · SelfTest
- 브랜치: `kd/t16-trench` (T03 브랜치에서 분기) | 상태: queued | 담당: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- D12 A: boundEndDist 12->20 · prepMinSec 8->20 · suppFrac 0.5->0.7 각 20판 재측정 뒤 채택, 안 되면 AI 만 N32 끔 (사용자 2026-10-22)
- 진단: 주원인 N32 (한국 승 기본 2/10 -> N32 끔 7/10); 붕괴 켜기 · aiMinOdds 1.5 는 효과 없음 (n=10)
## 범위 밖
붕괴(breakChance) · aiMinOdds 변경; yalu_crossing · ai_bn_assault (원인 다름); 플레이어 N32 명령 (유지); 그 외 수치
## 수용 기준 (측정 가능)
- 값별(3개) 각 20판 재측정 표 (한국 승수 · 일본 손실 · 참호 진입 peak)
- 채택 기준: 한국 승 기본 2/10 대비 뚜렷한 개선 (D12 에 임계 없음 -> 질문 1)
- 채택 시 tactics.json 3값 반영; 개선 없으면 AI 만 N32 끔 (aiEnabled=false 또는 미션 프로필), 플레이어 명령 유지
- 다른 미션 (yalu_crossing 5/10 · ai_bn_assault 9/10) 악화 없음; detcheck
## 측정
`--mission-run trench_assault --seconds 1200 --seed 101..120` (변종 = bin 사본의 json 만 수정, 판당 ~5초). 기준선: 진단 문서 + docs/BASELINE_CURRENT.md
## 열린 질문
1. "채택" 승률 임계 (예 20판 중 N승 이상)? 세 값을 개별/조합 어느 쪽으로 측정?
2. 3값이 전역 trenchAssault 설정이라 다른 미션 영향 -> 미션 한정이 필요한가?
## 결과 (에이전트가 끝에 3~5줄만)
-
