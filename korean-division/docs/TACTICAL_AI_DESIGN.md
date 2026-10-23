# Tactical AI Design (stage 2 complete, 2026-10-18)

Built on top of stage 1 (`docs/ENGINE_NOTES.md` 'tactical AI stage 1'). Rules unchanged: **commands only** (`World` public API), **only what is visible via `Fog.CanSee`**, **deterministic** (list · Id order, hash selection, tick-function staggering, state stored in snapshots).

## 1. Survey — game AI techniques

| Technique | Content | Adopted |
|---|---|---|
| Influence map (influence / threat / cover map) | Accumulate threat · cover · highland · line-of-fire scores on a coarse grid to choose positions (Total War · CoH family) | **Adopted** — 8-cell `TacMap`. The terrain layer is lazily computed per cell (terrain hash is the key → pure function, no storage needed), the threat layer is computed at query time from the visible-enemy grid |
| Utility score | Choose among candidates (position · target · commitment timing) by a weighted sum | **Adopted** — position selection · main effort selection · reserve commitment. Weights are in the profile JSON |
| HTN / behavior tree | Decompose tasks into subtasks / condition tree | **Partially adopted** — small state machines per layer (regiment phase approach → preparatory fire → assault, company state). No HTN planner (cost · storage burden at 10,000 units, few task kinds) |
| Blackboard | Memory shared between layers | **Adopted** — `TacFaction` · `TacDirective` (recently seen enemies · observed targets · main effort · fire targets) = the stored blackboard |
| Staggered budget | Think on different ticks per layer · unit | **Kept** — strategy 8 calls (≈2 s), regiment · battalion 4 calls (≈1 s, offset from each other by 2 calls), company 2 calls (≈0.5 s). Expensive queries (line of fire) only for the top few candidates |

## 2. Survey — 1910~20s infantry doctrine (to imitate)

- **Fire and movement**: while one element suppresses the enemy as a **base of fire** (prone fire, machine-gun centered), another element **maneuvers** (bounds from cover to cover). Squad-level alternating bounds (standard after 1918).
- **Machine gun = base of fire**: emplace first on highland · good lines of fire, prefer enfilade, suppress before the infantry assault.
- **Artillery preparatory fire → lifting / creeping**: preparatory fire on the enemy front before an assault; as friendly infantry gets close, shift fire to the enemy rear (reserves · second line).
- **Infiltration** (1918 German stormtroopers): bypass strong points, penetrate weak points (suppressed · disordered places) → weight suppression · disorder in target selection.
- **Defense in depth**: outpost (contact · delay then withdraw) → main resistance line (cover · highland) → counterattack reserve (counterattack at the breakthrough point).
- **Flank security**: cavalry · small units on open flanks, refused flank when the flank is empty.
- **Cavalry**: screen (reconnaissance to the front · flanks), pursuit (of a broken enemy), no frontal charge against machine guns.
- **Covering withdrawal**: some cover by fire while the rest withdraw.

## 3. Direction decisions

- **Performance**: at 10,000 units AI cost should be proportional to "scanning visible enemies + number of companies". Terrain scores are lazily cached per cell, line of fire (`HasFiringLine`) only for the top 4 candidates, threat via a visible-enemy grid query. Measurement target: within +50% of stage 1 at 16×1000 · 2×5000.
- **Determinism**: caches are pure functions keyed by (map, `TerrainHash`) → even if not stored they match the loading side. Phase · role · time are stored in state.
- **Data-driven**: profile = two sections `strategy` (faction strategy) · `tactics` (regiment and below tactics). Per faction + per mission (`aiProfile`, per-force directive groups).
- **Range · sight are read from the definitions** (balance overhaul: rifle 24 · machine gun 24 · carbine 22 · sight 32 · guns 32/50/70 · map ×1.5). Distance constants are expressed as "multiples of rifle range".

## 4. Layer structure (user directive 2026-10-19: two main layers)

```
Faction strategy (Strategy, ≈2 s)            ← stage 1 commander
  └ Directive group (TacDirective: per-mission-force posture · area)
      └ Regiment tactics (Regiment, ≈1 s)    ← new layer (AI bundle: 2~4 battalions)
          └ Battalion execution (Battalion, ≈1 s) ← stage 1 battalion (linked to world battalion number)
              └ Company execution (Company, ≈0.5 s) ← stage 1 company + squad alternating bounds · outposts
```

| Stage 1 | Stage 2 |
|---|---|
| Commander: posture · objective · battalion front · reserve battalion · artillery/cavalry | **Faction strategy**: per-directive-group posture · objective · **regiment** front allocation · main/secondary attack · reserve regiment · reinforcement assignment · artillery assignment · cavalry (screen · flank · pursuit) · posture change |
| (none) | **Regiment tactics**: phase (approach · preparatory fire · assault / defense in depth), battalion role (base of fire · maneuver · main resistance line · counterattack reserve), reserve battalion, artillery target · lifting, flank security |
| Battalion: company front · reserve company · firepower/armor/wagon | **Battalion execution**: company placement by role, reserve company (even battle = committed as flank extension), machine gun base-of-fire position, main resistance line depth selection (TacMap) |
| Company: firing position · gunner · bound · assault · trench · retreat | **Company execution**: + squad alternating bounds (fire squad ↔ maneuver squad), defensive outpost squad (withdraws on contact), TacMap firing position |

## 5. Data

**Profile** (`assets/data/ai_profiles.json`, each section overrides on top of default):
- `strategy`: `posture` · `aggressiveness` (speed of posture change · reserve commitment) · `reserve` · `defendPatience` · `artillery` · `cavalry` · `scheme` (frontal · flank · defensive) · `mainEffort` (main effort front concentration 0~1) · `regimentSize` (battalions per regiment 2~4) · `counterattackOdds` (defense→counterattack superiority multiplier: based on visible enemies within range ×1.25 in front of the position, 2 consecutive times → held 20 s, not done for mission directive groups) · `culminate` (offensive limit: if the group strength ratio is below this + the enemy is superior, switch to defense).
- `tactics`: `aggressiveness` · `retreatStrength` · `retreatSuppression` · `trench` · `chargeRange` · `chargeSuppression` · `chargeOdds` · `engageFrac` · `fireAndMovement` (squad alternating bounds 0/1) · `boundLength` (bound length, multiple of rifle range) · `prepSeconds` (preparatory fire) · `liftDistance` (lifting distance, cells — based on rifle range 16, actual = value × range/16) · `outposts` (outposts 0/1) · `outpostDistance` · `depth` (main resistance line→reserve, multiple of range) · `coverWeight` · `highWeight` · `losWeight` · `threatWeight`.

**Directive group** `IAiDirectable.SetDirective(faction, group, posture, area, radius, forces, units)`: group 0 = faction default (existing `SetDirective`). The mission groups by force (`forces[]` · `reinforcements[]`) with (posture = force posture ?? faction posture, area = aiArea ?? area), and **hold / defend without an area are separate per force** (fixes the problem where scattered ambush parties were merged into one line). Reinforcement units, when they appear, find their group via `Mission.ForceUnits` and are incorporated.

## 6. Per-layer decision loops

**Faction strategy (8 calls)**: ① incorporate new units (company formation · battalion · regiment assignment — the group's weakest regiment, a new regiment when full) ② perception per group (recently seen enemies · observed targets) ③ posture (directive > map > profile, defense waiting → attack, offensive limit → defense, counterattack when superior — for a group the mission ordered to defend · hold, no faction-level counterattack but it holds the position, and local counterattacks are handled by the regiment's reserve battalion) ④ objective (seen enemy > area > capture facility > enemy start) ⑤ regiment front allocation (concentrate to match the enemy front width, main effort = strongest regiment · narrow front, secondary = fixing) ⑥ reserve regiment (3 or more) commitment = collapse · below 70% · opportunity (enemy suppressed) ⑦ artillery → supports the main effort regiment, cavalry → screen (before contact) / open flank of the main effort / assault opportunity ⑧ reconnaissance → observer (no order after the autonomous reconnaissance flag is merged).

**Regiment tactics (4 calls)**: attack = approach (line up to range+margin) → on contact roles (2 or more battalions: the battalion with the better firing position score = base of fire, the rest = maneuver (diagonally toward the flank if scheme is flank); with 1, split by company within the battalion) → **preparatory fire** (if there is supporting artillery, for `prepSeconds` the maneuver element waits at the firing position and artillery is forced to attack the enemy front) → **assault** (bounds · assault permitted; if a friendly maneuver company is within `liftDistance` of the fire target or within 2x of it during an assault (bayonet · assault bound) then **lift**: target deeper enemies (15+ cells toward the enemy), if none the engine auto-targets). Defense = front battalion main resistance line (TacMap depth selection) + reserve battalion = counterattack (breakthrough · front battalion retreat · when nearby enemies are suppressed, or when an attack within range in front of the main resistance line is broken = visible enemies of the whole regiment front are 0.8x the reserve or below, or suppression ≥ `chargeSuppression` and 1.5x or below → attack that point for 25 s then return).

**Battalion (4 calls, offset from the regiment by 2 calls)**: stage 1 + roles: base of fire = stationary fire only at engagement distance (no bounds · assault), maneuver = bounds · assault, main resistance line = depth selection, attached machine guns go to highland · good lines of fire in front of the base-of-fire battalion (own battalion if none). Reserve company: if superior, held until the decisive moment (enemy suppression ≥ assault criterion), **if even · inferior, committed immediately on contact as a flank extension (outside on the FlankSign side)**.

**Company (2 calls)**: stage 1 + if an attack move stops at the end of range and more than half are firing for over 9 s, treat it as a fire line (engagement) (before that keep approaching to the engagement-distance firing position), advancing the fire line after fire superiority is **squad alternating bounds** (even/odd squads: one fires · one moves to a cover cell by `boundLength`, swap on arrival · after 6 s), during defense an **outpost squad** (first squad to cover `outpostDistance` ahead, withdraws if the enemy is within 80% of range or suppressed), firing position = TacMap score (cover · highland · line of fire · threat · distance). Advancing the fire line only when cover improves or the enemy is suppressed · 1.6x superior, but if it fired for a long time (advance wait ×2) at enemies in trenches · cover, it approaches. A defending-task company retreats only to the next position (`depth` × range × 0.5, max 18 × K).

**Mission auxiliary (`mission-aux`)**: unarmed groups (wagons) do not go more than 6 cells ahead of the escort center, and if a visible armed enemy is within 20 cells they evade behind friendly armed units on the side opposite the threat.

## 7. Terrain map `TacMap`

Cell 8×8. Terrain layer (lazily computed per cell, key `TerrainHash`): average cover (`TerrainCover` forest · rubble · trench · shell crater · beside houses), highland ratio, passable ratio, highest-cover tile within the cell. Threat layer (at query): number of cells of the visible-enemy grid × distance attenuation (enemy range radius). `PickPosition(reference point, enemy, desired distance, lateral tolerance, weights)` = candidate cell scores → line-of-fire check of the top 4 → tile-level `BestCover` of the best cell. Shared cache (thread-static, shared across factions).

## 8. Debug · tests

- `KD_TAC_DBG=1` (engagement sim): every 5 s regiment phase · battalion role · company state (`--mission-run` 30 s, interval via `KD_TAC_DBG_SEC=N`). At the end table of the sim: cumulative preparatory fire · lifting · alternating bounds · outposts · counterattacks · reinforcement incorporation + an `AI 비용` [AI cost] line (average · maximum ms/tick of AI thinking during engagement, `StressTest.TimedAi` shell).
- `--stress ai ai=tactical` (16×1000, 2×5000) ms/tick.
- Self test `KD_SELFTEST_ONLY=tactical`: layer mapping (faction → regiment → battalion → company), directive groups (ambush party separation), reinforcement incorporation, world battalion number linking, TacMap (cover · highland cell selection · purity), fire and movement (squad alternation), preparatory fire → lifting, defense in depth (outpost withdrawal · counterattack), determinism · save resume · fog.
- Observation: `--balance sim` stage 1 vs stage 2 (terrain infantry · mixed · artillery preparation), 3 missions headless.

## 9. Stage roadmap

- **Stage 2 ✅ (completed 2026-10-18)**: the layer structure above, TacMap, fire and movement, defense in depth, directive groups, reinforcement incorporation, world battalion linking, preparatory fire · lifting, the single contact point of the firing arc `InGunArc`, mission-auxiliary wagon evacuation.
  - **Results (vs stage 1 c542a3f)** — mission headless 10-seed wins (same AI on both sides): yalu_crossing 10 → 8, village_defense 5 → 8, supply_escort 4 → 5 (stage 2 just before WIP 3 · 6 · 3).
    Tactical vs old battle AI (infantry 200:200, 12 seeds): flat 12 → 12, terrain 11 → 9. Mixed (infantry 150 · heavy machine gun 12 · cavalry 20, terrain 3 seeds): 3/3 both (average surviving infantry 103 → 101).
    Howitzer 4 + reconnaissance 4 preparatory fire (3 seeds): 3/3 both, but stage 2 is slower due to the preparatory fire wait (victory 97 s → 153 s, survivors 116 → 78). Attack of gunners in trenches (infantry 150 · machine gun 6) 300 s: stage 1 annihilated in 3 games, stage 2 drew in 3 games (attacker 21~54 survivors — halts instead of reckless charges).
  - **AI cost**: `--stress ai players=4 n=1000 ticks=450` (4008 units) thinking avg 0.038 ms/tick (p95 0.18, max 1.21) — stage 1 0.029 (+31%, within target +50% · 1.5 ms). Engagement sim 400 units avg 0.01~0.015 ms/tick (same in both stages).
- **Stage 3 (deferred)**: mission objective linking (capture · escort · annihilation priority), infiltration (breakthrough axis through weak cells), covering withdrawal (staged retreat covered by a fire squad), artillery barrage movement (creeping — once a fire-point command exists), command delay · communication (messenger delay for regiment orders), trench line construction planning (connecting trenches when defense lasts long).
  What remains from the stage 2 verification: terrain infantry battle is weaker than stage 1 (12 games 9 wins vs 11 wins — presumed loss from the split where the base-of-fire company only fires), the preparatory fire phase delays the attack (adjust the wait time against artillery effect), lifting is rare (little reaches the assault), attacking enemies in trenches ends stationary (needs infiltration · smoke · concentrated assault), howitzers do not fire without an observer (reconnaissance · officer) (the engagement sim needs reconnaissance), supply_escort win rate half (wagon evacuation works but escort placement comes with mission objective linking).
- **Stage 4+**: campaign regiments (linked to the world regiment organization), difficulty (perception delay · error rate), learned weight adjustment (offline based on observation tables), reconnaissance information memory (map of last known positions in the fog).
