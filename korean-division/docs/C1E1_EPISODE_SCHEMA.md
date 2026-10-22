# C1E1 에피소드 그래프 스키마 (g1, 2026-10-22)

> 근거: `campaign/spec_c1e1.txt` v0.2 (공통 · 일수 확정 규칙 · R1 · 입력/출력 변수) · `docs/C1E1_IMPLEMENTATION_PLAN.md` §1.2/§1.5/§4 Q14·Q18.
> 소비자: d1 (노드·결정 데이터), g2 (화면), TC-E (결정 런타임 · 변수 레지스트리). 이 문서는 **구조만** 정한다 — 결정 내용 · 서사 · 호감/사기 값은 없다.

## 1. 데이터 파일
- 위치: `assets/data/episodes/<episodeId>.json` (E1 = `assets/data/episodes/c1e1.json`). 게임과 같이 배포.
- 로더: `EpisodeDefs.Load(path)` / `EpisodeDefs.Parse(text, errors)` (`src/Core/Campaign/CampaignEpisodeDefs.cs`). 검증 실패 = `null` + 한국어 오류 목록 (게임은 계속).
- 주석(`//`)과 끝 쉼표 허용. 모르는 키는 무시 (d1 이 노드에 필드를 더해도 로더는 깨지지 않는다).

```json
{
  "id": "c1e1",
  "dayCap": 7,
  "start": "EV1",
  "nodes": [
    { "id": "EV2", "kind": "EV", "decisions": ["D10"], "deadline": { "follow": 5, "delay": 6, "persuadeOk": 7, "persuadeFail": 5 }, "next": "IL2" },
    { "id": "IL2", "kind": "IL", "decisions": ["D16"], "waitDays": 1, "next": "OP1" },
    { "id": "SEL1", "kind": "SELECT", "decisions": ["D05"], "options": ["SK1", "SK2", "SK3", "SK4"], "next": "SEL2" },
    { "id": "SK1", "kind": "SK", "mission": "c1e1_sk1", "days": 1 },
    { "id": "OP1", "kind": "OP", "variants": [ { "key": "assault", "mission": "c1e1_op1_assault", "days": 2 }, { "key": "siege", "mission": "c1e1_op1_siege", "days": 4 } ], "checkDeadline": true, "next": "D14" }
  ]
}
```

## 2. 최상위 필드
| 필드 | 형 | 뜻 |
|---|---|---|
| `id` | string (필수) | 에피소드 id. 저장의 `Episode.EpisodeId` 와 맞춘다 |
| `dayCap` | int >= 1 (필수) | 에피소드 일수 상한 (E1 = 7). 넘기면 기한 초과와 같은 페널티 (R1 [제안] 승인) |
| `start` | string (필수) | 첫 노드 id |
| `nodes` | 배열 (필수) | 노드 목록. 순서는 의미 없음 (전이는 `next`) |

## 3. 노드 종류와 필드
공통: `id` (필수, 유일), `kind` (필수), `decisions` (선택, 문자열 배열 — 이 노드에서 묻는 결정 id. **정보용**: g1 은 해석하지 않고 g2/TC-E 가 읽는다), `next` (다음 노드 id, `""` = 에피소드 끝).

| kind | 뜻 | 추가 필드 | 진행 API |
|---|---|---|---|
| `EV` | 이벤트 (팝업/연출 + 결정) | `deadline` (선택: 결과 키 → 기한 일수) | `Resolve(outcome)` |
| `CH` | 챕터 홈 결정 (보충 · 군 내부) | `deadline` (선택) | `Resolve(outcome)` |
| `IL` | 편성 (홈 구간) | `waitDays` (선택, 0 = 대기 없음) | `ConfirmFormation(wait)` |
| `BT` | 필수 전투 | `mission`, `days`, `onLose` | `BeginMission` → `MissionWon` / `MissionLost` |
| `SK` | 선택 전투 (SELECT 의 선택지) | `mission`, `days`, `onLose`, `failDays` | 같음. `next` 없음 — 끝나면 부른 SELECT 의 `next` 로 |
| `OP` | 작전 (변형 분기) | `variants` [{`key`,`mission`,`days`}], `checkDeadline`, `onLose` | `ChooseVariant(key)` → `BeginMission` → … |
| `SELECT` | SK 고르기 1칸 | `options` (SK 노드 id 배열) | `Select(skId)` / `Select(null)` = 건너뛰기 |
| `REPORT` | 전과 보고 (결정 가능) | `deadline` (선택) | `Resolve(outcome)` |

- `onLose`: `"retry"` (BT/OP 기본) 또는 `"retryOrFail"` (SK 기본). `failDays` (SK, 기본 0): 실패 확정 때 쓰는 일수.
- `deadline`: `{ "<결과 키>": 일수 }`. `Resolve(outcome)` 의 outcome 이 표의 키여야 한다 (아니면 거부). 기한은 `1..dayCap`.
  E1 의 결과 키 (EV2 = D10): `follow`(따름 5) · `delay`(지연 6) · `persuadeOk`(설득 성립 7) · `persuadeFail`(설득 실패 5). 설득 성립/실패 판정(R7: intel.andong 유무)은 결정 런타임(TC-E/d1)이 해서 키로 넘긴다.
- `checkDeadline: true` 인 미션 노드가 이기면 기한 판정 (아래 §5).

## 4. 검증 (로드 거부 조건)
id 중복 · 빈 id · 모르는 kind · `start` 없음 · `next` 가 없는 노드를 가리킴 (끊긴 전이) · SELECT 의 `options` 가 비었거나 SK 가 아닌 노드/없는 노드 · SK 에 `next` · 미션 노드에 `mission`/`variants` 없음 · `days`/`waitDays`/`failDays` 음수 · OP 변형 key 중복 · `deadline` 값이 `1..dayCap` 밖 · 시작에서 닿지 않는 노드 · `next` 사슬의 순환 · 끝(`""`)이 없는 사슬.

## 5. 일수 · 기한 규칙 (사용자 확정 2026-10-22)
- `DaysUsed` 는 0 에서 시작. **미션 승리 시점에만** 그 노드의 `days` (OP 는 고른 변형의 `days`) 를 더한다. 재도전은 일수를 쓰지 않는다.
- IL 의 대기(D16)는 `ConfirmFormation(wait: true)` 시점에 `waitDays` 를 더한다.
- SK 실패 확정은 `failDays` (기본 0 — 명세에 일수 언급 없음, DECISIONS_PENDING 에 기록).
- `checkDeadline` 노드 승리 시: `Late = (DeadlineDay > 0 && DaysUsed > DeadlineDay) || DaysUsed > dayCap`. 패배가 아니라 승리 후 페널티 표시 (페널티 내용은 TC-E/d1).
- E1 총일수 = BT1 1 + 이긴 SK 수 + OP1 (강공 2 / 포위 4) + 대기 1 → R1 표 6행 x 4열 x 대기 유/무와 같다 (시험 `episode` 묶음).
- IL 화면 예고: `ProjectDays(variantKey, wait)` = 지금 DaysUsed + 남은 사슬의 대기 · 미션 일수 (SELECT 는 0). `WouldBeLate(total)` 로 ⚠ 표시.

## 6. SELECT 규칙 (Q14)
- SELECT 노드 1개 = 한 칸. E1 은 SEL1(D05) · SEL2(D06) 두 칸 → SK 0~2개.
- 같은 SK 는 다시 못 고른다 (이긴 것 · 실패 확정한 것 모두). 건너뛰기 = 다음 노드로 (뒤 칸에서는 여전히 고를 수 있다).

## 7. 재도전 복원
- `BeginMission` 이 체크포인트를 만든다: 변수 전체(`ICampaignVars.SnapshotAll`) + `CampaignState.Supply`.
- `MissionLost(confirmFail: false)` = 재도전: 변수 · 보급을 체크포인트로 되돌리고 같은 노드에 남는다 (일수 불변). OP 는 변형 선택도 지운다 → **IL2 선택 이후 상태** (대기 일수는 이미 들어 있음).
- `MissionLost(confirmFail: true)` (SK 만): 되돌린 뒤 실패 확정 기록 (`FailedSk`) 하고 SELECT 의 `next` 로.

## 8. 변수 (ICampaignVars)
- `ICampaignVars { int Get(key); void Set(key, value); bool Has(key); SnapshotAll(); RestoreAll(list); }`. 지금 구현 = `FlagsCampaignVars` (`CampaignState.Flags` 위, 없는 키 = 0). TC-E 가 레지스트리로 바꿔 끼운다.
- 이름 (spec R9, 계획 §1.5 — 이름 고정, 값 증감은 [보류]=0): `favor.hwangson` `favor.minDaejang` `favor.namChamjang` `favor.siwidae` `favor.yiSeok` `rep.local` `morale.troops` `intel.andong` `pow.bandit` `flag.flankPath` `flag.dockIntel` `flag.andongCavalryCut` `flag.andongSurrender` `flag.andongRemnant` `flag.siwidaeDispatched` `flag.namLetter` `unlock.fieldGun`. 정수만. g1 자신은 어떤 변수도 쓰지 않는다 (체크포인트 복원만).

## 9. 저장 (CampaignState.Episode, 캠페인 스키마 v12 — 번호는 병합 때 정렬)
`Episode` (null = 에피소드 밖, 옛 파일): `EpisodeId` · `NodeId` (`""` = 끝) · `SelectNode` (SK 진행 중일 때 부른 SELECT) · `DaysUsed` · `Selected` (고른 SK 순서) · `FailedSk` · `Variant` · `Waited` · `DeadlineDay` (0 = 없음) · `Late` · `Outcomes` [{`Node`,`Outcome`}] · `Checkpoint` {`Vars` [{`Key`,`Value`}] 키 정렬, `Supply`} (미션 중일 때만).
캠페인 저장은 시뮬레이션 스냅샷 밖이라 sim FormatVersion · 체크섬 · Audit 영향 없음. 옛 파일(v3~v11)은 `Episode = null` 로 이행.
