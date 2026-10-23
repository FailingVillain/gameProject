# Save compatibility policy (recommended defaults) - 2026-10-22

Status: **recommendation, auto-applied** (proceed per this table until the user decides otherwise). The answer to the ROADMAP_LONG item 'Settings · Distribution - save format compatibility policy'.
Basis code: main's Snapshot.cs / Replay.cs / CampaignState.cs + the end of the pending-merge chain (t42d-schedule-log, t48a-commander-data). Campaign schema is v1 on main, v7 at the chain end (T48a makes it v8). sim FormatVersion is 52 on main, 65~66 at post-E per the merge plan; replay is a separate number rising from 19.

## 1. Save kinds and current behavior (on version mismatch)
| Kind | Location / version | Current behavior |
|---|---|---|
| Campaign progress + slots 1~9 + 2 autosaves (right before an operation, right after settlement) | `%LOCALAPPDATA%/KoreanDivision/campaign/` campaign.json, slotN.json, auto_*.json. `Version` (schema) | Old files **open as-is, missing fields get defaults** (additive-only rule). Version 0/negative, corrupt, **newer version are rejected** (Korean reason). Corrupt original kept as .bak |
| In-mission save F6/F7 (quicksave) + autosave every 10 min (autosave_1..3) | `saves/*.kds`, header Format(FormatVersion) + game version | Rejected if the format differs (**older or newer**): "이 저장은 이전 버전에서 만들어져 열 수 없습니다 (저장 형식 v52 · 현재 v66)" (this save was made by an earlier version and cannot be opened). Game version mismatch alone is only a warning. Has checksum and truncation checks |
| Replay | json, `Format` (separate number, 19) | Rejected if the number differs (distinct old/new messages). Reason shown even if the start-save fragment is corrupt |
| Settings | `settings.json` (UserSettings) | Read failure silently falls back to defaults. Unknown keys ignored |
| Map / mission JSON (bundled with game) | data/ | Shipped with the game - no version issue |
| User-made map · commander JSON (map editor T33, regiment commander T48a) | user folder | No current policy (T48a enters the campaign as schema v8) |

selftest `savecompat` already checks that old/new/truncated/empty/arbitrary-byte files return a reason without crashing.

## 2. Options
A. Reject all old ones (message only). Easy to implement, loses player progress.
B. Keep conversion (migration) code. Nearly free for campaign files since they are additive-only. For in-battle snapshots, the sim FV has changed 20+ times so migration is practically impossible (determinism: a single differing field breaks replay).
C. Keep previous-version (N-1) read code. Not recommended due to upkeep cost.

## 3. Recommended defaults (by kind)
| Kind | Policy | Reason |
|---|---|---|
| Campaign (including slots·autosaves) | **Always migrate (B)**: fields are additive-only, a new field's C# default = the old file's value. If a field deletion · meaning change is unavoidable, put migration code in the same card (no rejection). Reject newer versions. Keep `campaign.json.v<old>.bak` once before the first migrated save | The only asset holding days of progress |
| In-mission save (F6/F7, auto) | **Reject old format (A)** + clear Korean message. Keep the format number in the header. In the list screen, show unopenable saves greyed out + reason and do not delete them | Missions are 1~2 hours so loss is small, and migration is impossible |
| Replay | **Reject old format (A)**. Optional format-number tail in the filename (`_r19`), already in the header | Same reason + replay only makes sense with the same code |
| Settings | **Lenient**: ignore unknown keys, defaults for missing keys, only the key gets default when a value is odd. If the whole file is unreadable, keep .bak then use defaults | The game must not fail to start because of settings |
| Bundled data | No compatibility (one body with the game) | |
| User maps · commanders | Same rule as campaign: additive-only, ignore unknown keys, defaults for missing values. **Skip** unreadable files and show the reason in the list (game continues) | Files the user wrote by hand |

Timing: **before release (0.x) the sim FV may be raised freely** (mission-save·replay rejection allowed). **After release too**, the campaign migration rule stays. After release, a sim FV change adds one patch-note line that 'in-progress mission saves are cut off'. We do not freeze FV to a final number once before release (not a separate decision).

## 4. User-visible messages (Korean, keep existing SnapMsg wording + add below)
- Mission save old format (existing): "이 저장은 이전 버전에서 만들어져 열 수 없습니다 (저장 형식 v{0} · 현재 v{1})". **Added line**: "진행 중이던 미션만 이어할 수 없습니다. 캠페인 진행은 그대로 안전합니다." (only the in-progress mission cannot continue; campaign progress is safe)
- New format (existing): "이 저장은 더 새로운 버전에서 만들어져 열 수 없습니다 …". Add: "게임을 최신 버전으로 올려 주세요." (update the game)
- Replay (existing wording) + "이 리플레이는 v{0} 게임에서만 볼 수 있습니다." (this replay can only be viewed on a v{0} game)
- Campaign migrated (new, once only): "캠페인 저장을 새 버전으로 옮겼습니다. 원본은 campaign.json.v{n}.bak 로 보관했습니다." (campaign save moved to the new version; original kept as .bak)
- Campaign newer (existing) + "파일은 지우지 않고 그대로 둡니다." (file is left, not deleted)
- User map/commander skipped (new): "'{파일}' 을 읽을 수 없어 건너뜁니다: {사유}" (cannot read '{file}', skipping: {reason}).

## 5. Implementation cards (new cards go to ops/QUEUE.md one line each)
| Card | Content | Size | Status |
|---|---|---|---|
| (exists) | Campaign additive rule + migration v3~v7, SaveCompatSelfTest, corrupt original .bak, slot·autosave | - | Already in T01/T19a~d/T42a~d (pending-merge group F) |
| **T49a** | Policy application 1: added message line, keep .bak once on migration, greyed-out + reason for unopenable saves in the list screen | S | new queued |
| **T49b** | Policy application 2: lenient settings read (per-key defaults) + user map/commander skip message + settings·commander cases in the savecompat test | S | new queued (after T48a) |
| **T49c** | Release gate: keep fixed old-file samples of campaign v1..current as test data, they must open to pass (prevents migration breakage) + add 'add a sample when raising the schema' to the card checklist | S | new queued |

No open questions (proceed with all defaults). If the user wants to change something, 'reject old campaign files too' is the only branch.
