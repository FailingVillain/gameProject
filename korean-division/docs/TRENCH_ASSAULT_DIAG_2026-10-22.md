# trench_assault 하락 원인 조사 (2026-10-22, 측정만 — 코드/수치 변경 없음)
- 측정 고정 커밋 main 92ac51c5, `--mission-run trench_assault --seconds 1200 --seed 101..110` (10판/변종, 판당 ~5초). 변종 = bin 사본의 balance/tactics/미션 json 만 수정.
- 한국군 승수 (n=10): **기본 2/10** · breakChance 0.15 (붕괴 켬) 3/10 · **N32 끔 (한국 aiProfile notrenchassault) 7/10** · 붕괴 켬 + N32 끔 3/10 · aiMinOdds 1.5 3/10.
- 판정: **주원인 = N32 참호 돌격 AI 교리** (끄면 2 → 7). nobreak (붕괴 끔) 는 주원인 아님 (켜도 2 → 3, 잡음 수준; 붕괴+N32 끔 조합 3/10 은 일관성 부족 — 표본 작음).
- 패배 양상: 한국 137명 중 96명 (70%) 손실이 패배 조건. 기본 변종은 일본 손실 40~100 만 내고 한국이 먼저 96에 도달, 참호 진입 peak 0~26 (일본 112) = 접근 단계 (약진/제압 준비) 에서 기관총 앞에 노출돼 출혈.
- N32 끈 판은 한국이 일본 손실 80~116 + 참호 점령 (peak 5~36) 으로 이김 → 참호 돌격 순서가 일반 공격보다 느리고 취약.
- yalu_crossing (기본 5/10 승, 3 시간초과) · ai_bn_assault (기본 9/10): N32 끔/붕괴 켬 모두 차이 없음 (n=10, 같은 결과) → **같은 원인 아님**. soak 78%/59% 는 다른 변경 (pronemove · units 등) 또는 표본 잡음 가능; 현 main 은 ai_bn_assault 90%.
- aiMinOdds 0.8 → 1.5 로는 해결 안 됨 (3/10): 트리거 병력비가 방어병 보이는 수 기준이라 이 미션에선 계속 켜짐.
- 추천 (사용자 승인 필요, DECISIONS_PENDING 기록): (1) 참호 돌격을 N32 `boundEndDist` 12 → 더 큰 값 (예 20) / `prepMinSec` 8 → 20 / `suppFrac` 0.5 → 0.7 처럼 접근 노출 시간을 줄이는 쪽으로 20판씩 재측정, (2) 안 되면 `tactics.trenchAssault.aiEnabled` false (AI 만 끔, 플레이어 명령 유지) — 미션 trench_assault 만 `korean+notrenchassault` 프로필도 가능.
- 한계: n=10, 후보 수치 재측정(1)은 이 조사에서 하지 않음 (지시: 수치 변경 금지).
