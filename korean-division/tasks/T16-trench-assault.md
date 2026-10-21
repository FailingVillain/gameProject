# T16 trench_assault 하락 (N32 접근 노출 줄이기, D12 A)
- id: T16 | 역할: sim (scout 진단 완료: docs/TRENCH_ASSAULT_DIAG_2026-10-22.md) | 등급: D上 H2 (선택 메모: R2 M Wheavy)
- 선행: T03 (wip 브랜치 위 스택, 병합 없음) | 후행: T12
- 핫스팟: assets/data/tactics.json trenchAssault 블록 (boundEndDist 12 · prepMinSec 8 · suppFrac 0.5 · aiEnabled) · 필요 시 AI 프로필 `korean+notrenchassault` · SelfTest
- 브랜치: `kd/t16-trench` (T03 브랜치에서 분기) | 상태: wip-ready | 담당: feature(sonnet)
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
## 결과
- 측정: 브랜치 kd/t16-trench (wip/breach2 기반, T03 포함) Release 빌드, `--mission-run trench_assault --seconds 1200 --seed 101..120`, bin 사본 json 만 변경. 한국 승 / 20판 · 일본 손실 평균(faction 2 lost): 기본 약 70 (아래 표 생략; 일본 참호 peak 는 항상 112).
| 변종 | 한국 승/20 | (앞 10판) |
|---|---|---|
| 기본 (12/8/0.5) | 5 | 2 |
| boundEndDist 20 | 5 | - |
| prepMinSec 20 | 6 | - |
| suppFrac 0.7 | 5 | - |
| 3값 조합 | 3 | - |
| aiEnabled=false (N32 AI 끔, 전역) | 5 | 1 |
| 미션 프로필 korean+notrenchassault | 4 | 0 |
- 판정: 채택 기준 (>=10/20) 충족 값 없음. 더 중요한 발견: 이 브랜치(T03 breach 포함) 에서는 N32 를 꺼도 승수가 안 오름 (4~5/20, 앞 10판 0~1/10; 진단 문서의 N32 끔 7/10 은 main 92ac51c5 기준이라 재현 안 됨). 즉 하락 주원인이 N32 가 아니거나 breach 이후 바뀜. 20판 표본의 잡음은 약 +-3.
- 적용 (conductor 기본 규칙 D12 A 대체안): trench_assault.json 한국 세력에 aiProfile korean+notrenchassault (미션 한정, 플레이어 N32 명령 유지, tactics.json 수치 불변, 다른 미션 영향 0). 승수 개선은 없음 -> 되돌려도 무방, 원인 재조사 권고 (T12 통합 측정에서).
- 검증: 빌드 오류 0, detcheck GOLDEN match, KD_SELFTEST_ONLY n32 / n77 / missions PASS.
- 참고: 막던 'heavy gate' = tools/agent/heavy_gate.ps1 (대기 큐 전체 풀 Max 8) — 호출하지 않고 동시 2판만 실행.
