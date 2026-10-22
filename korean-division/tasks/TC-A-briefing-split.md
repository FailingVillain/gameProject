# TC-A 브리핑용 적 규모 분리 + 정보 3단계 (캠페인 C1E1)
- id: TC-A | 역할: feature | 등급: D中 H2
- 선행: T01(gemini-import) · T09 캠페인 스택 | 후행: TC-E
- 핫스팟/만질 파일: 미션 정의(MissionFile) · 브리핑 UI · 캠페인 변수 intel.* · Snapshot 접촉 시 FormatVersion 예약(ops/FV_RESERVATIONS.md)
- 브랜치: 발주 시 지정 | 상태: queued | 담당 에이전트: -
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22: OP1 안동 수비대 실제 1,700 (기마 100 · 야포 4 포함). 게임상 브리핑 표시는 "2개 연대 약 3,000" 오정보.
- 정보 단계: 브리핑 3,000 → intel.andong(위력 정찰 + 문서 작전 활용) 후에도 약 2,000 → OP1 성내 시야 확보 시 1,700 확정(이벤트 E-OP1-0).
- 원문 설계: campaign/spec_c1e1.txt, 캠페인 디자인 v1.txt §26~27.
- 2026-10-22: intel.andong 출처는 SK2 + D07 B 단 하나(D02 A+C 는 intel 아님). spec R3.
- 2026-10-22 대체안(승인, spec R9): 1단계 = 정적 문구 3벌(텍스트 키) + intel.andong 값으로 문구 선택, E-OP1-0 은 팝업 문구로 정정. 완전판(briefingEnemy 필드)은 후속. 변수 이름은 완전판과 동일하게.
## 범위 밖
일반 첩보 탭 · 다른 미션의 정보전.
## 수용 기준 (측정 가능)
- 미션 파일에 briefingEnemy 와 실제 배치가 분리됨. 3단계 표기 시험. 저장/불러오기 후 단계 유지. selftest 통과.
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
selftest + snaptest.
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
-
