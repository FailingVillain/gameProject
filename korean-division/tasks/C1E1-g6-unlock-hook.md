# C1E1-g6 unlock.* → 편성 가능 병종 (야전포 해금)
- id: C1E1-g6 | 역할: feature | 유형: engine-gap | 모델: sonnet | 등급: D中 H2 (W light)
- 선행(blockedBy): TC-D (후행: s1 보상, q1) | 병렬/직렬: 직렬
- 핫스팟/만질 파일: CampaignState(스키마 추가만) · 편성 가능 병종 필터 · campaign_economy.json 해금 표 · Tests
- 브랜치: kd/c1e1-g6-unlock-hook (base: TC-D 위 스택) | 상태: wip-ready | 담당 에이전트: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22 BT1 보상: 야전포 해금 (unlock.fieldGun). BT1 에서는 보병포만, OP1 에는 야전포 소대 편성 가능, 곡사포는 미해금.
- E1 출력 변수 unlock.fieldGun 은 E2 로 전달.
- 원문: campaign/spec_c1e1.txt (v0.2) · 계획 docs/C1E1_IMPLEMENTATION_PLAN.md. 명세에 없는 것은 만들지 말고 docs/DECISIONS_PENDING.md 끝에 한 줄(= ops/DECISIONS 후보) 적고 속행.
## 범위 밖
다른 해금 내용 · 곡사포 해금 규칙 · 호감·사기 값(보류) · 서사 문구(층 3) · E2 설계 · 밸런스 조정(보류, 사용자 2026-10-22)
## 수용 기준 (측정 가능)
- unlock.* 불리언 집합이 저장·정산에서 유지되고 편성 화면이 미해금 병종을 제외.
- BT1 정산 후 unlock.fieldGun 참, 그 전에는 야전포 편성 불가 시험.
- 저장 호환 (마이그레이션 시험), Audit.
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
dotnet build · selftest(관련 묶음) · detcheck · snaptest · 기준선 docs/BASELINE_CURRENT.md
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
- 커밋 kd/c1e1-g6-unlock. unlock.* = CampaignState.Flags["unlock.xxx"] (저장 필드 추가 없음 -> 스키마 11 그대로, 번호 재조정 불필요; TC-E campaign_vars 의 unlock.fieldGun 과 같은 키). 표 = campaign_economy.json unlockKinds(fieldGun -> unlock.fieldGun) · unlockOnWin(c1e1_bt1). API CampaignUnlocks. BeginOperation 거부 + 편성 화면 제외 + Apply 첫 승리 해금. 시험 CampaignUnlocksSelfTest(savecompat 묶음). 빌드·savecompat·snaptest·detcheck(골든 일치) 통과.
- 남은 것: c1e1_bt1 은 아직 Chain 밖이라 Apply 경로로는 g1/s1 이 Chain 에 넣을 때 활성 (시험은 표 교체로 검증). 야전포 소대 Roster 중대(Kind fieldGun) 생성은 범위 밖 - 제안만.
