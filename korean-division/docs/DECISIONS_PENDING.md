# 사용자 결정 · 권한 대기 (에이전트가 한 줄씩 추가 → 지휘자가 ops/DECISIONS.md 로 옮기며 ID 부여)
> 형식: `- [작업/에이전트] 결정/권한이 필요한 것 — 선택지/권장`. 옮긴 줄은 지휘자가 지운다.
> 2026-10-22 이전 항목은 전부 ops/DECISIONS.md D01~D22 로 이관됨 (원문 = docs/legacy/DECISIONS_PENDING_ARCHIVE_2026-10-22.md).

- [T34d/기병 측면 강도] Q1 '강도' = flankOddsMul·flankMinSupp·flankOffset 세 키로 정의(lo 1.0/0.45, hi 0.5/0.15)해도 되나, minOdds·minRiders·rechargeCooldownSec 도 넣나 — 권장: 세 키만. (상세 Q1~Q8 = docs/T34_3CD_SPEC.md §6)
- [T34d] Q2/Q3 목표 지표(기병 생존>=50%·기관총 교착 0/20)와 D07(소총 대 기병 9/10 시간초과) 을 T34d 측정으로 닫아도 되나 — 권장: 측정 후 표로 제시, 결정은 사용자.
- [T34d] Q4 결정 8(무능 장교 정면 돌격)은 strictCharge=false 때만 켜짐, 기본 true 라 지금 꺼짐 — 의도 확인. Q5 세력별 프로필 vs 전역값. Q6 기병 혼성 시나리오 추가. Q7 하마 사격 끄고 비교. Q8 퇴행 허용폭 -20%p.
- [T34 전체] Q-G1 측정 종료선 50% 사본(*_obs) 채택(T31b 권고)? Q-G2 S12 지휘 대행은 T24 ① 로 일부 충족 — 별도 카드? Q-G3 3000기 대량 시험·대포병 사격(결정 11, AI에 아직 없음)을 T34f 로? Q-G4 난이도 인지 지연(0/2/4초) 새 상태 필요 가능 — AE2 와 합칠지.
