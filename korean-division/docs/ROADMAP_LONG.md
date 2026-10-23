Long-term · on hold · detailed originals. Do not read whole in a session — Grep by item name.

## User approval 2026-10-23

- **(z) Approved items 2026-10-23 (details; short list in ROADMAP_SHORT)** — (1) save/load audit: in-mission save, auto-save before engagement, campaign slots, verify real state vs Snapshot 74 compat policy. (2) settings menu: resolution, volume, key rebinding, default mute. (3) complete the tutorial (only chapter 0 exists). (4) colour-blind support: team colour plus pattern/emblem, combine with the mid-style transition. (5) performance budget card: baseline tick cost at campaign scale (thousands of units), 10,000-unit load test, link to scene-A top-5% tick issue. (7) soak as a mini-gate per merge round (AIPLAN stall in c1_01_hoeryong seed 3 is the motivating case, root-cause bisect running). (8) roadmap diet: split ROADMAP_LONG (88 KB) into done / on hold / active. (9) 0.3/0.4 boundary: 0.4 = AI improvements (T34b-e, T32b/c, difficulty multiplier, surrender expansion), separate from 0.3 balance.
- **(z2) Decisions 2026-10-23** — A4 mid art approved · golden bundle regeneration approved (order G1, NEW-3, NEW-4) · B2 horse-artillery zoom 0.88 · taegukgi finished (user deleted originals, ratio kept; no more taegukgi work) · march-fire MarchMuzzle code fix approved · temporary sounds kept (sound policy: keep temp mapping) · AI 3B pincerShare stays 0.5 default · translation open items held to 0.4 · 0.2.1 dist exe verified present (C:/works/_dist_out/KoreanDivision-0.2.1-2531ec1622.zip).

- **(a) 4 design documents on hold** — prisoners·loot (`spec/prisoners-loot`), auto-pause (`spec/auto-pause`), order cancel (`spec/order-undo`), checkpoint (`spec/mission-checkpoint`). Questions are in each branch's `docs/decisions_pending.d/`. Wounded evacuation stays excluded.
- **(b) Candidates required before the official version**
  - Save compatibility rules (`kd/save-compat`, re-issue after merge N3).
  - AI-vs-AI autoplay aggregation (`tools/autoplay-summary`).
  - Distribution packaging (`tools/package-dist`, done).
- **(c) Large items before official release (unassigned)**: main campaign scenarios · external playtest loop · languages other than English · full tutorial production · achievements·codex · mod support · accessibility.
- **(d) User decisions (pending)**:
  - Game launch muted by default (`run_game.ps1 --mute`).
  - English spelling of 이씨 = Lee.
  - `e2_credit_steal.title` = Merit Stolen.
- **Done (moved from SHORT 2026-10-23)** — verified with `git merge-base --is-ancestor <branch> merge/0.2-final` and ops/MERGE_PROGRESS.md · MERGE_QUEUE.md; evidence hash = 8 chars. Original SHORT wording kept.
  - Formation relaxation + 2-rank line default + choke + N33/N51 (上) — short moves as parallel translation · tolerance 1.5~2 cells · line spacing (details: LONG). Evidence: branch `formation` in the first main snapshot a1a35e6b (FV51; choke · skirmish line off). Still open: choke (NEW-4 kd/new4-choke-queue a7636dad) + remaining jitter, kept in SHORT.
  - Artillery · heavy weapons bundle (上) — deployment delay · hold position · heavy spacing · heavy MG range 32 · howitzer spotted fire · heavy MG last stop line · field gun 70→100 · howitzer splash 3 (details: LONG). Evidence: `arty3` in snapshot a1a35e6b (FV50, Replay18).
  - Tactical AI stage 3 (3B · 3B2 part; mass test stays in SHORT) (Friday 10-23 target, docs/TACTICAL_AI_STAGE3_PLAN.md). Evidence: ai3b2 merge 7d5c124c (B4).
  - Prone firing posture problem (中) — shoots sideways of the rifle · stock clipping, re-render prone frames (details: LONG). Evidence: kd/prone-import 8066152c (shards 1-4, 26 units; merged N3d-3) + kd/pioneer-prone a69e6acf.
  - Mongol · bandit look distinction (下) — del robe · pointed hat / assorted civilian clothes · head cloth (details: LONG). Evidence: `lookfix` in snapshot a1a35e6b + B1 bandit 5 types kd/b1-import f1c8d4bc (user review U03 of the Mongol sheet still pending, kept in SHORT).
  - Building size increase (上) — houses 3×3~3×4, large buildings 4×6, update map · cover · pathfinding (details: LONG). Evidence: wip/bigbldg 8d22ad53 + I4-houses 23cb7ce0.
  - Armored train · freight train (上 design · implementation) — controlled + scripted movement, freight = mission objective, railway terrain (details: LONG). Evidence: rail c7929fea (C1) · D1 b396991e · D2 · D4 dfe9d4f9 · D5 cb850fbb · D6 fixup 7611a0d0; train sprites kd/d-train-sprites-r 4faa3e0a.
  - Include rivers (上) — after the armored train, same framework (details: LONG). Evidence: E2a-d (river-r1..r4), tag post-E2 ac77417b.
  - Engineers (上) — barbed wire cutting, trench building excluded, together with the trench breakthrough bundle (details: LONG). Evidence: breach2 ffc41293 · t32a-pioneer 6f2d1465 · t32d-pioneer-art 5a9e0bbd. The sub-item 'assault troops also take the engineer role' is NOT verified and stays in SHORT.
  - Communication · command structure alpha (上) — division HQ · communication delay, mounted messenger · wire · regiment command post · battalion simple table, difficulty setting (details: LONG). Evidence: comms ① in snapshot a1a35e6b (FV52) · t10-comms-b 578ef29f · t10b-difficulty 60be3990.
  - Campaign home tab (上) — design document first (docs/CAMPAIGN_HOME_DESIGN.md) → approval → implementation (details: LONG). Evidence: design spec/t23-campaign-home 42fa4566 · tabs t19c-home-tabs 9260e367 (merge 63485c05) · read-only tabs T38f (merge 1257e426).
  - Command conveniences ①~⑥ — the done parts only: ①⑤ in-mission autosave · result report (t17-cmd-convenience 0eb92e86) and order queue (orderq2 d1947b05). The rest stays in SHORT.

## Graphics progress status (updated 2026-10-23 ~13:30, facts only)
- Prone-firing re-render (gt-prone-fix, 26 infantry, 4 shards): all 4 shards re-rendered and imported into the kd1920 theme — kd/prone-import 8066152c, merged into merge/0.2-final (N3d-3 bf6bb9e1); in-game sheet 63 approved by the user. Pioneer/scout kept as is (scout has no frame_wide → at the 0.3 unit re-render); the 5 non-res96 units of shard 2 (hunter·japanese·militia·regular·pioneer) → recommended at the 0.3 unit re-render, awaiting confirmation. Full-resolution margin 2 cells passed. Small LOD (@96/@48) approved on a 1-cell margin basis (user 2026-10-23). (Previous: 3/4 re-rendered, 4/4 in progress.)
- B1 bandit 5 types: merged (N3, kd/b1-import f1c8d4bc).
- B2 (kd/b2-deferred-units-2 8340da71, not merged): cavalry-bandit·cavalry-mongol res96 re-render 0 collide hits, edge ok (commit 8340da71); tachanka·horse-artillery wagon.py fixed and re-rendered with 0 collisions but left uncommitted by the old agent (WAKE_REPORT 04:43); user 12:1x: horse-artillery edge-contact 8 cells to be fixed, B2 commit allowed. Current state after the 03:3x cleanup of render processes: unverified. (Previous: horse-artillery 75 clipping cases; heavy-tachanka render failed.)
- Taegukgi (gt/taegukgi-detail 040cfd87, unmerged): Korean standard-bearer re-render + import (kd1920) and render scripts prepared; v2 design approved by the user (41eab625); merges at 0.3 early with the standard-bearer re-render. Open: flag-law text check (face width = vertical), low-LOD readability.
- A3 re-render (kd/a3-rerender): branch tip f979e23f = base commit, no own commit yet (in flight).
- Gando dress (T39g): render·import merged (kd/t39g-gando-import, N3-17 b28a5608).
- PR2 props: P-A (9 types)·P-B·P-C merged (props-shape-fix N3-2 d80c3694). User sheet no issues.
- Bridge rework (kd/bridge-rework d37041a8): merged · approved (user).
- Water foam fix: merged (N3b-3 4d2bd5de).
- Train D: merged (N3-4 4faa3e0a).
- Objective HUD (kd/objective-hud-audit): merged; HUD low-grade UI fixes: ui-log-transparent merged 6a47f3c8 (battle log/top bar/hover text-only, no panel); other low-grade UI fixes: state unverified.
- 0.3 art-style mid re-render: 0.3 promoted 2026-10-23 03:29 (tag v0.3 = 2531ec16); A4 not started (QUEUE_0.3 wave 2) — buildings·terrain·props·decals first, units in the later part (user 2026-10-23).
- Personnel management · regiment commander portraits: regiment commander portraits are not made now, lightly with AI later (user decision 2026-10-23).

# [1] Progress order details (original: old docs/QUEUE_2026-10-21.md, 2026-10-21)

# Queue 2026-10-21 (session after clear) — temporary record before HANDOFF update

In order (movement·posture·artillery code overlaps, so next after the previous is merged):

2. **Formation relaxation** (上) — user "On formations, we go with your proposal":
   short moves (15~20 cells) = parallel translation without slot reassignment · immediate departure · march tolerance 0.5→1.5~2 cells · arrival by zone (exact realignment only on the L command / non-contact rest) · small-error deadband · column/line transition hysteresis.
   + **2-rank line default fix** (user: "let's fix it while improving formations" — 1918-style squad column/skirmish wave, consider wider spacing).
   + choke branch (efe5f98, multi-company chokepoint deadlock) + remaining N33/N51 contact jitter. On/off setting + --collide flinch metric · tactics-obs comparison.
3. **Artillery · heavy weapons** (上):
   - Deployment start 0~3 s delay + deployment time ±15% (PairNoise, stateless).
   - Hold deployment position: re-deploy only for long out of range · far off the firing arc · explicit move; barrel only if within the firing arc; re-deploy cooldown.
   - heavy spacing: minimum 3 cells · 5 if possible (only when taking deployment/emplacement/combat formation positions, excluding march column · narrow roads · trench positions).
   - **Heavy MG range 24 → 32** (user decision; applies when emplaced · consider raising long-range hit).
   - **Field gun = both direct + indirect (howitzer-style)** (user decision; indirect if there is a spotter).
4. armour · 쇼샤 re-render → sheet check (user) → merge. Then trench width 2x (trenchvis).

5. **Building size increase** (user: "Considering the size of the soldiers, let's make buildings a bit bigger. They're too small now") — currently a house = 1 terrain cell (32px) ≈ one soldier's width. **Decision: houses 3×3~3×4 cells, large buildings 4×6 cells** (update cover · sight · pathfinding · hand-made mission maps).
6. **Armored train · freight train** (user: "let's start on this gradually") — no railway terrain (new). Decision: armored train = **both controllable unit + mission-scripted movement** (advance · stop · reverse on the track, field gun + heavy MG, heavy armor, per-car destruction), freight train = **mission objective** (escort · seize · destroy), track destruction = **later** (first pass: track = terrain). 上 design·implementation + 中 art.
8. **Small rivers included (river inclusion)** (user "will you make it?") — after the armored train, designed with the same framework.
9. **Heavy MG last stop line** (user "I agree") — an emplaced heavy MG fires along a designated fire line (fire-control K extension). Included in the artillery·heavy weapons bundle of #3.
10. **Mongol cavalry ↔ bandit look distinction** (user "put it in the progress queue") — Mongol: del robe · primary-color belt · pointed hat / bandit: assorted civilian clothes · head cloth. 下 art fix + sheet.
11. **Japanese infantry default bayonet + draw posture** (user "as proposed / go with the recommendation") — Japanese infantry = BayonetFixed from the start (no halt on charge orders either, no other effect). Blade branch = keep 0.3~0.5 s blade draw, and during that a charge posture instead of an aiming posture (no new art). 中.

13. **Artillery values** (user agreed): field gun center damage 70 → 100, howitzer splash 2.5 → 3. Included in the artillery bundle of #3.
14. **Howitzer spotted fire** (user "I agree. Put it in the queue") — when firing continuously at the same target zone, the spread shrinks per shot (e.g. 9 → 3 cells at the 5th shot), reset when the target changes. Bundle #3 or a separate 上.
15. **Engineers** (user approved) — barbed wire cutting (+ later track destruction). **Trench building excluded**. **Assault troops (assault infantry) can also take the engineer role (barbed wire cutting etc.)**. Together with the trench breakthrough bundle.
18. ~~**Cavalry charge option B**~~ **Cancelled (user 2026-10-21 "cavalry charge also stays as is")** (user "cavalry goes with option B") — charge-hit infantry paralysis chargeStunMin/Max 1.5 → 2.0, cavMeleeMaxTargets 3 → 4. Same bundle as #17. 中.
19. UI label '접적' → **'교전!'** (user) — added as an instruction to the progbar agent ('비접적' label → '대기').
20. **Austin turret overlap (team-color band)** — in progress (armour branch, sheet austin_fix.png, merge after user confirmation).
22. **Combat problems (Gemini analysis, before user decision)**: village_defense 4% (defense placement vs village zone) · city draws 52~74% + 66 zero-shot games · bandit_raid 37% remnant deadlock · artillery_spotters 36% · one-officer company permanent command deficit · cavalry vs machine gun deadlock · tank does not chase infantry (re-check) · mass sniper formation · unit description number mismatch.
23. **Casualty speed tuning** (user 2026-10-21: "I agree with lowering long-range hit rate", "#3 company-level retreat judgment also seems reasonable. Let's do that", "also improve AI-vs-AI surrender") — after the formation relaxation · #17/18 bundle, 中~上:
    - Long-range hit about ×0.5 (accFar family, charge unchanged). + **slightly lower rifle close-range hit** (user "let's slightly lower rifle close-range hit rate too" — proposal: accNear about ×0.85, e.g. 0.35 → 0.30, all rifle family).
    - In-combat feel check by the user directly after all overhaul work is done (user). Target: infantry 200:200 flat 50% losses in 2~3 minutes (currently ~50 s), battalion battle 20~40% losses in 10 minutes.
    - Company-level retreat judgment (not collapse — commander's judgment): **threshold company casualty rate 30~40%** (user 2026-10-21 "let's set the company casualty rate that is the general retreat criterion at about 30~40%. Let it apply differently by faction and commander disposition") or orderly retreat under prolonged suppression. Per-faction defaults + commander disposition modifier.
    - (For now only per-faction defaults. Commander modifier after officer dispositions come in.)
    - **Officer disposition design → long-term roadmap** (user "officer disposition goes to the long-term roadmap"): "Do not simplify to cautious, bold etc. Split into two: school and personality." **School** = the officer's basic tactical doctrine tendency (e.g. German school · Russian school · Pyongyang school etc.) — main influence. **Personality** = cautious · bold etc. — "should not give a big influence" (small modifier). New system (officer data · save state) → propose to the user and get approval for the school list · per-school effects (retreat threshold · assault tendency · dash/firepower weighting etc.) table before implementation. 上.
    - AI-vs-AI surrender improvement (currently 0 cases — conditions: no retreat possible · 10 s suppression · 80% company wipe-out almost never met).
24. **Night soak refresh** (user "if everything else needed tonight is done, proceed") — once today's merge-pending work is done, run `tools/agent/soak.ps1` (heavy job alone), and when done re-report the 9 combat problems of #22 with the new numbers (user "re-report the 9 combat problems").
- armour: 3 types besides Austin OK (user). Austin = turret overlap resolved by diagonal placement (armour 7bf78e3, game-tools d83a638, austin_fix2.png; slight contact in direction e). **Next**: user "remove the team-color circular band on the Austin armored car turret. Instead, put a marker on the rear of the hull." → remove the austin.py turret team-color band + hull-rear team-color marker, re-render · sheet austin_fix3.png → after confirmation merge armour (the 8 uncommitted 쇼샤 files of kd-armour: revert or ignore — same as the merged chauchat). 中.
- Medic = excluded (user: no HP recovery function).

## Roadmap additions (user 2026-10-21)
- **Prone-firing posture problem** (user "The prone-firing posture has far too many problems. What's with shooting the gun sideways, and the stock still clips through in places. Put this on the roadmap") — prone firing frames: muzzle direction mismatched with body direction so it shoots sideways + stock clips through body/ground. Investigate cause (per-direction frame mapping · model pose) → re-render · sheet. 中. (Updated 2026-10-23: prone re-render shards in progress — details in the 'Graphics progress status' section.)
- **Faction Major / Minor split** (user "currently the 5 factions are Major, the others Minor. Put this on the roadmap only, work on it later") — Major = Korea · Japan · Fengtian · **Russian White Army · Red Army** (user correction "bandits are also a Minor faction. Russian White Army and Red Army each count among the 5 major factions"), Minor = bandits · Semyonov · Zhili · Czech Legion · Ungern etc. The Red Army is not a separate faction now (the units branch has a soviet key — check). Long-term.

## Short-term roadmap — non-combat review (user 2026-10-21 "put it on the short-term roadmap. We proceed tomorrow or after the weekly token limit resets")
→ moved to ROADMAP.md (2026-10-21)

→ moved to ROADMAP.md (2026-10-21)

## Campaign · look decisions (user after 2026-10-21 02:30)
- **Korea · Fengtian color distinction**: "keep the gray family but a bit darker, and make Fengtian army's color a bit brighter" — lower Korean uniform gray brightness, brighten Fengtian gray (team color · cap silhouette are Claude's proposal, unconfirmed). Check with a reduced comparison sheet. 下~中.
- **Starting region = Yalu riverside** (user). Plan to move the existing 1-1 회령 · 1-2 두만강 to the front part of the Russian route (needs confirmation). Yalu 1-1 · 1-2 new.
- **Organization management: platoon excluded** ("effectively excluded"), **fire support system mainly at company level**.
- **Public-sentiment value included — applied later**.
- Campaign home tab (user composition + approved additions): recruits·unit management (battalion formation·deployment) · organization management (squad · company …) · logistics (weapon distribution) · light diplomacy (options · civilian aid) · staff (operations staff · regiment commanders · allied faction leaders) · promotion · command authority + **operations map · intelligence (espionage) · unit records · homeland** (user "good"). Player = '김 참장' (name undisclosed), unit = 17th Division '대한'.
- Design document first (per-tab data · screens · connection to existing systems) → user approval → implementation. 上.
- **Regiment selection in combat** (user "let's add a regiment selection function in combat. The hotkey is number keys") — max 9 regiments, **brigade function unused** (brigade · platoon = nominal only). The player-side regiment organization does not exist now (only the tactical AI's TacRegiment). **Conflict**: number keys 1~9 = currently 'select that-number battalion' (CONTROLS.md) → need user confirmation on moving battalion selection to another key. 中~上.
- **Delegate engagement to regiment commander** — roadmap (later, user).
- Korea · Fengtian color comparison sheet = `docs/art_review/kr_ft_colour.png` (art adjustment preview: Korean gray ×0.78 · Fengtian ×1.22, team color not applied) — after user confirmation, actual model color fix · re-render.

## Campaign rule decisions (user 2026-10-21, reflect in the design document at campaign redesign)
1. Resources = rifles · ammo · heavy weapons · horses etc. **No budget**, instead a **supply limit** — units beyond the limit cannot be formed.
2. Loot function = included.
3. Casualties = by default **25% chance to return to the unit as wounded**, treatment needs supplies.
4. Korea · Fengtian color = preview proposal confirmed (Korean gray ×0.78, Fengtian ×1.22) → model color fix · re-render (下~中).
5. Division size max **10,000**.
6. Defeat: **large operation = retry**, **small operation = campaign branch**.
7. Time · season = designed again later.
8. Campaign **save slots** included.
9. Regiment · battalion selection keys = **option 1 confirmed** (user "go with recommended option 1"): number = regiment, Alt+number = battalion; the current Alt+number 'battalion assignment' moves to another key.
10. Training system introduced. 11. Morale · fatigue introduced. 12. Equipment improvement · introduction = merged into the **equipment management tab**. 13. Prisoner handling included. 14. Schedule · event log included. 15. Division status summary (home first screen) included.
- **Recruit supply = homeland support only**, replenished every campaign (operation).
- **Mercenary-style units** (Cossacks · Gando volunteers etc.) = hired in the relevant tab, "mainly consuming funds and weapons" — **funds = diplomacy funds** (user "the budget is diplomacy funds. It does not go into unit management"): used only for diplomacy · mercenary hiring, not for unit management (formation · logistics).

→ moved to ROADMAP.md (2026-10-21)

## 2026-10-21 morning decisions
- **Investigate the cause of the trench_assault drop (soak 100% → 31%)** — put in the work order only (user "just put the cause investigation in the work order"). Suspects: nobreak (trench defenders do not collapse) · N32 trench assault doctrine. The yalu_crossing · ai_bn_assault drops may have the same cause. 上. Basis `docs/legacy/SOAK_REVIEW_2026-10-21.md`.
- **N79a '후퇴' button · console rally = keep** (user "let's keep it").
- Cover density feel = discuss again later (user).
- **Move repository out of OneDrive** (user "let's move it") — after running agents finish: korean-division · kd-develop · game-tools to `C:\works\` (keep relative layout), `git worktree repair`, update absolute paths in AGENT_RULES · HANDOFF · CLAUDE.md · memory.
- Art check list = `game-tools/korean-division-image-out/checklist/CHECKLIST.md` (user request).
- User plans to check directly: progress bar · '교전!' · Japanese bayonet · 7 new units (standard-bearer flag waving) · march-fire muzzle flash.
- UI paper texture mockup = next session.


# [2] Full old roadmap (original: old docs/ROADMAP.md, as of 2026-10-21)

# Korean Division Roadmap

> User directives · confirmed decisions · open items are collected here (same role as wargame's ROADMAP_v0.4). Progress records in `WORKPLAN.md`.
> When a directive arrives, update the relevant section and attach the date. Check against this document before coding.
> Completed details · long implementation records · old value histories: `docs/legacy/ROADMAP_DONE.md` (same section numbers, original kept). This document holds only open items · confirmed rules · on hold.

## 0. Game overview (2026-10-18, details to be discussed later)
- Setting: fictional 1920 alternate history — Russia won the Russo-Japanese War, the Korean Empire escaped annexation and launches the **northern expedition into Manchuria**.
- Structure: StarCraft-style linear + branching from the strategy map.
- One mission 1~2 hours, infantry-centered, thousands of units each for ally · enemy.
- Factions (2026-10-19): korean Korea (default) · japanese Japan · fengtian Fengtian warlord · russian Russian White Army · soviet Russian Red Army (Soviet)

## 0-1. Direction change (2026-10-19)
- **Resource system abolished**: currency · fuel · population (including manpower · steel · supply stock) not used.
- **Production abolished**: fight with only the given units. Existing building systems such as barracks · munitions factory not used.
- **Supply (ammo) new method**: each unit's supply limit is separate (per-weapon `ammo`), engagement consumption same as now, **slow recovery in non-combat situations**, **faster near occupied territory held by allies (cities etc.)**. No faction stock · depot · cost. Open: non-combat judgment (e.g. 10 s without firing · being hit), recovery speed, occupied-territory range · multiplier, vehicle · gun ammo. **[User 2026-10-21: current applied values approved — regenDelay 30 s · 0.15%/s · city 8 cells ×3 · supply wagon 6 cells ×4]**
- ~~**Construction function** (confirmed 2026-10-19, ②+③)~~ **→ By user decision 2026-10-19 all construction removed (below, 1-1 "construction (trench digging) function excluded", cleanup bundle 2 done). Trench = map · mission terrain + auto-occupation.** (old content) **Construction function** (confirmed 2026-10-19, ②+③): remove building · construction system. **Stage 1 = individual infantry trench** (unit state: stationary · after the 'dig trench' command N seconds → cover, released on move). **Stage 2 = trench line** (draw by dragging → infantry dig over time → remains as terrain so anyone gets cover). Trenches · barbed wire can be pre-placed on mission maps.
- **Cover system** (confirmed 2026-10-19): a unit in cover gets **both reduced chance to be hit + reduced suppression received**. First cover source = trench (construction method open), later forest · hill · building rubble etc. added in the same structure. Initial values (balance.json): trench hit **×0.6** (user decision 2026-10-19, both individual trench · trench terrain; previously 0.7) · suppression ×0.5.
  - ✅ Trench line stage 2 · shell crater terrain (2026-10-19) (→ docs/legacy/ROADMAP_DONE.md)
- **Supply recovery draft confirmed** (2026-10-19): non-combat = 10 s without firing · being hit, recovery = 1%/s of limit, ×3 within 8 cells of an ally-held city, vehicles · guns same rule (no fuel).
- Impact: economy · production · buildings · engineers · economy-type standard AI · related tests need cleanup (§4 'unneeded feature cleanup' brought forward). Cities · occupied territory remain as **capture targets (objective · supply acceleration)**.
- ✅ **Implementation done (2026-10-19)**: resources · production · construction · engineers · fuel deleted, given forces + ammo recovery · supply wagon · cover · individual trench · AI reduced, FormatVersion 7. Details and tempo measurement record (completed details: docs/legacy/ROADMAP_DONE.md §0-1).
  - Open · next: forest/hill cover, campaign AI. (✅ `cover.autoDig` = false confirmed 2026-10-19 — individual trench only by command) **[User 2026-10-21: waiting]**

## 1. Current focus (2026-10-18)
**Engagement system + unit graphics.** Campaign layer · unneeded feature cleanup · large-scale command UI later.
### 1-1. Recent updates (2026-10-19)
> Completed bullets (✅ implementation records, 2026-10-19) → `docs/inherited/ROADMAP_ARCHIVE_2026-10.md` §1-1. User decision · open bullets remain below.
- ✅ **River crossing · chokepoint breakthrough + counterattack doctrine (user decision 2026-10-19, branch `crossing`, Snapshot v33)**: decision = the AI must be able to force a crossing at a single bridge · ford (possible with enough force · firepower, not always successful), and a 1.5:1 counterattack must be able to retake a trench line (no weakening of defense overall). Implementation = firepower-based → crossing (company column in succession) → bridgehead → continue (tactics.json `crossing`), old behavior if there are multiple places to cross; mission posture `counter` = reserve commitment · fixed fire → volley assault (tactics.json `counter`). Measurement BATTALION_CAMPAIGN 'crossing · counterattack (crossing)'. **Awaiting user decision**: 2:1 rifle-only crossing threshold, firepower-supported crossing speed vs losses, the fact that the 1.5:1 counterattack is already 8/8 after engage. → decision 2026-10-20: crossing threshold kept (10-19 provisional value confirmed).
- ✅ **gapfill line tidying (user decision 2026-10-19, branch `gapfill`)**: decision = gap filling in combat by command only — ① player "line tidying" (L, replay recorded) ② an officer (including succession commander · acting NCO) commands when the line is badly disordered in combat (3 metrics · hysteresis · re-order interval) ③ soldier veteran skill: recruits go straight in even while firing, veterans engage first (probability · delay table). The rest of N12 unchanged. Snapshot v27 · Replay v10, values balance.json `combat` close*, details ENGINE_NOTES 'gapfill', measurement TACTICS_OBS 'round 10' (win · K/E within noise, 3 candidates — awaiting user decision), test `KD_SELFTEST_ONLY=gapfill`. → decision 2026-10-20: gapfill reference value = replaced by F1.
- ✅ **Decision 4 assault permission · decision 12 battalion role · default NCO organization (user approved 2026-10-19, branch `ai412`)**: offensive assault (engine maneuver group · tactical AI company) = old condition + firepower superiority signal or overwatch confirmation + force ratio, otherwise reserve timer 150 s after engagement (tactics.json `assaultGate` · `assaultMinOdds` · `overwatchSec/Radius/Min` · `assaultFallbackSec`). Officer · march-fire 3-cell auto-assault · defensive counterattack unchanged. A battalion with fewer than 40 rifle troops is a firepower-based, not maneuver battalion; one combat battalion = split (`battalionRoles.minStrength`). Mission infantry squad leader = corporal by default (balance.json `combat.squadLeaderRank`, overridden by mission `squadLeaderRank`). Measurement TACTICS_OBS 'round 9': 97% of permitted assaults after signal · overwatch, assaults 16.8 → 6.6, 40 s retreat 49 → 39%, B maneuver role 10x but B wins 4/5 → 2/5. **Awaiting user decision**: whether to readjust the NCO multipliers (`ncoHesitateMul` · `ncoDecayMul`), B maneuver company approach method (cover hop · flank bypass). Test `KD_SELFTEST_ONLY=ai412`. → decision 2026-10-20: NCO multipliers kept · offensive maneuver company approach = replaced by F1 (wait lifted).
- ✅ **N3 command rank by post · temporary promotion · succession · officer experience (2026-10-19, branch `command-ranks`, Snapshot v23)**: decision = `docs/TACTICAL_AI_STAGE3_PLAN.md` 'command rank · experience decisions'. At organization the post rank (company's first officer captain · second lieutenant · rest second lieutenant, battalion commander major / lieutenant colonel with 3+ companies — deployment stage is formal, in combat temporary promotion up to +3), mission `officerRanks` · `officerXp`. Commander killed → next-ranking officer commands after 5 s handover (temporary promotion), with no officer the NCO acts after 8 s (simple framework only), with nobody the default formation. Officer experience (engagement time · company kills · objectives) → improves judgment frequency · command delay · handover time, recorded in the result file. Values balance.json `officers`. For N4 `CommanderOf(company)` · `CommandLevelOf` · `TacticAllowed`. Details ENGINE_NOTES 'command rank · temporary promotion · succession · officer experience', test `KD_SELFTEST_ONLY=command`. **Next**: N4 tactical formation stage 2 (block at framework switch with `TacticAllowed`). → decision 2026-10-20: officer values kept.
- **User decisions (2026-10-19, approved)**:
  - Kepi band = fixed crimson.
  - Horse towing for howitzers only.
  - Light machine gun firing = kneeling + bipod on sandbag parapet (kept). ✅ Firing direction · art alignment (N2, 2026-10-19): barrel = drawn row direction, light MG 16 directions · wide cell, emplaced aim rotation 90°/s (traverseRate, field-of-fire limit · no re-emplacing). Details ENGINE_NOTES.
  - Scout binoculars posture carbine position kept.
  - Shell crater probability approved: howitzer 35% · heavy howitzer 50% · field gun 20% · infantry gun 8%.
  - Weather minimum sight 8 cells (✅ implemented, `03c7243`).
  - Order delivery delay details approved: assault all · NCO 12 cells · vehicles 0 · squad merge under 6 men.
  - Map size approved as it is.
  - Balance values: re-measure after merging tactical AI stage 2 → new proposal.
  - Operating rule: heavy jobs (build · full test · performance measurement · mass render) up to 2 at once (after the PC forced shutdown 2026-10-18; current pool limit = ops/LIMITS.md).
- **User decisions (2026-10-19, additional approval)**:
  - Officer handover time 5 s (officer) · 8 s (acting NCO) kept.
  - Officer experience does not rise by capturing mission objectives.
  - Officer experience thresholds 60 / 180 / 420 / 900 approved.
  - Battalion adjutant (major) assigned automatically, colonel designated only in missions.
  - Outside ranks set by the campaign, command goes to the highest-experience officer and no promotion (promotion only when succeeding through death).
  - March fire trigger distance = 0.7x effective range.
  - Direct-fire gun point fire = scatter only (no hit judgment) approved.
  - Japanese insignia icon kept.
  - Tactical AI uses frameworks (templates).
  - In progress: "fix art touching cell edges" (game-tools).
- **Done**: armored vehicle ×1.5 size adjustment · Soviet budyonovka higher (anti-tank rifleman keeps low height) · tuning values as JSON (policy: `image/tune/*.json`) · art import gap found (only 7 unit types were in the game) → art import in progress.
- **In progress (parallel agents)**: ④ squad · rank · NCO · battalion · selection (+ officer gap check ≥10 s, 33% sword_wave) · trench line (construction stage 2) + shell crater terrain (approved: crater = cover ×0.8 · movement ×0.85) · weather · time of day (approved) · realistic procedural sound effects (approved, samples `coding/game-tools/korean-division-audio-default`) · all-unit art import · infantry motion round 2 (crawling · dragging the wounded · assault grenade throw · trench firing) · sitting · fix pelvis protrusion in kneeling posture.
- **Decision (2026-10-19)**: barbed wire = impassable obstacle (provisional) · hesitation (reaction delay · hesitation while moving) on hold · cover beside a house ×0.8/×0.8 · trench hit ×0.6 (individual · terrain common), suppression ×0.5 · sight block 2×2 approved · terrain tiles approved · motion round 1 approved.
- **Follow-up: house ruin art**
- **Approved (user 2026-10-21 'approve all') — implementation queue**: mission framework (placement · victory conditions · briefing · result screen), in-game map editor, order delay when no officer · NCO, medic · wounded. Balance value proposals wait for the user's next batch directive (`docs/BALANCE_OBS_2026-10-19.md`, branch `balance-obs-tool`).

- **Order (confirmed 2026-10-19)**: ① engagement round 4 (done) → ② diplomacy removal (✅ 2026-10-19, below "diplomacy removal → static teams") → ③ terrain sim (✅ 2026-10-19, below "terrain · height ✅ implemented") → ④ squad · rank · organization · selection (§2-4c · §2-7) → ⑤ tactical AI stage 1.
- **Last batch (very end of the order)**: body slimming · inner outlines · headband · insignia · faction variants · full re-render.
- ✅ Mission framework (2026-10-19) (→ docs/legacy/ROADMAP_DONE.md)
- **Tactical AI approval (2026-10-19)**: commander → battalion → company, 3 layers. Start from the most basic form. Per-faction + per-mission AI profile JSON (aggressiveness · retreat threshold · trench tendency · artillery/cavalry use · preferred tactics; overridden by mission file). The old AI (leftover from skirmish RTS) is to be replaced.
- ✅ ⑤ Tactical AI stage 1 (2026-10-19) (→ docs/legacy/ROADMAP_DONE.md)
  - **Observation (no balance value change)**: the old BattleAi re-ordered howitzers every 4 s so they never finished deployment (6 s) → howitzer fire 0 shots · annihilated; the tactical AI in the same composition deployed 35~46%, 9~13 shots, guns 4/4 survived. Cavalry: old AI melee 0 times → tactical AI melee 60~97 times (charges 2~3, 18% during a charge). Machine guns: the old AI also auto-emplaced (deployed 46~54%) — the tactical AI emplaced at distance (58~66%), head-to-head fire 3x. Head-to-head (tactical vs old, sides swapped): infantry 200 flat 6/8 wins · terrain 1/4 wins, MG mixed 4/4, cavalry mixed 3/4, howitzer mixed 4/4. A large part of the observation that "artillery · cavalry · machine guns looked weak" (especially artillery · cavalry) seems to be the old AI's fault — value judgment after observing again with the tactical AI.
- ✅ **⑤ Tactical AI stage 2 (2026-10-18, branch `tactical-ai-2` → merged to main)**: user directive, two layers = **faction strategy** (per-directive-group posture · objective · regiment front allocation · main/secondary attack · reserve regiment · reinforcement incorporation · artillery/cavalry assignment) + **regiment tactics** (AI bundle = 2~4 battalions: approach → preparatory fire → assault / defense in depth = main resistance line + counterattack reserve, firepower-based · maneuver roles, artillery extension) below stage 1 battalion · company execution (+ squad alternating dash · defensive outpost · TacMap firing positions). Profile = two sections `strategy` · `tactics`. Per-directive-group by mission force (separating scattered ambush groups), reinforcement incorporation, world battalion number linking, wagon evacuation (auxiliary AI). Design · results · deferred work = `docs/TACTICAL_AI_DESIGN.md`, engine notes `docs/ENGINE_NOTES.md` 'tactical AI stage 2'.
  - **Results (vs stage 1, no balance value change)**: mission 10-seed wins yalu_crossing 10 → 8, village_defense 5 → 8, supply_escort 4 → 5. Tactical vs old battle AI infantry 200:200 12 seeds flat 12 → 12 · terrain 11 → 9, mixed · howitzer preparatory fire 3/3 both (preparatory fire delayed victory 97 → 153 s), attack on trench defenders = stage 1 annihilation → stage 2 stationary draw. AI cost 4008 units avg 0.038 ms/tick (stage 1 0.029, +31%).
  - **Deferred to stage 3**: mission objective linking · infiltration · covering withdrawal · barrage movement · order messenger delay · trench line planning + what remains from verification (terrain infantry battle disadvantage, preparatory-fire wait adjustment, attacking enemies inside trenches, howitzer spotter dependence, escort placement).
  - **Open · next stage proposal**: terrain-map infantry battle weak (better cover selection · hill occupation), reinforcement incorporation, mission objective (other than capture point) linking, refine reserve commitment rule (in a small even battle the reserve is a loss — now committed soon on even contact), check cover score after merging trench line · shell crater. **[User 2026-10-21: on hold]**

- ✅ Diplomacy removal → static teams (2026-10-19) (→ docs/legacy/ROADMAP_DONE.md)
- **Performance policy (user, 2026-10-19)**: multithreading only where needed and safe — terrain chunk baking (already threaded) and terrain sight computation (company-level parallelization only if still slow even in the lightweight version, results merged in fixed order to keep determinism). Sim movement · push-apart · engagement stay single-threaded. Target search · AI parallelization later only if measurement demands.
- ✅ **Bug (2026-10-19, user observation)**: infantry · cavalry flinch when moving (cavalry shows the same → possible common cause: unit render · movement · push-apart) (frame · position appears to jump). Cause candidates: walk frame switching · walk_shoulder 33% switching · flag formation realignment · hesitation (reaction delay) · push-apart · reference point difference during LOD switching — confirm cause after reproducing. 中
- **Bug (2026-10-19, user observation)**: infantry prone-firing (aim_prone / fire_prone / go_prone) motion error — re-check with the batch re-render (slimming) result, then fix posture · gun position (game-tools). 中
- ✅ Large-scale combat collision · formation redesign (2026-10-18) (→ docs/legacy/ROADMAP_DONE.md)
- ✅ Construction (trench digging) function excluded (2026-10-19 decision) (→ docs/legacy/ROADMAP_DONE.md)
- **Bug (2026-10-19 user)**: during assault (march) fire the muzzle flash keeps flashing like a machine gun — because flash is drawn on frames 2 and 4 of the 4 walk_fire frames. → **flash only at the moment of actually firing**: remove the flash from the walk_fire sheet (at game-tools re-render), and the game draws the muzzle flash overlay only on fire events (same method for machine gun · rifle). 中
- **Next (2026-10-19 user)**: replace the **NATO company symbol → each faction's insignia** on the zoomed-out screen (keep considering retaining branch distinction as a small mark next to the insignia). Insignia source = game-tools `image/tools/parts/badges.py` (cap insignia, `323c08c`) + drafts `korean-division-image-out/_drafts/badges/` (badge-korean · japanese · fengtian · russian-white · russian-red, badges_compare.png) → extract as 2D icons (32/64px) and register in the game theme. 中
- ✅ Target scatter wobble 5 · officer first-strike ban (2026-10-19 decision) (→ docs/legacy/ROADMAP_DONE.md)
- **Decision (2026-10-19 user)**: Korean army accent color = **A brass** (`acc_brass`, insignia · collar/cuff · trouser stripe · kepi band/edging; officer gold stripe distinguished with `badge_gold`). Applied at re-render.
- ~~**(on hold)** infantry digging motion~~ → **cancelled** (2026-10-19 no use after construction removal).
- **Decision (2026-10-19)**: infantry body slimming confirmed at the current level (game-tools `456f47e`), no further reduction.
- **Next (2026-10-19 user directive, reduce resemblance to the North Korean army · Red Army)**: change the Korean army base set's **insignia · buttons · red stripes (collar · cuff · trouser stripe) · kepi hat band** from red → **brass or yellow** family (replaces the "fixed crimson" kepi band decision). Apply after sample render, distinguish so as not to overlap officer gold decoration. 中
- (old memo, resolved) engagement tests are muted by default — can be turned on with N since 2026-10-19.

## 2. Engagement overhaul

### 2-2. Damage · hit · range (confirmed part, ✅ implemented — §2-5)
- Rifle damage 40 or more. Standard = **basic infantry HP 100 → dies from 3 rifle hits** (HP adjusted later).
- **Sight · range 2x** (currently rifle 8 → 16, infantry sight sightNear/sightFar also 2x).
- Significantly lower base hit rate + hit rate decrease with distance (currently 3-segment line `Combat2.AccuracyCurve` → extend).

### 2-3. Confirmed (2026-10-18 round 2)
- **Rifle**: damage 40, range 16, **fire interval = deterministic random between [min, max]** (per-weapon `cooldownMin/Max`, rifle default 1.8~2.4 s; hash-based Roll, once per shot).
- **Hit curve**: close range 0.30 → max range 0.04, continuous curve (decreases proportionally to distance squared) — replaces the 3-segment line. Unit hit multiplier 0.7~1.2 kept.
- **2x applied**: all unit sight + direct-fire weapon range. **Howitzer waits** (logic overhaul planned, untouched).
- **Ammo**: rifleman carries 100 rounds, differs by branch (weapon) (weapon `ammo` value).
- **Bazooka → reworked to anti-tank rifle**. Tank kept (modeling new).
- **Suppression** (no morale · rout): affects only hit rate · rate of fire · movement speed. Higher-grade units receive less (unit `suppressResist`).
- **3 map sizes** (adjustable): small ≤1000 units · medium 3000~4000 units · large 10,000+ units. Actual size after measurement later (as is for now).
- **2 kinds of guns** (2026-10-18 round 3): **direct-fire gun** = blocked by obstacles (line-of-sight check, using `GameMap.HasLineOfSight`), **howitzer** = ignores obstacles entirely. All guns must be fixed (deployed) to fire. Deploy/withdraw (load/unload) **auto-switch** according to combat · move orders (extending existing towed gun `DeployPhase`).
- **Bayonet charge · melee** (function + motion): add infantry charge motion · melee engagement motion. Charge = **command**, **automatic** at point-blank. Movement speed **slightly increased** during a charge. Open: melee damage · judgment, auto-trigger distance, increase size, relation to suppression. **[User 2026-10-21: current rules as they are (charge +30% · auto 1 cell · no auto at suppression 400 or more · judgment 0.3 cell · bayonet damage 60/hit 0.6/1.5~2 s). Cavalry charge impact also as it is now (paralysis 1.5 s · cooldown 10~15 s · suppression +300) → QUEUE #18 cavalry option B cancelled]**
- **Motion** (2026-10-18 round 4): about **10 unique motions**, **shared body** (same body · movements, only hat · equipment layers differ). Unique motions can be added by branch. Sheet layout as data (animation list → sheet/cell index, sheet width ≤4096).

### 2-4b. Confirmed (2026-10-18 round 5) — ✅ round 2 implementation (§2-6) · organization ✅ (§2-7)
- ✅ **Bayonet charge**: melee damage 60 (basic infantry dies in 2 hits), attack interval 1.5~2.0 s random. Movement speed +30% during a charge, automatic charge when an enemy is within 2 cells. Implemented values: melee hit rate 0.6 (no unit multiplier), no firing during a charge, gun-crew units do not auto-charge, no auto-charge at buildings (§2-6).
- ✅ **Anti-tank rifle** = current bazooka values · logic as is, only name · graphics replaced (no area damage). Weapon key `at_rifle`, unit name '대전차소총병' (art key `unit.at_infantry` kept).
- ✅ **Hit rate excluded from suppression effect** (round 6): only rate of fire · movement speed. Initial values (balance.json, adjusted later): range 0~1000, per attack rifle +60 · machine gun +150 · barrage (everyone within the blast radius) +400, −80 per second, at 1000 fire interval ×2 · movement −50% (linear), resistance = increase multiplier (conscript 1.0 · regular 0.75 · elite 0.5). Suppression state display needed on screen.
- Unit organization (rounds 7~10 confirmed · keys · cards · formations) → implementation done. Current rules in §2-7 (completed details: docs/legacy/ROADMAP_DONE.md §2-4b).
- On hold (after values decided): 1920 weapon list. (Suppression · bayonet charge · anti-tank rifle · target scatter are round 2 implementation ✅)

### 2-4c. Officers · squads (round 11, 2026-10-18) — officers · ranks ✅ round 2 implementation, fixed squad · NCO · 9-level rank · gap check · sword waving ✅ ④ (2026-10-19)
- ✅ **Officer unit** (confirmed, `officer` — art is an infantry alias): immune to suppression. Armament = pistol, melee = saber (sword). Charges with the sword raised during an assault. Suppression value of nearby soldiers decreases quickly (command range). Several allowed in a small unit. In charge of gap-filling judgment (round 9).
  - (Round 12 confirmed) effects **do not stack**, all officers equal. **HP 200, cost = 10x infantry**. Soldiers within command range: suppression decrease accelerated + **slight rate-of-fire improvement**. Initial values (balance.json, adjustable): command range 8 cells, suppression decrease ×3, fire interval ×0.9. Pistol = damage 40 · range 6 · interval 1.2~1.6 s · hit 0.35→0.05, saber = melee damage 60 · interval 1.2~1.6 s.
  - **Gap check** (round 12): if an officer is nearby, at **intervals of 10 s or more** check the nearby formation and fill gaps. The formation may be a bit loose. (Implemented during the flag formation work)
  - **Sword waving** (round 15): during combat, when an officer does formation tidying (gap check), **33% chance** of the `sword_wave` motion (waving the sword in the air). Art in progress, motion linking together with the gap check.
  - **(Confirmed 2026-10-19, replaces the earlier 4-level rank · round 14, original docs/legacy/ROADMAP_DONE.md §2-4c) Fixed squad · NCO · rank**: squad = fixed organization of up to 12 men (only auto-merge · reorganization). Official NCO (corporal or above) — squad leader effect: hesitation −50%, in-squad suppression decrease ×1.5, does not stack with the officer effect. Enlisted **9 levels** (삼등병 … 원사), kill counts 0 / 2 / 5 / 10 / 18 / 30 / 48 / 75 / 110 (cap 128). **7 officer ranks** (base '장교' + 소위 … 대령), base '장교' = rank 0 (officers equal to each other, role-based differentiation later). Names as per-faction JSON.
  - Graphics: same body + officer hat · pistol · saber. Unique motions = sword-raised charge · sword slash · pistol fire.
- ~~**Auto squad (squad)** (round 12 confirmed)~~ → replaced by fixed squads 2026-10-19 (④ implementation below): temporary organization around an officer in combat, auto-dissolved when combat ends, not shown on screen. Build a formation centered on the officer · spread out · follow the officer if possible. **Max 12 men**, auto-composed · adjusted by situation. Update **every 2~3 s** (staggered per small unit). Player command (flag formation move) takes priority.
- **Order** (round 12): engagement round 1 wrap-up → **suppression · officers** (+ bayonet charge · anti-tank rifle · target scatter) → ✅ organization · flag formation (§2-7) → auto squad · gap check.

> Implementation-complete records (2-4d reaction · 2-4e machine gunner · 2-4f anti-tank rifle · 2-6 round 2 implementation · 2-7 organization implementation) → `docs/inherited/ROADMAP_ARCHIVE_2026-10.md` §2
### 2-4. Open (discussion needed)
- Actual cell counts of the 3 map sizes (draft: small 160² · medium 256² · large 400~512²) and generation method. (2026-10-19: generated map side ×1.5 applied — §2-8. 3-size presets not yet) **[User 2026-10-21: draft approved]**
- Ammo count per branch (machine gun 500 · anti-tank rifle 8 are initial values), suppression value adjustment (initial values §2-6). **[User 2026-10-21: on hold]**
- 1920 weapon list (machine guns · field/mountain guns · mortars · cavalry · armored cars etc.), readjust other weapon damage. **[User 2026-10-21: on hold]**
- ~~Range increase → target search cost for thousands of units~~: mitigated by docs/legacy/ROADMAP_DONE.md §2-4g C (E 10,000 units 44 → 5 ms/tick, docs/legacy/ROADMAP_DONE.md §2-6).

### 2-6. Planned
- **Additional combat overhaul** (user notice, 2026-10-18): the user will direct the contents — based on engagement round 2 results.
- Engagement test mode muted + `--mute` · mute key (add to engagement round 2 batch).
> Implementation-complete records (2-8 range · sight · map ×1.5 bundle · 2-7 cleanup) → `docs/inherited/ROADMAP_ARCHIVE_2026-10.md` §2
## 3. Unit graphics (methodology under discussion)
- Art rules · decisions: `docs/ART_SPEC.md`

- **Batch fix of infantry overlap errors — with opus 5.5** (user-specified 2026-10-19): all cases where weapons · equipment overlap the body, such as the rifle stock appearing stuck in the body. Proceed together with the clipping check tool (`tools/collide.py`, false-positive correction needed).
- ✅ `infantry-japanese` preserved version (game-tools `b1e67e4`, material `jkhaki` + `japanese_cap`; the old light khaki value is not in git so it equals the current trouser khaki — modify only jkhaki if a different value is wanted). **Later: reuse for Japanese army units** — preserve the light khaki WWI uniform (+ Japanese-style cap) as `units/infantry-japanese.json` (round 24).
- **Art queue** (order): the fix above → machine gunner → **anti-tank rifleman** (emplaced type) → **infantry-regular** (Korean name **정규병** (2026-10-19, changed from steel-helmet infantry), **German M1916 Stahlhelm** infantry (changed from Adrian, 2026-10-19; haiku round 1 `9c95eef` unsatisfactory → next session sonnet)) → **Renault FT tank female · male** → **`armored_car`** (vehicles by a separate agent, branch `vehicles` / `coding/game-tools-vehicles`).
- Japanese test infantry cap → WWI-era Type 45 style: haiku attempt (`f929eda`) result unsatisfactory → **redo with sonnet next session** (user 2026-10-19).
- ✅ Infantry backpack color = uniform field gray (`17f53c8`), field gray adjusted (between blue-gray and green-gray), field cap visor shortened 22% (`aee4c4b`), officer saber handle (`a93c8ce`). (_doc wording cleanup done — only the Japanese preserved setting keeps light khaki).
- **Infantry body slimming** (2026-10-19, feedback: overall too chubby, doll-like): reduce torso · arm · leg thickness and shoulder width to be slimmer (body dimensions in tune JSON + shared body so it applies to all infantry · officers · heavy weapons · cavalry riders). After reducing, re-check clipping · edges. 中. **Order: the very last, after all other work is done** (user directive).
- **Infantry motion addition plan** (confirmed 2026-10-19, just before the final batch re-render): round 1 = 4 idle (smoking · adjusting cap · inspecting rifle · sitting rest) + 2 celebration (cheering with rifle raised · salute; on capture · assault victory · mission victory). Round 2 = crawling · dragging the wounded · grenade throw (assault) · trench firing.
- Next candidate: ③ smooth joining (renderer fix).
### Per-faction look (2026-10-19, important)
- **Basic (common) branch bundle · faction-specific branches**: draft confirmed (user 2026-10-21).
- **(2026-10-19) Faction variants on hold**: faction variants of cavalry · artillery · sniper proceed **at the final batch re-render** (user approved).
### Additional branches (2026-10-19)
- **On hold**: combat engineer `infantry-pioneer` (trench line · barbed wire cutting/placing · demolition), mortar crew `heavy-mortar` (2 men, light howitzer-style fire), **medic + wound system** (collapse instead of instant death → death after a set time, medic rescues).
### March fire (assault fire) (confirmed 2026-10-19, 中 — next engagement batch)
- Art: add 2 `walk_fire` frames for the infantry family (中, after the overlap error work is done — touches the same posture file). Shared body so it applies to most infantry branches together.
### Terrain graphics polish (added 2026-10-19, 中 — art line while out)
- **C. Extension** (later, roadmap only): **winter Manchuria theme** (snow · season change), **railway**, water sparkle animation, shell craters (link to barrage marks — barrage mark terrain ✅ 2026-10-19, art is temporary procedural tiles). Art for new terrain (forest · rubble · house · barbed wire · highland) at the terrain · height work.
- **Bug (2026-10-19 user)**: some units such as the anti-tank rifleman **lack maximum zoom (LOD0) art** — zoomed in the art disappears or only the small LOD shows. Cause candidates: page not created in art memory reduction (LodAtlas lazy load · 30 s drop · 256MB budget · small LOD substitute while upload pending), importer LOD selection (1/2 · 1/4 rule — units with different cell sizes), `UsedAnimNames` omission, faction variant key. 中 (N8)
- **Next (2026-10-19 user)**: zoomed-out display — **only units assigned to a unit (company) shown with the faction insignia** grouped (infantry · all other branches). **Units not assigned to a unit return to team-color (blue etc.) dots**. 下 (N9, together with N8)
- **Later (2026-10-19 user)**: attach a **per-branch unit distinction mark** (infantry · machine gun · artillery · cavalry etc.) under the insignia.
- ✅ Artillery re-aiming · range 40/80/120 · 5 map size levels (2026-10-19) (→ docs/legacy/ROADMAP_DONE.md)
- **Next**: overload test per map size (generation · pathfinding · minimap · fog · AI threat map · save · tick cost). 中 (N13)
## UI check (user 2026-10-20, later)
- LOS display key B: check line-of-sight display · legend readability (hard to see on green background). **[User 2026-10-21: review later]**
- Fire control key K: check cycling (hold fire → return fire only → free). **[User 2026-10-21: review later]**
- F1 zone border: looks larger than the company (radius 5) → consider shrinking. **[User 2026-10-21: review later]**
- Progress ring: check color · size. **[User 2026-10-21: review later]**
- Major visual overhaul planned (user 2026-10-20) — cover prop art v1 (cover_props_v1.png) redone at the overhaul.
- Trench breakthrough bundle N59/N60/N65 · order queue N74 = later.

## 4. Later (on hold)
- **Story · campaign (long-term, registered 2026-10-19)**: start after receiving the user's directive. Order: ① worldbuilding · timeline document (alternate-history branch point Russo-Japanese War → 1920 Manchurian northern expedition) ② campaign structure (branching linear — remember: branching linear) · chapter composition · mission list draft ③ between-mission screen (unit management · promotion (enlisted/officer campaign promotion) · replenishment · unlock) design ④ result file → force inheritance link ⑤ first chapter (3~5 missions) prototype ⑥ characters · briefing style. Existing foundation: mission framework · result file (kills · promotion candidates · officer experience) · static teams · weather/time of day · tactical AI profiles. 上 (design) / 中 (mission production).
- **2026-10-19 direction summary**: map · tactics (tactical AI, map force placement · victory conditions, map editing) **slowly, later**. **Artillery branch on hold** (needs subdivision). **Agreed on sight blocking (forest · hill · building block sight)** — together with terrain addition. **Terrain addition**: forest · hill · building rubble · barbed wire · civilian houses etc. + **highland / lowland** (review proposal in conversation record, awaiting user decision). **Add supply wagon unit** (transport unit that extends the supply recovery radius to the front — together with the new supply of the direction-change major work). **Grenade method counter-proposal** (conversation record, awaiting decision). **Max zoom 4.0**. Fengtian infantry rifle is slung diagonally. **Start cavalry art** (上, `game-tools-u4`).
- **Map editor (2026-10-19, user decision)**: roadmap only, later (not implemented now).
- **Cavalry charge impact (charging)** (2026-10-19): ~~first-hit damage 300 + 0.4~0.6 s paralysis after 2+ s of charging~~ → **revised (user 2026-10-19)**: no damage bonus (normal saber damage), impact event = paralysis 0.6 s · per-cavalry cooldown 10~15 s · suppression +300 on enemy infantry within 2 cells of the impact point. Charge speed ×1.8 kept. **No melee attack on tanks · armored cars** (bayonet · sword · large blade · cavalry all; grenades are not melee).
- Cavalry charge impact damage removed (user 2026-10-19): no damage bonus (`chargeImpact` deleted), normal saber damage. Charge impact event kept — per-cavalry cooldown 10~15 s (`chargeCooldownMin/Max`), paralysis after impact fixed 0.6 s (`chargeStunMin = Max = 0.6`), suppression +300 on enemy infantry within 2 cells of the impact point (`chargeSuppress` · `chargeSuppressRange`). Speed ×1.8 kept (branch cav-nocharge, save FormatVersion 24)
- **Cavalry unit** (added 2026-10-19): default = **carbine cavalry** (carbine fire), movement speed = **1.5x or more** infantry, charge speed = **1.8x**, stamina limit = **3x** infantry. Melee charge is **saber**. **Toggle: charge only instead of firing** can be set. **Dismount function not implemented**. Art = horse + rider (new model — 上/中 decided at start), after the stamina system (§4 stamina · quick step).
- **Stamina · quick step** (2026-10-19, later): stamina decreases slowly on charge → charge speed drops with the decrease, movement speed also slows at low stamina. **Patrol function removed**, instead **quick step** = normally uses stamina, faster than walking and slower than a charge. Open: maximum · consumption/recovery speed · speed curve, hotkey, art (quick step walking motion). **[Already implemented 2026-10-19: staminaMax 100 · charge consumption 8 · quick step 3 · recovery moving 2/standing 5 · Q key · HUD stamina bar. Remaining = only the quick step walking motion art]** **[2026-10-21 quick step walking motion started]**
- Campaign layer: mission scripts (objectives · triggers · reinforcements · dialogue), strategy map branching, save progress between missions. **Branch · equipment unlock = mission reward** (research tab deleted 2026-10-19, no research system).
- **Terrain subdivision (later)**: terrain kinds · variants · color diversification (2026-10-19 directive).
- Unneeded feature cleanup (16-player battle AI etc.) — right after campaign structure is confirmed. (Diplomacy ✅ deleted 2026-10-19 · economy ✅ deleted)
- Command UI for thousands of units (company · battalion-level organization orders).
- ✅ Stamina · quick step (2026-10-19) (→ docs/legacy/ROADMAP_DONE.md)
- **Formation function fix** — many problems currently (user, 2026-10-18). Later start by organizing the problem list.

### Balance decisions (user approved 2026-10-19, LETHALITY Part B) ✅ applied
- ✅ Balance decision LETHALITY Part B applied (2026-10-19) (→ docs/legacy/ROADMAP_DONE.md)

- [Decision 2026-10-19] Machine gun balance: 1 Maxim ≈ 10 riflemen (1v10 about 40%, 1v5 win, 1v20 loss), 1 light machine gun ≈ 5 riflemen (1v5 about 40~50%). lmg/hmg accNear 0.3 · accFar 0.12, Maxim hp 340 · suppressResist 0.3, machine gunner hp 160 · suppressResist 0.4. The Guard is a special branch — keep as is (user).
- 2026-10-19 bundle 3 done: charge fire toggle · enlisted in-combat promotion removed (StartKills) · non-combat attack-ground · leftover cleanup (ENGINE_NOTES bundle 3). Zoomed-out insignia: if the player has a lone unit without a company it shows as a dot (missions use companies so insignia) — unresolved/user confirmation needed. **[User 2026-10-21: leave as dots as now — resolved]**
- [Decision 2026-10-19] Light machine gun burst (lmg): 10~20 round burst (unit Id · tick hash deterministic) then 1~2 s pause, cooldown within the burst. On losing the target the burst breaks and pauses, a new target starts a new burst. Values in balance.json lmg (burstMin/Max, burstPauseMin/Max). Heavy MG (hmg) fires continuously. Compensating for burst uptime of about 66%: lmg cooldown 0.2→0.13, accNear 0.3→0.38, accFar 0.12→0.16. 1v3/1v5/1v8 (tactical AI, flat, 8 seeds) = 8/8 · 4/8 · 1/8 (`KD_SELFTEST_ONLY=lmgtable`). FormatVersion 24 (CMB2 BurstLeft per unit).
- [Decision 2026-10-19] Company retreat = casualty rate 30~40% (retreatStrength 0.60~0.70), 2026-10-19. Aggressive 0.60~0.63 (40% casualties) · cautious 0.67~0.70 (30%). `retreatSuppression` · Spent halving · battalion/regiment ×0.75 unchanged (decided together at suppression · morale redesign).
- ✅ Artillery AI 6 items · contribution · pursuit · retreat fire (2026-10-19 decision) (→ docs/legacy/ROADMAP_DONE.md)
- **(2026-10-19 user decision, `rankui`)** Officer promotion cap = lieutenant colonel (`officers.maxPromotionRank`). Zoomed-out company symbol = faction insignia + a **branch image** below it (NATO symbols only replace the old theme). The user will provide the per-faction rank · unit name document separately (no inventing names).
- ✅ Engagement distance engageRangeFrac 0.7 (2026-10-19 decision) (→ docs/legacy/ROADMAP_DONE.md)
- [Decision 2026-10-19] Active use of trenches · cover + defense terrain priority when defending (trench line → house · forest strongpoints, machine gun position, retreat to the next trench line, after reserve cover) ✅ applied (N25) — occupy-position order · find cover when hit · attack via cover (light version, full version in stage 3) · defense front cap `defendSpacing` (front without trenches). Adjustment candidates (cover search range · trench multiplier · trenchPerTile · attack position distance) in BATTALION_CAMPAIGN 'engage', awaiting user decision. Next: suppression redesign (close-impact suppression · accuracy · prone). → decision 2026-10-20: N25 adjustment candidates = at F1/F6.
- [Decision 2026-10-19] Tactical AI stage 3 (STAGE3_REVIEW §7 decisions 1~13) — **3A ✅ applied (s3a)**: cooperation metrics first → win rate vs stage 2 (1) · 3A in parallel with suppression redesign (2) · save format bumped once = v31 (3) · flank bypass ≤ front ×2 · 120 s (4) · withdrawal allowed + per-mission values `factions[].aiWithdrawAt` · `aiCommitment` (5) · faction personality on hold = plan bias 0 (7) · S1 instead of infiltration (9) · mission objective linking included (10) · 8-seed 3+3+2 measurement (12). All 3A behaviors behind profile keys (`strategy.manoeuvre` etc.), stage 2 comparison = overlay `korean+stage2`. Results · remaining work = STAGE3_REVIEW '3A results'. Next 3B.
- [Decision 2026-10-19] Forest combat (user, branch forest) ✅ applied: ① if the enemy is not visible in the forest or it is deadlocked the scout party actively enters the forest to find them (`scoutAi.forestProbe*`, hit-cover first) ② forest concealment slightly weakened (`terrain.forestSeeDepth` 3 · `forestHit` 0.8 · `forestSuppress` 0.85), defensive advantage kept (column ambush ×1.9, forest defense 2:1 exchange 2.3). Measurement · open items: BATTALION_CAMPAIGN 'forest combat'. Open question: depth 3 made column ambush defense too strong (restoring depth 2 applies only to search) — user judgment. → decision 2026-10-20: forest sight depth 3 kept.
- [Decision 2026-10-19] Cavalry command (user): ① cavalry officer `officer_cavalry` (commands the cavalry company · command range · officer rules as is, pistol · saber on horseback, art = copy of cavalry model + officer equipment, Korea · Fengtian) ② cavalry squad NCO default organization (after officer loss N3 acting NCO, overridden by mission `squadLeaderRank`) ③ cavalry company missions use a cavalry officer instead of an infantry officer ④ cavalry flank intensity (`flankOddsMul` etc.) kept open. ✅ Applied (branch cavcmd — during AI cavalry charges the officer follows 10 cells behind `cavalry.officerBack`, TACTICS_OBS round 13). Open: cavalry flank intensity values (round 12).
- [Decision 2026-10-19] N44 combat adjustment bundle (tweak1) ✅ applied: line tidying excludes firepower groups · deferred during a charge, hit cover for defending units only, trench 0.5, engageFrac 0.6, observer exposure avoidance, field gun arrival deployment, infantry gun 70 · 1.5 · 250 — remaining: separate the cause of the Korean charge retreat (TACTICS_OBS round 14).
- [Decision 2026-10-19] Sniper · rain (user): ① sniper rifle fire interval 4.5 (4~5) → **8 (7~9)**, range · hit kept — 30 vs 30 exchange ratio 6.8 → 1.45 (end of EXCHANGE_TABLE). Mass formation (25%+) still dominant → formation cap or range adjustment for later review. ② rain disadvantage for the attacker = not intended → `weather.states.rain.move` 0.9 → 1.0 (end of TACTICS_OBS). ✅ Applied (branch snrain, values only).
- ✅ Bandits · common units · rifle variants · Korean army infantry stages (2026-10-19 decision) (→ docs/legacy/ROADMAP_DONE.md)
- [Resolved] Whether to turn on the 3 switches of suppression redesign 2 (supp): prone `proneAt` 0.35 (S1 55→37%) · collapse `breakChance` 0.15 (trench attack sim 1/5→3/5, crossing AI failure) · cover recovery `coverDecayMul` 1.25 — ENGINE_NOTES 'suppression redesign 2'. — Applied in round 2 (supp2): prone 0.35 · cover recovery ×1.25 on, collapse breakChance 0 (user 'I'd rather not put in the collapse rule'). User 2026-10-21 'go with the recommendation' = keep the current state.

## Armored vehicles (user confirmed 2026-10-20, timing undecided)
- Mark V heavy tank **male type only** (2 six-pounders + machine gun) — Korean Empire · Russian White Army few · Red Army some (captured). For trench breakthrough, about 7.4 km/h, crew 8.
- Lanchester armored car (general purpose) — Korean Empire · Russian White Army · Red Army. None for Japan · Fengtian · bandits.
- Fengtian army **improvised armored car** (converted truck) new. Bandit gangs have no armored vehicles. Basis · values = docs/ISSUES_2026-10-20.md (Gemini web search lead, re-confirm before implementation).
- (On hold) remaining N33/N51 jitter (rubbing of the rear rank on contact, 上) — re-propose around 2026-10-22.
- (User 2026-10-21 0:00) **Prone soldiers are dragged by the formation move** — soldiers who went prone from suppression slide along with the formation in the prone posture. Solutions: ① soldiers prone from suppression = pinned (do not follow the formation move order, stand up and rejoin when suppression drops) ② when an explicit order arrives with high suppression → crawl (speed ×0.3, crawl anim) / with low suppression → stand up (about 0.8 s) then walk ③ forbid walking movement in the prone posture (posture · anim · speed consistency test). 中.
- (User 2026-10-21 0:00) **Milling about at narrow chokepoints (bridge · ford · alley · forest path)** — solutions: ① automatic column when a chokepoint is detected (one line in squad order) ② chokepoint passing order queue (arrival order · per squad, next when the front clears) ③ turn off formation slot reassignment · gap check while passing, rebuild the formation at the rally point on the far side ④ weaken collision push-apart inside the chokepoint. Reuse the existing crossing doctrine (company column in succession) and the F2 'normal formation when blocked' path. 中~上.
- (User 2026-10-21 0:00) **Assault fire (march fire · shooting while walking) muzzle flash position is off** — presumed that the muzzle anchor (anchors.json muzzle) of the walking fire frames deviates from the real muzzle. Solution: in game-tools recompute the muzzle anchor of the march fire anim (march_fire etc.) per direction · frame (reuse the muzzle measurement method used for prone firing) → re-render · import, on the game side check that the anim reads the fire anchor on the correct frame. 下~中 (art).

## 2026-10-21 decisions (short · long term) — original · details in `docs/QUEUE_2026-10-21.md`
- **Short-term (tomorrow / after the weekly limit resets)**: non-combat review 6 bundles (art·UI confirmation pending · controls/info · settings/distribution · performance · sound · campaign foundation) · prone-firing posture (shoots sideways · stock clips through) · Gando volunteer corps dress · Mongol/bandit look distinction · Austin team-color marker on hull rear. (Updated 2026-10-23: for prone-firing · Gando dress see the 'Graphics progress status' section above.)
- **Progress queue**: formation relaxation · artillery/heavy weapons bundle · casualty speed · building size (houses 3×3~3×4, large buildings 4×6) · armored train (controlled + scripted)/freight train (mission objective) · river inclusion · engineers (trench building excluded, assault troops also take the engineer role) · Type 3 heavy machine gun.
- **Long-term**: officer disposition (school = German · Russian · Pyongyang etc. doctrine / personality = cautious · bold etc. small modifier) · faction Major (Korea · Japan · Fengtian · Russian White Army · Red Army) / Minor (bandits · others) · Czech Legion · light mortar range 36 (trench breakthrough bundle).
- **On hold · cancelled**: rifle grenadier infantry = not included · signal-flare defensive barrage = cancelled · medic = excluded (no HP recovery) · troop transport trucks/wagons = not included.
- **Campaign character candidates (roadmap, user 2026-10-21)**: 3~4 regiment commanders · homeland quartermaster general · Wonsubu inspector · NCO representative. Confirmed characters in `docs/CAMPAIGN_HOME_DESIGN.md` §8. Unit name · lineage function is also roadmap.
- **Light map editor** (roadmap, user 2026-10-21): the user modifies an auto-generated map a little — terrain painting · trenches/barbed wire · objectives · start positions · save. Per-mission placement dictated by the user → generation parameters.
- **Map cover props · forest shape** (roadmap, user 2026-10-21): cover props are placed too scattered → mainly clusters (fence lines · stone walls · village perimeter · forest edge · in front of trenches). Forests are rectangular so odd → irregular outline (noise boundary · sparse trees at the edge). Together with the map bundle (building size · railway terrain · generation parameters).
- **Standard-bearer flag ratio 1:1 → 3:2** (longer horizontally, user 2026-10-21 "make it look a bit longer") — roadmap, **proceed at the next session (weekly limit reset)** (user). 下.
- **Graphics roadmap = `game-tools/GRAPHICS_ROADMAP.md`** (2026-10-21: genre RTT · cells 48x60/96x120/192x240 · keep pixel-art style (Blender abandoned) · prone-firing redesign 08 confirmed · all-unit re-render bundle · terrain/building/lighting unification later).

## Long-term — ending routes (user concept 2026-10-21)
- **A**: northern expedition succeeds, Gojong keeps the throne.
- **B**: northern expedition succeeds, Sunjong enthroned by coup.
- **C**: northern expedition of limited success. Officers who failed the coup, centered on the protagonist, set up the **Northern Gando Military Government** in Manchuria → effectively warlords.
- **D**: retreat during the northern expedition, **Republic of Korea (대한 공화국)** established (DLC expected).
- Two coup concepts: (1) resentment of an emperor gone senile who only shouts for war → demand Sunjong's enthronement → **capital coup** (linked to B success / C failure). (2) **Military's own coup** → suppress leftist forces then establish the Republic of Korea (linked to D).
- Branch variable candidates (Claude, to review at design): northern expedition progress · relations with imperial grandson Yi Tae / General Min / Yi Seok · homeland trust · public sentiment · officer school · casualties (`maxCasualtyFrac` forcing history).
- **5 officer schools · enemy faction dispositions = `docs/OFFICER_DOCTRINES.md`** (school table approved · research link · enemy officers have no school but 2~3 faction dispositions (Fengtian 4, bandits = share Fengtian lowest) — enemy disposition table approved 2026-10-21).

## Command conveniences · systems (user 2026-10-21)
- **Approved (progress queue)**: ① in-mission save + autosave just before a big engagement (bundled with campaign save slots) ② game speed control (0.5~3x) + orders while paused (check the current implementation level first) ③ unit list panel (regiment › battalion › company tree, shows strength · ammo · state, click to select) ④ order queue Shift+order (N74 brought forward) ⑤ battle results report screen (= campaign post-operation screen) ⑥ branch codex (unit description · portrait · values; shared with info tab · tutorial) ⑧ color-blind-friendly team colors (color + pattern/mark).
- **Deeper discussion later**: ⑦ engagement alerts · minimap display.
- **Introduce later**: ⑨ stronger regiment commander delegation (specify defense · attack · delay attitude, linked to school) ⑩ photo mode.

## Combat system additions (user approved 2026-10-21)
- Alpha: **① division HQ · communication delay**. After alpha: ② counter-battery fire ③ night combat reinforcement (flares · night sight/hit) ④ march fatigue in combat.
- **Communication · command structure (user design)**:
  - A. **Reintroduce messengers — mounted messengers** (travel on horseback).
  - B. Add **wire telephone · wire Morse**. **Wireless Morse = research later**.
  - C. Add **regiment command posts** — each command post is connected to the division HQ by messenger · wire. **No battalion command post; the battalion commander sets up a simple command table at a suitable location** (user proposal, Claude agreed).
  - **Orders given to a regiment commander are carried out only if actually delivered by wire · messenger.** Regiment commander → battalion commander re-delivery. The player can also give orders directly to the battalion commander.
  - Details · other communication means = Claude review (conversation below · awaiting decision). **[User 2026-10-21: adopted]**
- **Communication confirmed (user 2026-10-21, roadmap only)**: round 1 = mounted messenger (can be captured → order loss/exposure) · wire telephone · Morse (shelling · enemy can cut, signalman repairs) · **signal flare** (short pre-arranged signal) · **carrier pigeon** (forward unit → HQ report) · regiment command post · battalion simple command table · **'in transit' indicator** · **enemy AI follows the same rules**. Handwritten notes · signal lamps = round 2, wireless Morse = research.
- **Convenience principle (user)**: commanding at the single-unit level stays, and the player directly commanding a company is also possible — a convenience area. (Control-feel countermeasures = conversation below · Claude proposal: communication realism option etc.)
- **Communication rules confirmed (user 2026-10-21)**: communication realism = **difficulty setting (easy · normal · hard)** area. **Wire orders within 1~3 s**. **Messengers fairly fast — normally arrive within 10~20 s**. **Always notify when an order is lost**. **Easy = wire is never cut**. **Direct company orders = delivered instantly** (control-feel area — the user's judgment that at thousands-of-troops scale only important units will be commanded directly and the rest delegated). Regiment commander orders = carried out only when delivered by wire · messenger (above decision kept).

## Short-term roadmap — non-combat review (user 2026-10-21 "put it on the short-term roadmap. We proceed tomorrow or after the weekly token limit resets") — moved from QUEUE
To be moved to ROADMAP.md (when there is no merge lock). Proceed after tomorrow.
1. Art · UI awaiting confirmation: prone_batch.png (E view rifle short) · missing_theme_units.png · surrender_pose.png · new_units sheet · whether to keep the unasked '후퇴' button/console rally of N79a · march-fire flash on other branches · in-game check of progress bar · ROADMAP 'UI check' bundle. **[User 2026-10-21: keep the '후퇴' button (N79a) · applying march-fire flash to other branches = approved. The rest awaiting confirmation]**
2. Controls · info: command card cell overflow (when the infantry card is full, rally · barrage type drop out) · hotkey cleanup (CONTROLS.md + in-game help) · large battlefield info (engagement alerts · minimap display).
3. Settings · distribution: settings menu (resolution · window mode · volume · key rebinding) · save format compatibility policy (rose to 46 today — decide compatible/reject before release) · crash log.
4. Performance: campaign scale (1~2 hours · thousands of men) frame · sim measurement (cost may rise with today's added features), 10,000 load test.
5. Sound: replace the temporary radio voice · per-faction voice/battle-cry policy (Gemini TTS rejected 10-20).
6. Campaign foundation (story · progression method being conceived by the user — organize after the concept, do not touch first): briefing · mission selection · between-mission organization/replenishment · branch conditions · tutorial chapter 0.

## Additional roadmap (moved from QUEUE 2026-10-21)
- Rifle grenadier (rifle grenade) — **on hold · not included** (user "a bit negative", many heavy weapons — Claude agreed). If squad howitzer-style firepower is lacking after the light mortar, reconsider only as assault infantry equipment.
- Czech Legion — later addition plan only (user).
- Cancelled: signal-flare defensive barrage (SOS barrage) — cancelled by the user.
- Troop transport trucks/wagons = not included (user "I think it's not good. It's complicated", Claude agreed).
- Memo: **light mortar range 36** (user decision) — not in the game yet, apply when making the trench breakthrough bundle (smoke · barbed wire cutting · light mortar · smoke grenade).
- Proposal pending: measure casualty cause ratios (artillery share) in the soak.
- **UI overhaul — paper texture** (user "the current UI format is too web-game-like", "I prefer a paper texture") — paper texture direction confirmed (user 2026-10-21 'decided'; CAMPAIGN_HOME_DESIGN 'as proposed').
- **Campaign home design draft = `docs/CAMPAIGN_HOME_DESIGN.md`** (user "put it in the campaign design document as this proposal"; 3-layer resources = manpower + supplies + equipment stock (including shell stock), per-caliber ammo only at the campaign level, UI paper texture "as proposed"). Research function approved (user 2026-10-21). **[User 2026-10-21: one paper-texture mockup first (docs/art_review/ui_paper_mock.png) → actual implementation after the 10-23 weekly limit reset]**
- Campaign design v0.2 reflected (user): auto organization·replenishment · supply preview · staff advice · difficulty approved, unit name·lineage = roadmap, division 10,000 = organization standard · recruit pool 7,000, no approved authorized strength (adjusted by recruit dispatch volume), research = consumes funds + supplies · linked to officer school, **imperial grandson deputy division commander · supreme commander absurdity axis** (imperial son → imperial grandson setting change). `docs/CAMPAIGN_HOME_DESIGN.md` §6 · §7.
- **Add railway terrain** (user "let's add railway terrain") — new TerrainType (track) + generation · pathfinding · sight · render, prerequisite for the armored train (chapter 2) · freight train. In the same map bundle as the building size increase. 上.
  - Northern Gando volunteer army = later (Tumen WIP mission · Northern Gando/Primorye route) — on hold.

## Long-term — UI
- **Paper texture UI · commander's desk** (user 2026-10-21 "the commander's desk isn't bad. For now leave it at this, and later go on to improving the operations map image and fixing details") — mockup `docs/art_review/ui_paper_mock.png` (33eed0f) approved at the current level. Later: operations map image improvement · detail fixes (morale/fatigue line spacing, replenish button clipped etc.). Actual implementation after 10-23.


## Graphics improvement candidates (Fable review 2026-10-22)
Of the 24 scoring 5 or below, 11 improvement candidates were re-checked against the original sheets (2026-10-22). Details: C:/works/_notes/ART_IMPROVEMENT_CANDIDATES.md, queue table: section 'E. Graphics improvement candidates' of ops/BACKLOG_2026-10-22.md.
- Orderable (now): standard-bearer prone flag unification (together with the prone-firing re-render shards), wooden bridge width·entry, scorch decal variety, branch symbol @16, refugee·volunteer identification marker.
- After merge: imperial grandson portrait ears (gt/vip-portraits), wounded dragging s series (kd/t39h2).
- On hold: old dark UI battle 1024 overlap (after the paper UI v2 adoption decision), quickstep officer.
- Discard candidates: missing_theme_units 3 cavalry types (replaced by B2·c1e1 cavalry).
- The remaining 13 are marked one line each in BACKLOG section E: 9 in progress · 2 replaced · decision pending (ruin question 52).

## Personnel management · roster operation (user 2026-10-22)
- New personnel management function — waiting officer pool·manual assignment/replacement/dismissal·automatic assignment result report (T61e candidate, 0.3)
- Automatic assignment·company merge UI (T61d, 0.3)
- Officer·equipment handling on merge (Q-F, 0.3)
- Artillery delay link to artillery barrel (#31, 0.3, together with communication alpha piece ③·gun block mapping)
- (Updated 2026-10-23, user decision) Regiment commander portraits are not made now. Lightly with AI later.

## Art style direction — shift to the middle value (user 2026-10-22, 0.3 in progress)
- Decision (user night of 2026-10-22): change the art style direction to the 'middle value' (keep the pixel·cartoon basis + slightly more realistic) but proceed at 0.3. Among the options **middle value** adopted (vs keeping current), timing **0.3**. Put it on the roadmap.
- Confirmed (2026-10-22): mid profile — see ART_SPEC for values (`docs/ART_SPEC.md` 'Confirmed mid profile (2026-10-22)').
- Direction definition:
  - Keep: pixel·voxel render, black outlines, team-color ring, high-contrast silhouette, limited palette.
  - Raise: earth·cloth-centered desaturated colors, uniform dirt·wear expression, natural body proportions, texture detail (tree rings·bark·roof tiles), historically accurate dress·architecture.
  - Avoid: photographic look, excessive texture, primary-color-centered palette, cartoonish exaggeration.
- Additional decision (user night of 2026-10-22): unit re-render (mid art style applied) is redone at 0.3. For now (current stage) only the building·terrain family is re-rendered in mid, and this re-render proceeds in the 0.3 initial version (first work right after 0.3 starts, unit re-render after that).
- Application order (latest, additional decision night of 2026-10-22):
  ① 0.3 initial: building·terrain family mid re-render (houses·ruins·terrain tiles etc.). **Confirmed (2026-10-22): includes all buildings·terrain tiles·props·decals (excluding vehicles·trains → unit side, 0.3 late). Time: buildings 1~2 hours · terrain within 1 hour · props within 30 minutes · decals small, total about 3~4 hours of rendering.**
  ② 0.3 late: unit re-render — color post-processing first, full re-render only for the units that need it (93 unit types, full re-render about 7~12 hours wall clock).
  ③ (Done) Comparison sheet approved — sheet `51_style_house_compare.png`, user confirmed mid.
- (Updated 2026-10-23, user) The application order after promotion stays: buildings·terrain·props·decals first, units in the later part.
- (Previous proposal, before the additional decision of 2026-10-22 — kept for reference)
  ② Application order:
    - Apply houses·props·terrain·decals·vehicles together first (about 3~4 hours of rendering).
    - For the 93 unit types do 'color post-processing (per layer, team color protected)' first, and fully re-render only the units that need it. Full re-render averages 24 min per unit·median 16.5 min × 93 types = serial 25~37 hours, with 3~4 render lanes in parallel about 7~12 hours — this method is under review.
  ③ Prerequisite engineering:
    - Convert voxel.MATERIALS ramp at render time + post-processing hook right after make_frame (base layer only; per-material effects if the material mask is exposed).
    - Prop hook (small).
    - Terrain·decal post-processing (medium).
    - Vehicle hook (small~medium).
  - Applicability memo (per asset group): houses can be applied directly with `make_building.py --style mid`. Props·vehicles need a hook added (small~medium), terrain·decals need post-processing work (medium). Units use the ramp conversion + make_frame post-processing hook method, applying per-material effects separately once the material mask is exposed.
  - (Previous application order, before the mid confirmation of 2026-10-22 — kept for reference)
  1. At the start of 0.3 render 1 basic infantry + 1 prop + 1 house as a 3-level 'current / middle / more realistic' comparison sheet → the user selects·confirms a level.
  2. Record the art style rules in `docs/ART_SPEC.md` (section 'Art style direction' below).
  3. For existing units, apply the saturation·wear expression together at the 'final batch re-render' already on the roadmap (no separate re-render needed).
  4. Visibility protection test: after lowering the tone, check with a sheet that the team color·silhouette on terrain is still distinguishable per zoom level.
- Constraints:
  - Until 0.3 new assets keep the current direction.
  - Keep resolution·LOD·memory budget (256MB).
  - Do not go photographic.

## Game music (user 2026-10-22, roadmap only — not started)
- Status: **not started.** Timing = undecided (start when the user gives a candidate song list).
- Concept: use old military songs·folk songs with rewritten lyrics. Must be commercially usable.
- Confirmed constraints·research memo:
  - Claude cannot generate music audio directly. What can be done within the pipeline = writing scores (MIDI·ABC·LilyPond·MusicXML)·lyrics rewriting·arrangements, and rendering OGG with commercially usable synthesis (own numpy chiptune synthesis or FluidSynth + a soundfont with verified license).
  - Human-like vocals are not possible. Provide instrumental + lyrics text, a human singer recommended for vocals.
  - AI music models: MusicGen weights are non-commercial (CC-BY-NC) → excluded. Stable Audio Open has a conditional license → latest terms need checking.
- Copyright checklist:
  - Whether 70 years have passed since the song author's death (by Korean standard authors who died before 1955).
  - Whether the original lyrics copyright has expired.
  - Specific recordings·arrangements must not be used → arrange directly from the score.
  - Rewritten lyrics are derivative works, so the original lyrics must have expired.
  - Rights limits of purely AI-generated output.
- Proposed order:
  1. Receive the candidate song list → check copyright expiry per song (web search via Gemini).
  2. A test song with 1 song confirmed expired (chiptune march arrangement + rewritten lyrics + OGG loop for the game).
  3. Expand.
- Decisions needed: per scene (battle BGM · campaign home · victory/defeat) · mood.
- Prior questions: which scenes·moods, candidate song list.

## 0.4 — In-game sound effects overhaul (user 2026-10-22)
- Item: overhaul of in-game sound effects (firing·explosions·marching·order responses·UI·ambient sounds·battle cries etc.).
- Survey tasks (before 0.4 starts):
  - Current sound effect list·sources·licenses.
  - Volume balance.
  - Overlap·priority.
  - Distance attenuation.
  - Link to the ROADMAP battle-cry policy R01 'provisional B (re-report)' item (basis: `docs/SOUND_POLICY_2026-10-22.md`, branch sfxnew; decision record in `ops/DECISIONS.md` R01).
- Detailed design when 0.4 starts.

## 0.4 — Regiment commander delegation UI improvement (user 2026-10-23)
- The T57c delegation UI is confirmed at the current level (separate panel·4 attitude buttons·dotted map marker), and improvements (integration with the battalion list·hotkeys·"in transit" marker·target zone designation·auto-release conditions etc.) are handled in 0.4.
