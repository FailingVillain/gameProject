# C1E1 Episode Graph Schema (g1, 2026-10-22)

> Basis: `campaign/spec_c1e1.txt` v0.2 (common · day-count confirmation rules · R1 · input/output variables) · `docs/C1E1_IMPLEMENTATION_PLAN.md` §1.2/§1.5/§4 Q14·Q18.
> Consumers: d1 (node·decision data), g2 (screen), TC-E (decision runtime · variable registry). This document defines **structure only** — there is no decision content · narrative · favor/morale values.

## 1. Data file
- Location: `assets/data/episodes/<episodeId>.json` (E1 = `assets/data/episodes/c1e1.json`). Distributed with the game.
- Loader: `EpisodeDefs.Load(path)` / `EpisodeDefs.Parse(text, errors)` (`src/Core/Campaign/CampaignEpisodeDefs.cs`). Validation failure = `null` + a list of Korean error messages (the game continues).
- Comments (`//`) and trailing commas allowed. Unknown keys are ignored (the loader does not break even if d1 adds fields to nodes).

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

## 2. Top-level fields
| Field | Type | Meaning |
|---|---|---|
| `id` | string (required) | Episode id. Matches `Episode.EpisodeId` in the save |
| `dayCap` | int >= 1 (required) | Episode day cap (E1 = 7). Exceeding it is the same penalty as missing the deadline (R1 [proposal] approved) |
| `start` | string (required) | First node id |
| `nodes` | array (required) | Node list. Order is meaningless (transitions are `next`) |

## 3. Node kinds and fields
Common: `id` (required, unique), `kind` (required), `decisions` (optional, string array — decision ids asked at this node. **Informational**: g1 does not interpret them, g2/TC-E read them), `next` (next node id, `""` = end of episode).

| kind | Meaning | Additional fields | Progress API |
|---|---|---|---|
| `EV` | Event (popup/presentation + decision) | `deadline` (optional: result key → deadline day) | `Resolve(outcome)` |
| `CH` | Chapter home decision (replenishment · internal army) | `deadline` (optional) | `Resolve(outcome)` |
| `IL` | Organization (home section) | `waitDays` (optional, 0 = no wait) | `ConfirmFormation(wait)` |
| `BT` | Mandatory battle | `mission`, `days`, `onLose` | `BeginMission` → `MissionWon` / `MissionLost` |
| `SK` | Optional battle (a choice of SELECT) | `mission`, `days`, `onLose`, `failDays` | Same. No `next` — when finished, goes to the `next` of the calling SELECT |
| `OP` | Operation (variant branching) | `variants` [{`key`,`mission`,`days`}], `checkDeadline`, `onLose` | `ChooseVariant(key)` → `BeginMission` → … |
| `SELECT` | 1 slot for choosing an SK | `options` (array of SK node ids) | `Select(skId)` / `Select(null)` = skip |
| `REPORT` | Results report (decisions possible) | `deadline` (optional) | `Resolve(outcome)` |

- `onLose`: `"retry"` (default for BT/OP) or `"retryOrFail"` (default for SK). `failDays` (SK, default 0): days used when the failure is confirmed.
- `deadline`: `{ "<result key>": days }`. The outcome of `Resolve(outcome)` must be a key in the table (otherwise rejected). The deadline is `1..dayCap`.
  E1 result keys (EV2 = D10): `follow` (follow 5) · `delay` (delay 6) · `persuadeOk` (persuasion succeeds 7) · `persuadeFail` (persuasion fails 5). The persuasion success/failure judgment (R7: presence of intel.andong) is done by the decision runtime (TC-E/d1) and passed as a key.
- When a mission node with `checkDeadline: true` is won, the deadline is judged (§5 below).

## 4. Validation (load rejection conditions)
Duplicate id · empty id · unknown kind · no `start` · `next` points to a non-existent node (broken transition) · SELECT `options` empty or a node that is not an SK/does not exist · `next` on an SK · mission node without `mission`/`variants` · negative `days`/`waitDays`/`failDays` · duplicate OP variant key · `deadline` value outside `1..dayCap` · node not reachable from the start · cycle in the `next` chain · chain without an end (`""`).

## 5. Day · deadline rules (user confirmed 2026-10-22)
- `DaysUsed` starts at 0. **Only at the moment of a mission victory** add that node's `days` (for OP, the chosen variant's `days`). A retry does not use days.
- The IL wait (D16) adds `waitDays` at the time of `ConfirmFormation(wait: true)`.
- SK failure confirmation uses `failDays` (default 0 — the spec does not mention days, recorded in DECISIONS_PENDING).
- When a `checkDeadline` node is won: `Late = (DeadlineDay > 0 && DaysUsed > DeadlineDay) || DaysUsed > dayCap`. Not a defeat but a penalty display after victory (penalty content is TC-E/d1).
- E1 total days = BT1 1 + number of SKs won + OP1 (assault 2 / siege 4) + wait 1 → equal to the R1 table 6 rows x 4 columns x with/without wait (test `episode` bundle).
- IL screen preview: `ProjectDays(variantKey, wait)` = current DaysUsed + wait · mission days of the remaining chain (SELECT is 0). Show ⚠ with `WouldBeLate(total)`.

## 6. SELECT rules (Q14)
- One SELECT node = one slot. E1 has two slots SEL1 (D05) · SEL2 (D06) → 0~2 SKs.
- The same SK cannot be chosen again (both won and failure-confirmed ones). Skip = go to the next node (the later slot can still choose).

## 7. Retry restoration
- `BeginMission` creates a checkpoint: all variables (`ICampaignVars.SnapshotAll`) + `CampaignState.Supply`.
- `MissionLost(confirmFail: false)` = retry: restore variables · supply to the checkpoint and stay at the same node (days unchanged). For OP the variant choice is also cleared → **the state after the IL2 choice** (the wait days are already included).
- `MissionLost(confirmFail: true)` (SK only): after restoring, record the failure confirmation (`FailedSk`) and go to the `next` of the SELECT.

## 8. Variables (ICampaignVars)
- `ICampaignVars { int Get(key); void Set(key, value); bool Has(key); SnapshotAll(); RestoreAll(list); }`. Current implementation = `FlagsCampaignVars` (on top of `CampaignState.Flags`, missing key = 0). TC-E swaps in a registry.
- Names (spec R9, plan §1.5 — names fixed, value increments [on hold]=0): `favor.hwangson` `favor.minDaejang` `favor.namChamjang` `favor.siwidae` `favor.yiSeok` `rep.local` `morale.troops` `intel.andong` `pow.bandit` `flag.flankPath` `flag.dockIntel` `flag.andongCavalryCut` `flag.andongSurrender` `flag.andongRemnant` `flag.siwidaeDispatched` `flag.namLetter` `unlock.fieldGun`. Integers only. g1 itself writes no variables (checkpoint restoration only).

## 9. Save (CampaignState.Episode, campaign schema v12 — number sorted at merge)
`Episode` (null = outside an episode, old file): `EpisodeId` · `NodeId` (`""` = end) · `SelectNode` (the calling SELECT while an SK is in progress) · `DaysUsed` · `Selected` (order of chosen SKs) · `FailedSk` · `Variant` · `Waited` · `DeadlineDay` (0 = none) · `Late` · `Outcomes` [{`Node`,`Outcome`}] · `Checkpoint` {`Vars` [{`Key`,`Value`}] sorted by key, `Supply`} (only during a mission).
Campaign saves are outside the simulation snapshot so there is no effect on sim FormatVersion · checksum · Audit. Old files (v3~v11) migrate with `Episode = null`.
