# T32a 전투공병 infantry_pioneer 병종 데이터 (철조망 절단 전용)
- id: T32a | 역할: feature (병종 데이터 + 시험) | 등급: D中 H1 (선택 메모: R1 S Wlight)
- 선행: T05 (ai3b2 위 스택 — breach 의 wireCutter/CutWire 필요) | 후행: T32c, T32d(그림)
- 핫스팟/만질 파일: assets/data/balance.json (units 에 1항목 + 세력 목록), 편제 데이터가 병종 id 를 참조하면 그곳(캠페인/미션 편성은 건드리지 않음), src/Tests/BreachSelfTest.cs 또는 새 PioneerSelfTest, docs/ENGINE_NOTES.md 한 단락. Snapshot/Replay/Defs 코드 접촉 없음 (데이터 + 시험만; Defs 필드 신설이 필요하면 멈추고 질문).
- 브랜치: `kd/t32a-pioneer` (base = ai3b2 f74a675, 작업 폴더 kd-develop/kd-t32a) | 상태: wip-ready | 담당: feature(Sonnet)
## 확정 결정 (사용자 원문+날짜, 변경 금지)
- ROADMAP_LONG 규칙 15 "공병 (사용자 승인): 철조망 절단 (+ 나중 선로 파괴). 참호 구축 제외. 돌격대(돌격보병)도 공병 역할(철조망 절단 등) 수행 가능." 추가 병종 보류 줄(~243)의 전투공병 `infantry-pioneer`.
- 2026-10-22 사용자: 전투공병 `infantry-pioneer` 보류 해제 (지휘자 전달). 범위 = 철조망 절단까지.
## 범위 밖
참호 파기·구축, 철조망 설치, 폭파(~243 의 설치/폭파는 규칙 15 가 허용한 범위 밖), 선로 파괴(나중), 의무병, 박격포반, 신규 명령·저장 상태, 캠페인 편성 투입.
## 설계
- 병종 `infantry_pioneer` (표시 "전투공병"): 기존 breach 기능만 사용 = `wireCutter: true` (CutWireCommand 로 절단, 칸당 breach.wireCutSec 4초). 명령 버튼은 Commands.cs 의 `d.WireCutter` 조건이 자동 노출 — 코드 변경 없음.
- 초안 수치 (돌격보병과 소총병 사이, 전투 병과 아님): hp 100 · speed 3.2 · sight 32/20/32 · accuracy 1.0 · category infantry · suppressResist 0.6(소총병 기본값에 맞춤; 확인) · canCapture true · autoAttack true · grenades 2 · smokeGrenades 0 (연막은 돌격보병 몫; 질문 Q2) · wireCutter true.
- 무기·소속: 무기 `rifle_mosin`(한국 보병 기본 소총) 로 초안, 세력 = 한국군 전용(`koreaOnly: true`, 돌격보병과 같은 방식). 근접 `bayonet`. (질문 Q1·Q3)
- 그림: 별칭 `artAs: "infantry_regular"` 임시(진위대 모델에 변형 없이). 최종 그림은 T32d.
## 수용 기준 (측정 가능)
1. 빌드 오류 0, balance.json 로드 OK, `infantry_pioneer` 가 Defs 에 있고 `WireCutter` true.
2. 새 시험 (KD_SELFTEST_ONLY=pioneer): (a) 공병 1명이 CutWire 로 철조망 1칸을 wireCutSec ±1틱 안에 연다 (BreachSelfTest 철조망 시험과 같은 배치), (b) 일반 소총병은 CutWire 를 받아도 무시, (c) 돌격보병도 여전히 절단 (회귀), (d) 공병은 SmokeGrenades 0.
3. detcheck 골든 일치 (새 병종이 어떤 기본 미션/편성에도 없으므로 시뮬 불변), snaptest QUICK exit 0, FormatVersion/Replay 번호 불변.
4. 기존 breach/ai3b/s3a 묶음 PASS.
## 측정 (도구 · 기준선)
selftest 묶음 + detcheck + snaptest (docs/BASELINE_CURRENT.md 최신 줄 대비 변화 없음). 밸런스 측정 없음 (편성에 안 들어감).
## 질문 (≤8)
- Q1 무기: 소총(rifle_mosin) vs 단축 카빈(carbine_assault) vs 권총만?
- Q2 공병에게 연막 수류탄을 줄까 (규칙 15 에 없음)? 초안 0.
- Q3 한국군 전용 vs 공용 (적/동맹 세력 AI 도 공병을 쓰나)?
- Q4 한 중대 편성에 몇 명 (분대당 1~2?)? 이 카드는 편성에 넣지 않음 — 편제 카드에서.
- Q5 제압 저항·체력은 소총병 기준(초안) 으로 충분한가?
## 결과 (에이전트가 끝에 3~5줄만)
- 구현: balance.json `infantry_pioneer` + src/Tests/PioneerSelfTest.cs (+ SelfTest.cs 등록, Combat4 수류탄 보유 기대 갱신) + ENGINE_NOTES 한 단락.
- 질문 답 (카드 권고): Q1 소총 rifle_mosin + 총검 · Q2 연막 0 (수류탄 2는 초안대로) · Q3 한국군 전용 koreaOnly · Q4 편성 미투입(편제 카드) · Q5 소총병 기준 hp100/suppressResist 0.75 (진위대 infantry 값에 맞춤; 초안의 0.6 대신 실제 기본값 0.75).
- 그림: artAs infantry_regular 임시 (theme 항목 불필요; 별칭으로 동작). T32d 에서 전용 그림.
- 검증: 빌드 오류 0 · pioneer/breach/units/factions/missions/combat4/savecompat PASS · detcheck GOLDEN 일치 · snaptest QUICK exit 0 · FV/Replay 불변. 기존 시험 기대 2곳 갱신(Combat4 수류탄 보유, Mission koreaOnly 목록).
