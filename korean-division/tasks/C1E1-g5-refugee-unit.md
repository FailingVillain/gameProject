# C1E1-g5 피난민 비전투 유닛 정의 (자리표시 그림) + 호송 목표 점검
- id: C1E1-g5 | 역할: feature | 유형: data | 모델: sonnet | 등급: D下~中 H1 (W light)
- 선행(blockedBy): C1E1-u1 (후행: s4) | 병렬/직렬: 병렬
- 핫스팟/만질 파일: assets/data/balance.json (유닛 추가만, 기존 값 불변) · 그림 자리표시(기존 민간 계열 스프라이트 재사용) · escort 목표 시험 한 건
- 브랜치: kd/c1e1-g5-refugee-unit (base: main) | 상태: wip-ready | 담당 에이전트: feature (Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- 2026-10-22 SK3: 호송 대상 = 피난민 (비전투 유닛), win = 마적 소탕 + 피난민 70% 이상 호송 목적지 도착, lose = 피난민 50% 이상 사망.
- 그림은 새로 그리지 않는다 — 기존 스프라이트 재사용 자리표시, 본 그림은 사용자 검수 후 별도.
- 원문: campaign/spec_c1e1.txt (v0.2) · 계획 docs/C1E1_IMPLEMENTATION_PLAN.md. 명세에 없는 것은 만들지 말고 docs/DECISIONS_PENDING.md 끝에 한 줄(= ops/DECISIONS 후보) 적고 속행.
## 범위 밖
피난민 AI 행동(도주·공포) 신설 · 새 그림 · 밸런스 · 호감·사기 값(보류) · 서사 문구(층 3) · E2 설계 · 밸런스 조정(보류, 사용자 2026-10-22)
## 수용 기준 (측정 가능)
- 유닛 id 하나 (비전투, 무기 없음, 사격 불가, 기존 태그/escort 목표와 호환), 한국/중립 세력 허용.
- escort 목표 시험: 태그 피난민 N명 중 70% 도착 시 달성, 50% 사망 시 실패 (실패 조건 표현은 keyUnit 또는 losses 대체 여부를 u1 보고 기반으로 기록).
- Defs 로드·유닛 도감·미니맵 표시 크래시 0, detcheck 불변 (미사용 유닛).
## 측정 (도구 · 기준선 docs/BASELINE_CURRENT.md)
dotnet build · selftest(관련 묶음) · detcheck · snaptest · 기준선 docs/BASELINE_CURRENT.md
## 결과 (에이전트가 끝에 3~5줄만 기록; 에이전트는 자기 카드의 결과/상태만 수정)
- 유닛 `civilian_refugee` 는 base(u2 8061b9a)에 이미 있음 — 이 카드는 정의를 새로 만들지 않고 호송 시험만 추가 (MissionSelfTest, KD_SELFTEST_ONLY=missions 4건: 비무장 배치 · 6/10 도착 진행 · 7/10 도착 승리 · 4/10 사망 패배).
- 승리 70% = 기존 escort `count`=ceil(0.7N). 실패: escort 는 생존 < count 면 Failed 라 사망 30% 초과(>30%)에서 이미 실패 — 명세 "50% 이상 사망 = 패배"보다 엄격함. keyUnit 은 1명만 죽어도 실패라 부적합, losses 는 플레이어 전체 비율이라 피난민 한정 불가. 정확히 50% 는 g4 (losses% 변수) 몫 → s4 는 escort count 로 쓰거나 g4 대기 (DECISIONS 후보).
- 코드 변경 없음(시험만): detcheck GOLDEN 8aa6448b 일치, missions PASS, snaptest OK, FV 변경 없음.
