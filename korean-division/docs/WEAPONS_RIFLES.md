# 소총 (N54, 사용자 2026-10-19)

공통: 억제 60 · 탄약 100 · 간격 ±0.3초 (cooldownMin/Max). `ammoType` = 탄약 종류 (자료만, 보급 규칙은 나중). `rifle` 은 별칭으로 유지.

| id | 이름 | 피해 | 사거리 | 간격 | 명중 기본/근/원 | 탄약 종류 |
|---|---|---|---|---|---|---|
| rifle_mosin | 모신나강 소총 | 40 | 24 | 2.1 | .25/.35/.08 | 노식 소총탄 |
| rifle_berdan2 | 베르단 II 소총 | 40 | 24 | 3.0 | .25/.35/.07 | 노식 소총탄 |
| rifle_arisaka38 | 38식 아리사카 소총 | 35 | 23 | 2.0 | .27/.38/.09 | 일식 소총탄 |
| rifle_murata | 무라타 소총 | 40 | 20 | 3.3 | .22/.33/.06 | 구식 일본 소총탄 (일식과 별개) |
| rifle_hanyang88 | 한양 88식 소총 | 45 | 25 | 2.3 | .25/.37/.08 | 독식 소총탄 |
| rifle_mauser98 | 98식 마우저 소총 | 45 | 25 | 2.3 | .28/.40/.10 | 독식 소총탄 |
| rifle_lebel86 | 86식 르벨 소총 | 45 | 22 | 2.3 | .24/.35/.07 | 불식 소총탄 |

## 세력별 분배 (소총 칸 보병 = 유닛 weapon `rifle_mosin`)
korean 모신 100 · russian(백군 · 적군) 모신 100 · japanese 38식 100 · fengtian 38식 60 / 한양 30 / 르벨 10 · bandit(소총병) 무라타 40 / 모신 30 / 38식 20 / 한양 10 · 공용 유닛 모신. 마우저 98 · 베르단 II 는 분배 없음 (정의만).

- 자료: `balance.json "factionRifles"` {세력 en → {무기 → 가중치}}. 다른 무기 (기관총 · 저격 · 카빈 · 돌격) 는 그대로.
- 생성 시 결정적 선택: `Defs.WeaponKeyFor(def, en, unitId, mapSeed)` — 황금비 수열 (id 연속 → 표에 고르게, 1000명 표본 오차 1% 미만), 시드는 시작점. 결과는 `Unit.Weapon` 키 (저장 · 체크섬에 이미 포함 → 스냅샷 형식 변화 없음).
- 미션이 고정: 병력/중대 `"weapon": "rifle_mauser98"` → 소총 칸 보병 전원 그 소총 (분배 무시).
- 선택 패널: `Hud` 가 `u.Weapon.Name` 표시 (실제 든 소총 이름).

## 한국군 보병 단계 (사용자 2026-10-19) — 내부 id 유지, 이름만 바뀜
| 단계 | 이름 | 유닛 id | 소총 | 그림 (기존 테마 연결) |
|---|---|---|---|---|
| 0 | 지방대 | `infantry_default_old` (신설 — 조사 시점 main 에 없었음, 스프라이트 id infantry-default-old) | 베르단 II | `unit.infantry_reserve` (예비군 옛 군복) 대용. **검은 옛 군복 시트(infantry-default-old)는 어디에도 없음** — game-tools 에는 infantry-default-darkgrey · kepi 만 있고 미반입. 시트가 오면 테마에 `unit.infantry_default_old` 추가 시 자동 우선 |
| 1 | 진위대 | `infantry` (옛 징집병 · 정규병 단계를 합침) | 모신나강 | infantry |
| 3 | 친위대 | `infantry_regular` (철모) | 모신나강 | infantry-regular. **철모: `shellDamageMul` 0.85 — 포격 · 수류탄 (범위 피해)만, 소총 · 기관총 · 근접은 그대로** |
| 4 | 시위대 | `infantry_guard` | **98식 마우저** (`rifle_long` 정의는 유지) | infantry-guard (이미 반입된 남색 의장복 + 피켈하우베 시트) |
| 5 | 돌격대 | `infantry_assault` | 카빈 (그대로) | infantry-assault, 이름만 변경 (돌격보병 → 돌격대) |

- 징집병 (`recruit`) 정의는 유지 — 한국 미션 · 시나리오 JSON 에서는 쓰지 않음 (그래서 한국 미션 변환 없음). 기본 AI 가 편성 단계 (`AiDefs.Recruit`) 로 쓰는 것 + 도구 (`tools/bn_campaign`, `import`, `n18_exchange`) 에서 참조.
- `shellDamageMul` (유닛 필드, 기본 1): `Explode` (포탄 폭발) · 수류탄 폭발의 유닛 피해에 곱한다. 결정적 (곱셈만, 상태 없음).

## 봉천 대도병 (`infantry_dadao_fengtian`) = 한국 지방대에 해당하는 봉천 하급 보병
대도뿐 (`weapon` = `melee` = dadao, `meleeOnly`: 생성 때 돌격만 켜짐 → 사격 불가, 마적 잡병과 같은 규칙), 체력 · 속도 = 봉천 보병, `factions:["fengtian"]`, 스프라이트 id `infantry-dadao-fengtian` (그림이 오기 전엔 봉천 보병 그림 `artAs`).
