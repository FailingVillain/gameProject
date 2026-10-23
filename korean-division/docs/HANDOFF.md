# Work handoff (HANDOFF)

> **State is sourced from ops/STATE.md, queue from ops/QUEUE.md** (merge = ops/MERGE_QUEUE.md · limits = ops/LIMITS.md · work cards = korean-division/tasks/).

> Prep for usage limits · session end. **A new session reads `ops/STATE.md` first** (5 resume steps = `ops/PIPELINE.md`). This file keeps the save procedure + the pre-move record (2026-10-22 04:14).
> When the user says "상황 저장" (or `/save-state`), update **ops/STATE.md** per the procedure below. Resume with "이어서" (or `/resume`).
> Finished items = docs/legacy/HANDOFF_ARCHIVE*.md (Grep only).

## Save procedure (for Claude)
1. SendMessage to each running agent: "Stop at the current step, WIP-commit in a buildable state, update the card status, and report the remaining work · next step briefly in Korean".
2. On receiving reports (or, if you cannot wait, checking via git log · git status · git worktree list) update `ops/STATE.md`: main hash · agents · lock · worktrees · "in progress now / next step". Copy instructions given to agents only by message onto the relevant work card (agent conversations vanish when the session ends). New decision items go to `ops/DECISIONS.md`.
3. Local commit (no push). If game-tools also has remaining changes, WIP-commit it.
4. Report to the user in one line: "save done + how to resume".

## Resume procedure
`ops/PIPELINE.md` §conductor session resume 5 steps (STATE → QUEUE → DECISIONS → PIPELINE → order).

---

## ⏸ Pre-move record (organized 2026-10-22 04:14 — hashes · limits · queues below are old-repo values, current values in ops/)
main `ba4033fb` (2026-10-22 arty3 merge, FormatVersion 50 · Replay 18). Original text of old states (2026-10-21 02:25 · night halt · evening save · overnight queue · 2026-10-18~19 records) → `docs/legacy/HANDOFF_ARCHIVE_2026-10-22.md`.
Agent rules: `tools/agent/AGENT_RULES.md` (the rules file is latest for limits · lock · gate values).
**Today's decisions · full queue = `docs/ROADMAP_SHORT.md` (details · verbatim user quotes = `docs/ROADMAP_LONG.md` [1] — on resume start with SHORT)**.

### WIP branches (kd-develop/kd-…) — new agent on resume
| Branch | Commit | Next |
|---|---|---|
| `arty3` | ✅ merged (2026-10-22, ba4033fb, FV 50 · Replay 18) | deployment delay · hold position · heavy spacing · medium machine gun 32 · field gun indirect + 100 · howitzer splash 3 + adjusted fire · last stand line (ENGINE_NOTES arty3). Interpretations · guessed values in DECISIONS_PENDING |
| `pronemove` | ✅ merged (2026-10-21, FV 48) | include Released companies in the bridgehead count (TacticalAi.Crossing CpBridgehead) → crossing PASS. selftest 1905 OK |
| `units` | ✅ merged (2026-10-21, Snapshot 47) | optional: standard-bearer flag ×1.5 · waving 4 frames (-12/6/32/6°) · Cossack = Russian line + Korean army · raise fur hat + fur texture · Gando volunteers 1920s khaki dress. Sheet docs/art_review/units_fix_cossack_gando.png (awaiting user check) |
| `choke` | `efe5f98` WIP (default off) | included in the large-formation relief bundle (QUEUE no. 2). 上 |

### 📋 2026-10-22 approved queue (user bulk approval — start with new agents as slots free up)
**Limits** (recorded as of 2026-10-22, current = ops/LIMITS.md): agents 20 · 上 6 · Gemini pro 3 + flash 3 · heavy jobs 5 (temporary, burst before weekly reset). Do not start duplicates if in progress.
**In progress (2026-10-22 daytime)**: gemini-import UI merge · campaign WIP review · horse artillery · large-formation relief + choke · artillery/heavy weapons · assault infantry strengthening · Mongol/bandit appearance · building size→railway terrain (excluding armored train) · trench breach (smoke · barbed wire · light mortar · smoke grenade) · fire control/order queue/firing-line display · tactical AI 3B · cavalry dismounted fire/scouts · tutorial 0-1/0-2 · re-render all units · save compat policy · '총기병'→'기병' · new branches N68/N71.
**Approved·not started (order = dependency)**: 1) casualty speed adjustment (after assault infantry + large-formation relief merge) 2) night soak refresh→re-report 9 combat problems 3) investigate trench_assault drop (上) 4) S1~S5 5) control UI bundle (after gemini-import merge; regiment selection key includes player regiment organization design) + UI paper-texture overhaul (priority, after UI bundle) 8) engineers (remaining parts such as track destruction) 9) comms·command structure alpha (上) 10) command convenience: in-mission save·autosave · battle result report · engagement alert/minimap 12) counter-battery · night combat · march fatigue 15b) Korean army tachanka (after horse artillery merge) 16) N79 approved items 17) Major/Minor · officer school/disposition implementation · regiment commander delegation 18) campaign home MVP (approved scope) 19) implement campaign rules 1~15 26) cover clustering · forest shape · terrain subdivision (after building size merge) 29b) sound: replace radio voice · per-faction voice policy 31) harness_local run test · docs/kd folder cleanup · update CLAUDE.md selftest count 30) move repo to C:\works (very last, after all agents end).
**Held**: 6b armored train (only railway terrain proceeds) · 7 river · 11 tactical AI 3C/3D + 10k load (after all else ends) · 28 map editor implementation · 20~22 campaign order linking/chapter 1/ending/North Gando·Czech Legion (later command). 24 Japanese Type-45 cap needs completion check (coordinator checking), 25 Austin done (user).

### 📦 2026-10-22 final migration (user instruction, **very last task**): game-tools · korean-division → `C:/gameProject/` + new .git
- Location (created by the user): `C:/gameProject/korean-division`, `C:/gameProject/game-tools` (directly under C drive — outside OneDrive sync, resolving the earlier sync-burden concern). Worktrees (kd-develop · gt-develop) are recreated under the new location when needed.
- Correction: the FailingVillain repos on online GitHub are plain backups, and the user's requirement is to **delete those repos uploaded to GitHub** (the account is not deleted). Deleting a GitHub repo needs the user's GitHub login (Settings → Danger Zone → Delete this repository) — this PC has no delete tool/token so the user does it themselves and tells us afterward to confirm. Once both local old .git deletion and online deletion finish, old history is unrecoverable (what remains = the new snapshot).
- User decision: both repos get a **new .git (1-commit snapshot, email seungwoo3656 only)**, locally delete the old .git (legacy email).
- **No old email left locally (user 2026-10-22)**: no `-old` archive; right after the new repos are verified, immediately delete old .git · old working folders · temp bundles/backups (including the legacy history copy at C:/works/_git). Before making the snapshot, grep-remove the legacy address string from docs/memory/settings (HANDOFF · DECISIONS_PENDING · ROADMAP_SHORT · AGENT_RULES, etc.). What remains: ~/.claude session records (jsonl) may keep it as command text → delete those records if the user wants (user's part).
- **Compensating for history loss**: just before migration create `docs/legacy/GIT_HISTORY_DIGEST.md` (date · title only of merge commits + major commits, no email · author) — agents check "is it already implemented" by Grepping this file instead of `git log` (avoids the wasted duplicate investigation of N61/N63/N78). BASELINE_CURRENT · SELFTEST_HISTORY · ROADMAP_DONE are kept.
- Preconditions (order): ① all agents ended · merge queue finished (unmerged branches are permanently lost when history is deleted) · render shards ended ② move the working folders whole (including untracked outputs: game-tools/korean-division-image-out · icons · audio-*, game docs/soak · art_review, etc.) ③ git init + single commit at the new location → verify with build · detcheck · selftest ④ delete the old .git only after verification (if permission is needed, the user does it or request allowance) ⑤ update absolute paths: AGENT_RULES (heavy_gate path etc.) · CLAUDE.md · HANDOFF · memory · publish.ps1 · tool scripts (game-tools path in import_kd1920, etc.) · harness_local settings ⑥ re-publish dist ⑦ keep the temporary internal bare (C:/works/_git/*.git) until the new GitHub address is decided, push after receiving the user's address.

### 📧 2026-10-22 legacy email handling (user correction — the earlier migration/deletion plan is **cancelled**)
- User correction: this is not a command to erase the legacy address (retired legacy school email) from git. **No history rewrite · remote repo deletion · account deletion.** One requirement only: **do not push any more to the online git registered with the legacy email (`FailingVillain/korean-division`)** (korean-division only, so it is no longer updated). New commits use `seungwoo3656@gmail.com`.
- Action (2026-10-22): blocked korean-division's `origin` push URL with `DISABLED_legacy_email_no_push` (verifiable with `git remote -v`, fetch URL unchanged). All worktrees share the same setting. To revert, `git remote set-url --push origin <URL>` — only on user command.
- game-tools: the remote push URL is also blocked (2026-10-22). Fresh start = pushed a 1-commit snapshot of the current state to the temporary internal bare repo `C:/works/_git/game-tools.git` (commit `d16163c`, email seungwoo3656 only). When work ends, refresh the snapshot (`git archive HEAD` → new commit) + push to the new GitHub repo URL when the user gives it.

### 🚀 2026-10-22 04:xx 5 speedup proposals approved + limits 18/5
- Limits (recorded then, current = ops/LIMITS.md): agents 18 · 上 5 · heavy jobs 5 (1 render reserved). Top priority = release the merge lock + first merge.
- Applied: ① shorter pre-merge verification (build·detcheck·own suite·snaptest; full selftest once by the AI 3B merger at the end of the queue) ② suites in parallel max 2 per bundle ③ reserve 3 render slots after the merge queue ends ④ split merge of big sim work (artillery·trench breach) ⑤ integrated balance measurement+soak once after the queue ends. The lock holder uses `heavy_gate.ps1 -Priority`.

### 🧊 2026-10-22 tool freeze (user approval)
- agentHub · harness_local · agent templates · rule docs are **frozen** in their current state. New tools/rules only when "how many hours were lost in the last week without this tool" can be answered with concrete numbers.
- Three records for this week (only from existing logs/reports): ① total gate wait time ② number·time of verification repeated at every merge ③ average response time Antigravity plan vs API (`agy_usage.jsonl`). After the merge queue ends, report the procedure improvement "determinism + relevant suite before merge, full suite once at the queue end".
- Only fix: agy_delegate defaults (short flash · web search = direct API, long input · pro · argon = plan).

### ✅ 2026-10-22 additional approval (counter-proposals A/B) — added to the queue
- Recommended "next step": ① **one integrated balance measurement** — after several merges (assault infantry · large-formation relief · artillery · cavalry · ammunition, etc.) finish, A/B/C scenarios seed 8, freeze the result as baseline (`docs/BASELINE_CURRENT.md`) ② **one-page decision summary** — gather N47 · N57 · n50 K/E worsening numbers · large-formation relief defaults · questions raised by agents for the user ③ **campaign vertical slice** — tutorial 0-1→0-2→1-1 (압록강 crossing) through one full cycle "mission select→organize→battle→result→resupply", then expand tabs. First fix the Gemini campaign WIP's **save transaction (temporary state before mission entry → atomic commit when the result is confirmed)** + campaign save `schemaVersion`/per-field default rule.
- **Difficulty structure from the start** (approved): same slider structure as comms realism (easy/normal/hard) also bundles AI accuracy · ammunition · reinforcements — included in the comms structure alpha work.
- **Merge queue** (approved): merge order assault infantry → large-formation relief → artillery → cavalry → trench breach → fire control → AI 3B. The coordinator adjusts the order; on conflict the later work resolves after main merge.
- Other "supplements" · "things to cut" approved. "Nice to add" = **design stage → kept in `docs/wishadd.md`**, re-reported to the user at that turn and decided (no implementation).
- Limits (recorded then, current = ops/LIMITS.md): after the first merge agents 13 · 上 4 (in-progress 16/6 wait to decrease naturally).

### Next order (QUEUE numbers)
- **(2026-10-21 added) First bundle to put the campaign in the game — after the weekly limit reset**: ① tutorial 0-1 (90 vs 30 bandits, retreat at 50% damage → auto win) · 0-2 (about 600 men, several bridges, only 1 outpost visible, 2 events: bandit cavalry 30 · West Gando volunteer army 40 join, destroy the stronghold = win, 50% fallen soldiers = loss) — mission file · map generation · event scripts (中~上) ② campaign order: c1_01 회령 · c1_02 두만강 → keep as WIP, tutorial first ③ campaign home MVP (`docs/CAMPAIGN_MVP_SPEC.md` S1~S5, includes 上). Design = `game-tools/campaign/EPISODES.md` · `docs/CAMPAIGN_HOME_DESIGN.md` · `docs/CHARACTERS.md` · `docs/OFFICER_DOCTRINES.md`.

### Awaiting user decision · review
- Sheets: type3_hmg.png · austin_fix3.png (after render) · progress_bar directly in game (does not show in screenshots).
- Faction addition candidates (enemy/Far Eastern Republic recommended) · whether to move the repo out of OneDrive (bin 3.6 GB · kd-develop 1.8 GB sync burden).
- Existing: N79a keep the '후퇴' button? · marching-fire muzzle flash for other branches · play campaign 1-1/1-2 directly · prone_batch / missing_theme_units / surrender / new_units sheets.

### Decisions · reviews to receive from the user
- (2026-10-19 `retreat`) company retreat threshold at casualty rate 30~40% (retreatStrength 0.60~0.70, data only). Open: `retreatSuppression` (company average suppression is 0.003~0.02 so effectively never triggers), halving after Spent, battalion/regiment ×0.75 — decided together when the user redesigns suppression · morale.
- Full listen to sound effects (`C:/gameProject/game-tools/korean-division-audio-default/index.html`).
- Per-faction overview after batch re-render · before/after slimming comparison review.
- Tactical AI stage 2 results · approve new balance value proposals.

### Remaining N waiting (see the original text moved to the archive)
- N46 · N56 · N56b · N57 · N60 · N62 · N64 · N65 · N66 · N67 · N69 · N70 · N78 · N79 details · S1–S5 — compare with the approved queue · ROADMAP_SHORT and keep only what is worth saving.
