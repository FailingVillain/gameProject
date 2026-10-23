# Controls (CONTROLS)

> Basis: current state of the code (HEAD `40bd155`, 2026-10-21). Input handling `src/Game.cs` (`HandleMouse` · `HandleKeyboard` · `HandleGlobalKeys` · `ScrollCamera`), command definitions `src/Core/Commands.cs`, formation cycling `src/UI/FormCycle.cs`, text `src/UI/Strings.cs`, console `src/Game.cs` (`RunConsoleCommand` · `FormationConsole`), launch arguments `Program.cs`.
> Items not confirmed are marked **(needs checking)**. If the document and the code differ, the code is right.

---

## 1. Camera

| Input | Action |
|---|---|
| Arrow keys (← ↑ → ↓) | Move the view |
| Screen edge | When the window has focus and the cursor is inside the window, scroll in that direction within 8px of the edge (`Config.EdgeScrollMargin`) |
| Middle button drag | Drag the view |
| Mouse wheel | Zoom in · out centered on the cursor position |
| `+` / `-` (including numpad) | Zoom in · out about the screen center |
| Minimap click · drag | Move the camera to that position (if a command is pending, command that minimap point) |
| `Space` | Move the camera to the center of the selected unit |
| Tap a regiment · battalion number twice (within 0.35 s) | Move the camera to that regiment · battalion (see §2) |
| `[` / `]` | Lower / raise the game speed one step (play: 0.5 · 1 · 1.5 · 2 · 3x) |
| `F1` | Open · close the help screen (§13). Opens even while paused |
| `F3` | Pause · resume (the top button "일시정지" is the same). Commands are accepted even while paused (queued) and executed exactly once on the first tick of resuming |
| Main menu › Settings › Color-blind support | When on, adds shape markers (circle · square · triangle · diamond · cross, cycling by faction number) next to the faction color: above units · on zoomed-out dots · top-left of company symbols. Default off |

---

## 2. Selection

| Input | Action |
|---|---|
| Left-click drag | All friendly units inside the rectangle (free selection). If there are no friendly units, 1 friendly building |
| `Alt` + drag | All members of every **squad** touched by even one unit in the rectangle |
| `Shift` + drag | All members of every **company** touched by even one unit in the rectangle (added to the selection) |
| Left-click | Select one unit (click an empty place · `Esc` to clear) |
| `Shift` + click | Add to · remove from the selection (when the selection is only friendly units) |
| Double-click (friendly unit with an organization) | Cycle selection **squad → company → battalion → squad** (holding `Shift` adds to the existing selection) |
| Double-click (unit without an organization) · `Ctrl` + click | All friendly units of the same kind on screen |
| Click a squad chip (inside a company card) | Select that squad (`Shift` = add · if already all chosen, clear) |
| Click a company card | Select that company (`Shift` = add · if already all chosen, clear) |
| Click an empty place | Clear selection (kept if `Shift` is held) |
| `Esc` | Cancel if a command is pending, otherwise clear selection |
| Number `1`~`9` (including numpad) | Select that-number **regiment** (all its battalions; if the regiment is empty, the battalion with the same number). Pressing the same number again within 0.35 s also moves the camera (`Shift` + number does nothing) |
| `Alt` + `1`~`9` | Select that-number **battalion** |
| `0` | Open · close the battalion list panel |

---

## 3. Basic commands (move · attack-move · stop · hold · assault)

| Key | Command | Action |
|---|---|---|
| `M` | Move | Click a point to move there (keeps the default formation direction) |
| `A` | Attack (attack-move) | Click an enemy to attack it. Click the ground to move engaging enemies on the way. Neutral targets can also be force-attacked with `A` |
| `S` | Stop | Stop the current command |
| `H` | Hold | Hold the position. Not pushed by other units |
| `C` | Assault (bayonet) | Run toward the enemy (or point) 30% faster and fight in melee. No firing during an assault. Automatic assault if the enemy is within 2 cells. Even with mixed branches selected, the button · hotkey works if even one soldier can assault — only soldiers with melee weapons charge, and machine guns · guns · carts etc. **hold** in place (covering fire, N36) |
| `Q` | Quick step | Move 15% faster than walking using stamina (slower than an assault) |

The start delay of the player's direct commands (reaction 0~2 ticks + command chain delay) shrinks with the number chosen — 1 man ×0.25 · squad (12) ×0.3 · platoon (40) ×0.5 · company (150) ×0.8 · battalion (500+) ×1 (the current level), linear in between (`balance.json` `command.playerSelSizes/playerSelMul`, N37). AI commands ×1.

**Right-click** (target = selected units, when no command is pending):
- Right-click an enemy → attack (units with weapons). Non-combat units follow
- Right-click the ground → move
- Right-click drag → **front command**: move to the midpoint of the drag line, width is the drag length, direction is perpendicular to the drag line and on the side farther from the selection's center of mass (formation uses the default formation)

**Command pending**: `M` · `A` · `Q` · `C` · `X` · `T` are executed only after pressing and then left-clicking a point (or enemy). Cancel with right-click · `Esc` while pending.

**Command queue (N74, `Shift`)**: `Shift` + right-click (move · attack enemy) · `Shift` + right-click drag (front) · `Shift` + left-click after `M`/`A`/`Q`/`C` (command pending is kept while `Shift` is held so several points can be clicked in a row) · `Shift` + `H` (hold at the end) are appended after the current command. The same group of units shares the same queue; choosing a different group makes a new queue for only those units. When all of the group finishes one leg (arrival · target gone), the next leg. When selected the route (line · circle, hold = square) is shown. **Cancel**: a move · attack · stop · hold · assault · retreat · barrage command issued without `Shift` = discards the remaining queue. Later legs have no command chain delay (only reaction delay).

**Line-of-fire blocked indicator (N75)**: above soldiers among our direct-fire gunners who have a visible enemy within range but all lines of fire are blocked, a hatched circle — dark green = forest, brown = beyond highland, gray = rock · building etc. Display only (unrelated to the sim).

`Enter` opens the command input box (console) (§9).

---

## 4. Command card

The card at the bottom of the screen shows the commands that fit the current selection. Card positions are a 3×3 grid, and the hotkey is the same as the letter on the card. Card hotkeys do not work while `Ctrl` is held.

**Tabs**: `Tab` switches the **command ↔ combat** tab (or click the tab button). While spectating, `Tab` toggles the spectate panel.

**Page turning (T38c)**: when the command tab has more than 9 buttons (e.g. infantry company + barrage type · rally point · trench assault …) the card is divided into 8 per page + a bottom-right 「쪽 N/M」 [page N/M] button. Click the button or press the `.` key for the next page (after the last, the first page). With 9 or fewer, the old layout is kept. **The hotkeys of all commands work even on a page that is not visible.** `.` is a fixed key so it is not a rebinding target. UI only (unrelated to sim · save).

### 4-1. Hotkey table

| Key | Command | Card position (column, row) | Display condition |
|---|---|---|---|
| `M` | Move | (0, 0) | Everyone |
| `S` | Stop | (1, 0) | Everyone |
| `A` | Attack | (2, 0) | At least one unit has a weapon |
| `J` | Rally point | (2, 0) default (second page if cells are full) | When a unit belonging to a company is selected. The hotkey works on any tab · any page (global) |
| `Q` | Quick step | (0, 1) | All infantry (excluding cavalry) |
| `H` | Hold | (1, 1) | Everyone |
| `X` | Grenade | (2, 1) | All hold grenades (assault infantry) |
| `E` | Autonomous reconnaissance | (2, 1) default | All scouts |
| `O` | Barrage type (cycles sustained → preparatory → creeping barrage → smoke) | (2, 1) default | When there are howitzer · deployed guns |
| `T` | Point barrage | (0, 2) default | All deployable guns + howitzer or area weapon |
| `F` | Assault fire (company toggle) | (0, 2) default | When a unit belongs to an infantry company |
| `D` | Dismount · mount toggle | (0, 2) default | All cavalry (excluding officers) |
| `K` | Fire control (free → hold fire → return fire only) | (1, 2) default | When there are armed units |
| `V` | Charge only (cavalry toggle) | (1, 2) default | All cavalry |
| `L` | Line tidying | (1, 2), if occupied (2, 1) | At least one unit belongs to a company |
| `C` | Assault (bayonet) | (2, 2) | At least one has a melee weapon (soldiers that cannot hold, N36) |
| `W` | Trench assault | First empty cell (not shown on the card if none) | When a company is selected (if the trench assault setting is on). Works as a global key on any tab even without a cell |
| `G` | Company merge (not a card) | — | If a company is selected (§5) |

**Second page** (cmdcard, 2026-10-21): when a selection has more than 9 buttons (e.g. infantry company + howitzer) the overflowing buttons do not disappear but go to the second page. The last cell of the first page = "더 보기" [show more], the last cell (2, 2) of the second page = "처음으로" [to the beginning] (click only, no hotkey). The hotkeys of all pages work regardless of the page currently visible. Returns to the first page on a new selection · tab switch. `J` · `W` are global keys so they are page-independent.

Grenades use the `X` key (moved from `G` 2026-10-19; resolves the conflict with company merge `G` — N15 item 1). X confirmed to be used for no other purpose anywhere in combat · deployment · console (text input) · card.

**`L` line tidying** (gapfill, 2026-10-19): the **entire company** the chosen units belong to (even if only a squad is chosen, that company) fills the gaps now and retakes its positions — even in combat. The command is valid for 20 seconds (`combat.closeWindowSec`): waiting soldiers who strayed more than 1 cell (`closeSlack`) from their slots walk straight to their positions (move command), and soldiers in engagement (target · fired within 5 s) depend on proficiency — recruits (rank 0~1) go straight away, regulars (2~3) 50% straight away · otherwise after 6 s, veterans (4+) 15% straight away · otherwise after 12 s (immediately when the engagement ends). Soldiers in trenches · trench positions, soldiers holding · moving · assaulting stay as they are. During a march (flag move) · tactical framework action, after it ends (within the validity time). The hotkey `L` works on both the command tab · combat tab (does not overlap with other keys — also differs from `R` rotate in the mission deployment stage). Officers also issue the same command themselves when the formation is badly disordered in combat (ENGINE_NOTES 'gapfill').

**`F` assault fire**: company-level toggle (default off). When on, during an attack-move when an enemy is visible within range, the officer judges and the whole company slowly walks while firing. Not done during normal movement · retreat.

### 4-2. Command summary by unit type

| Unit | Commands shown on the card |
|---|---|
| General infantry | `M` `S` `A` `Q` `H` `C` `F` (+ `L` · `J` if belonging to a company, `K` if armed, `W` if the setting is on) |
| Assault infantry | the above + `X` (grenade) |
| Scout | infantry commands + `E` (autonomous reconnaissance) |
| Cavalry | `M` `S` `A` `H` `V` `K` (+ `C` if holding a melee weapon, `D` excluding officers, `L` · `J` if belonging to a company). `Q` (quick step) excludes cavalry |
| Deployable guns (infantry gun · field gun · howitzer) | `M` `S` `A` `H` `K` `T` (point barrage) · `O` (barrage type) |
| Tank · armored car · transport vehicle | **(needs checking)** — not organized in this check. The card follows the `Available` condition (`src/Core/Commands.cs`) |

The card is shown **only when all selected units satisfy the condition** (attack is the exception, shown if even one has a weapon). Example: choosing infantry and a gun together does not show `Q`.

### 4-3. Combat tab

Formation buttons are click only without hotkeys (except line tidying `L`). If companies are chosen it changes their formation, otherwise the default formation.

| Button | Action |
|---|---|
| Line tidying (`L`, cell (0, 0)) | Only when a unit belonging to a company is chosen. Same as `L` on the command tab (the only button with a hotkey on this tab) |
| Free | No formation (`Free`). Even with companies chosen only the default formation changes to free |
| Spread | Spread formation (chosen companies · default formation) |
| Show more | Cycles line → column → wedge → box → line |

---

## 5. Organization (company · battalion)

Terms: **squad** (group of units, chip in a company card) → **company** (max 100 men, one card) → **battalion** (max 10 companies, one cell of the right bar). Battalions are given numbers 1~9.

| Input · button | Action |
|---|---|
| `Ctrl` + `Alt` + `1`~`9` | Form a new company from the currently selected friendlies and put it into battalion n (T08: moved from the former `Alt`+number). If the battalion is full only a notice appears |
| `Ctrl` + `Shift` + `1`~`9` | Assign the current battalion to regiment n (regiment = battalion number default (n-1)/3+1, the assignment record is screen state — not saved, see design proposal) |
| `Ctrl` + `1`~`9` | Change the current battalion's number to n (if another battalion already uses the number they swap). "Current battalion" = the selection is exactly all companies of one battalion, or within the last called battalion |
| `1`~`9` | Select that-number regiment (`Alt`+number = battalion). Pressing the same number again within 0.35 s also moves the camera |
| `0` | Open · close the battalion list panel (select a battalion · company by clicking in the list) |
| `G` · [병합] button | Bundle the selected companies into one battalion (a new battalion) |
| Drag a company card → battalion cell | Assign to that battalion (notice if the battalion is full) |
| Drag a company card → empty place of the battalion bar · list panel | Detach from the battalion |
| Company card [분할] [split] | Split a company of 2 or more men **(needs checking: composition rule after splitting)** |
| Company card [해산] [disband] | Dissolve the company **(needs checking: handling of the force)** |

---

## 6. Formations · tactical frameworks · roles

### 6-1. Formation kinds

Free (Free) · line (Line) · column (Column) · wedge (Wedge) · box (Box) · spread (Spread) · left · right echelon (Echelon, **cavalry companies · squads only**).

### 6-2. Where to change

| Target | Input | Cycle |
|---|---|---|
| Default formation (first cell "기본" [default] of the battalion bar) | Left-click | free → line → column → wedge → box → spread → free |
| Branch placement of the default formation | Right-click (same cell) | mixed → armor front · infantry rear → infantry front · armor rear → mixed |
| Selected company | `Ctrl` + `F` | line → column → wedge → box → spread → line (cavalry companies get left · right echelon after spread). If no company is chosen, the default formation |
| Selected battalion's formation | `Ctrl` + `Shift` + `F` | If a battalion is selected, cycles the battalion formation (below), otherwise the branch placement order of the default formation |
| Battalion formation | Left-click the battalion cell of the battalion bar | line → column → ladder → 2 lines + reserve → **waves** → line |
| Company card [대형] [formation] | Left-click | Company formation cycle (same as `Ctrl`+`F`) |
| Squad formation (only 1 squad selected) | Company card [분대] [squad] | auto → box → spread → column → line → wedge → auto (cavalry gets left · right echelon after wedge) |

### 6-3. Tactical frameworks · roles

- **Tactical framework** (company card [전술] [tactic]): no framework → fire-maneuver → left bypass → right bypass → defense in depth → artillery formation → assault → no framework.
- **Role** (only 1 squad selected, company card [역할] [role]): auto → fire team → firing team → maneuver team → support → command · reconnaissance → auto.

The same can be set with the console (targets the selected company, §9): `ftac`, `fco`, `fsq`, `fbat`, `fmove`.

---

## 7. Artillery (deployment · point barrage · firing arc)

- **Deployment**: deployable guns (infantry gun 2 s · field gun 4 s · howitzer 6 s, data `setupTime`) **deploy automatically when about to fire**. They do not fire before deployment completes. While deploying · deployed they cannot move, and on receiving a move command they pack up first (pack time `packTime` 1.5 · 3 · 4 s). There is no manual deployment command.
- **Firing arc**: fires only within the center ± half (data `traverseArc`) decided at deployment. Targets outside are handled in the order pack up → rotate → redeploy. For the arc values see `assets/data/balance.json`.
- **Point barrage (`T`)**: after clicking a point, keeps shelling that point even without an observer (spread 1.5x). Not possible out of range · inside minimum range. Direct-fire guns need a line of fire. Ends on stop · another command · ammo exhaustion.
- Among deployable units, the heavy machine gun family (`heavy_machinegunner` · `heavy_maxim`) and `heavy_at_rifleman` also have `setupTime` 3, but whether they fit the point barrage display condition (howitzer or area weapon) is **(needs checking)**.

---

## 8. Scouts

- Scouts (`scout`, `infantry_scout`) have 1.5x sight (`scoutSightMul`).
- **Autonomous reconnaissance (`E`)**: when on, they go 6~10 cells ahead on their own when the company moves and observe (including howitzer observation). They return to the officer 5 s after being suppressed · wounded · finding an enemy, or if they get too far.
- Giving a direct command turns off autonomous reconnaissance. It turns on again when `E` is pressed again or the company they belong to receives a new command.

---

## 9. Sound · screen toggles

| Input | Action |
|---|---|
| `I` | Toggle squad status icons on · off (the top button "분대 아이콘" is the same). Shows suppression · retreat · out of ammo · assault · no commander · trench occupation as one icon above the squad (medium · close zoom, selected squads always) |
| `U` | Cycle the overhead display level (concise → normal → detailed, same as settings 「머리 위 표시」 [overhead display], default concise). Concise = health · ammo · load bars and color-blind shapes only for selected · mouse-over, squad icons only for selected · danger states (pinned · suppressed · retreat · low/no ammo). Normal = the old behavior. Detailed = friendly health · ammo · load always |
| `B` (while held) | Show lines of fire (lines friendlies saw · can fire). With `Shift` held also, lines of fire from the cursor (N75). Not shown together with `Ctrl` · `Alt` |
| `N` | Sound on · off. Also lifts the engagement-test mute |
| Console `mute` | Same toggle |
| Console `volume <0~100>` | Sound effects volume |
| `Ctrl` + `G` | Grid display on · off (the top button "격자" is the same) |
| Console `fps` | Frame time display on · off |
| `F5` (during an engagement test) | Restart immediately with the same composition |
| `F6` · `F7` | Quick save · quick load |
| `Tab` (while spectating) | Toggle the spectate panel. Switch viewpoint with number keys (0 = all) |

- Engagement tests (`--battle` · `--quick-battle`) are muted by default. Sound is on with `KD_SFX_BATTLE=1`.
- Top buttons: grid · save · load · spectate · pause · quit.

---

## 10. Console commands

Open with `Enter`, run with `Enter`, close with `Esc`. The up · down keys recall input history. Commands are not accepted while spectating · in replays.

| Command | Description |
|---|---|
| `help` · `?` | Print the command list |
| `show all maps` (`reveal`) | Toggle revealing the whole map on · off (cheat) |
| `spawn <unit> [count]` | Spawn my units at the screen center (1~200, cheat) |
| `funds <amount>` | In the help but **(needs checking)** — no branch handling it was found in this code (may not work) |
| `select <unit key>` | Select 1 of my units of that kind nearest the screen center (for confirmation · demo) |
| `zoom <factor>` | Set the zoom factor |
| `org <men per unit> <company count> [battalion number]` | Organization demo: group armed non-armored units near the screen center into companies, select them and march forward (battalion number default 1) |
| `ftac <framework key>` | Tactical framework of the selected company. Keys: `fire_manoeuvre` · `flank_left` · `flank_right` · `defence_depth` · `artillery` · `assault` · (empty = no framework) |
| `fco <formation key>` | Formation of the selected company. Keys: `line` · `column` · `wedge` · `open` (spread) · `close` (box) · `echelon_left` · `echelon_right` |
| `fsq <formation key>` | Formation of all squads of the selected company (keys same as `fco`, auto if no key) |
| `fbat <kind>` | Battalion formation of the selected company. Kinds: `line` · `column` · `echelon` · `reserve` · `waves` |
| `fmove <dx> <dy> [width]` | Move the selected company by (dx, dy) from the current center of mass (width can be specified) |
| `save [name]` · `load [name]` · `saves` | Save · load · list. `F6` · `F7` = quick save · load |
| `mission win` · `mission lose` | Force the mission result in mission mode (mission only) |
| `after <ticks> <command>` | Run a command after the given ticks (for demo · screenshots) |
| `volume`, `mute`, `fps` | See §9 |

Cheat-type commands are recorded in the replay.

---

## 11. Launch options

### 11-1. CLI flags (`KoreanDivision.exe`)

**Basic start · world**

| Flag | Value | Description |
|---|---|---|
| `--players` | N | Number of players (limited to 2 ~ the maximum) |
| `--faction` | number or name | The faction I control |
| `--map` | `WxH` or map name | Map size or map (map names such as `corners4`) |
| `--seed` | N | Random seed |
| `--ai` | name · `none` | AI: `tactical` · `tactical:<profile>` · `standard` · `passive` · `rush` · `battle` · `none` |
| `--nofog` | — | Turn off fog |
| `--weather` | `clear` · `overcast` · `fog` · `rain` · `snow` | Weather (for testing) |
| `--time` | `dawn` · `day` · `dusk` · `night` | Time of day (for testing) |
| `--theme` | `kd1920` · `ww2` · `none` | Theme |
| `--mute` | — | Sound off at start |

**Spectating · replays**

| Flag | Value | Description |
|---|---|---|
| `--spectate` | — | Start in spectate mode |
| `--view` | N | Spectate start viewpoint (0 = all) |
| `--replay` | file | Play a replay |
| `--verify-replay` | file | Verify a replay |
| `--no-replay` | — | Do not save a replay |

**Engagement tests · missions**

| Flag | Value | Description |
|---|---|---|
| `--quick-battle` | [n] | n infantry on an empty flat vs n AI infantry (default 500) |
| `--battle` · `--vs` | `"kind:count,..."` | Engagement composition (friendly · opponent) |
| `--battle-map` | `flat` · `terrain` · `city` | Engagement map |
| `--battle-ai-vs-ai` | — | AI on both sides |
| `--battle-distance` | N | Engagement start distance |
| `--mission` | mission ID | Mission (from the deployment stage) |
| `--mission-start` | — | Skip the mission deployment stage |

**Screen · screenshots · tests**

| Flag | Value | Description |
|---|---|---|
| `--shot` | output png | Save a screenshot then exit |
| `--shot-frame` | N | Frame at which to take the screenshot |
| `--sim` | seconds | Simulation time to advance before the screenshot |
| `--demo` | — | Demo scene (engagement in front of the base) |
| `--exec` | command | Run a console command at start (can be repeated) |
| `--novsync` | — | Turn off vertical sync (performance measurement) |
| `--menu-shot` · `--menu-page` | file · `main` `setup` `spectate` `replays` | Start screen screenshot · page specification |
| `--selftest` | — | Self test |
| `--uifuzz` | [seconds] | UI fuzz test (default 120 s, random inputs) |
| `--fuzz-menu` | — | Fuzz starting from the start screen |
| `--probe` | — | Determinism probe |

**Windowless modes** (exit immediately if the arguments are right)

| Flag | Description |
|---|---|
| `--stress` | Unit count stress test |
| `--collide` | Large-scale combat movement · overlap measurement |
| `--snapbench` · `--snaptest` · `--snap-run` | Save file size · time measurement · verification |
| `--balance` [suite] · `--balance-worker` | Balance automatic matches (`--balance-worker` is for child processes) |
| `--mission-run <id>` | Run a mission to the end without a window (AI for all factions). `--seconds N` · `--seed N` · `--verify` |
| `--detcheck` | Determinism checksum (used by `tools/detcheck.ps1`) |

A flag not in the arguments stops with the `알 수 없는 인자` [unknown argument] error.

### 11-2. Environment variables for testing (summary)

There are several environment variables starting with `KD_` (about 50 by grep). Only the main ones are listed. All are for testing · screenshots · diagnostics and are not needed in normal play.

| Variable | Value | Purpose |
|---|---|---|
| `KD_SHOT_NOINPUT` | `1` | Ignore real mouse · wheel input during `--shot` (for screen comparison) |
| `KD_SFX_BATTLE` | `1` | Turn on sound in engagement tests too |
| `KD_INF_AMMO` | `1` | No infantry ammo consumption |
| `KD_CAMPOS` | `x,y` | Start camera position (tile coordinates, for screenshots) |
| `KD_CAMZOOM` · `KD_SHOT_ZOOM` | factor | Start · screenshot zoom factor |
| `KD_SCENE` | scene name (e.g. `mgaim`, `allunits`) | Scene for confirmation (with `--demo`) |
| `KD_FAKE_AGE` | ticks | Fake age for checking corpse presentation |
| `KD_TAC_DBG` · `KD_TAC_DBG_SEC` · `KD_AIMDEBUG` | — | Tactical AI · aiming debug output (**(needs checking)** detailed format) |
| `KD_RESULTS_DIR` | path | Result file save location |
| `KD_SELFTEST_ONLY` | — | Self-test-only run (**(needs checking)** detailed scope) |
| `KD_LOD` · `KD_LOD_SYNC` · `KD_LOD_EVICT_SEC` · `KD_LOD_UPLOAD_MB` | — | Rendering LOD tests (**(needs checking)** details) |
| `KD_MAPWORKERS` · `KD_MAPSTATS` | — | Map generation parallelism · statistics (**(needs checking)**) |
| `KD_PROBE_SNAP` · `KD_PROBE_PRE` · `KD_PROBE_N` | — | Details of the `--probe` determinism check |

## 12. Conflicts (several actions on one key)

Check result: **there is no place where two actions are bound to one key in the same situation.** Below are the places where the same key is divided by situation · modifier.

- `Enter`: open the command console (main game) · start mission deployment (deployment stage) · restart on the game-over screen · executes inside the console.
- `Esc`: cancel the pending command → clear selection, in that order. Closes each in the console · main menu.
- `F`: assault fire card (no modifier) · `Ctrl`+`F` formation cycle · `Ctrl`+`Shift`+`F` branch placement. Card hotkeys are blocked when `Ctrl` is pressed.
- `G`: company merge · `Ctrl`+`G` grid display (separated by modifier).
- `W`: trench assault — the card if there is a card cell, otherwise a global key. The same action runs only once (`pressedCard`).
- `L`: line tidying — on both the command tab · combat tab (same action).
- `Tab`: toggle the spectate panel while spectating · otherwise switch the command ↔ combat tab (mutually exclusive).
- `1`~`9`: battalion selection · `Ctrl`+number number change · `Alt`+number company organization · viewpoint switching while spectating. Separated by modifier · mode. `Shift`+number only blocks battalion selection and has no other action.
- `F5`: restart only during an engagement test (no action otherwise).
- Card cell (2, 1): the default cell of `X` · `E` · `O` is the same → the later button moves to the first empty cell, if the cells are full to the end, the second page (keys unchanged, not a key conflict).
- Code check (cmdcard): no duplicates in the same situation between the card hotkeys `M S A Q H X T V D E C F K L O W J` · globals `B I N G R Space Tab [ ] + - F3 F5 F6 F7 0~9`. The in-game `keys` console command prints this table (`src/UI/Bindings.cs`) as is.

## 13. Hotkey list (synchronized with the in-game help)

Pressing `F1` in the game shows the same content as this table (`Bindings.All()`) as a help screen (also opens on the pause · quit screens; close with `F1` · `Esc` · an outside click, scroll with the wheel · `↑` `↓` · `PgUp` `PgDn`). The table below must not differ by even one line from `Bindings.All()` — `KD_SELFTEST_ONLY=uiicons` compares between the markers and fails if anything is missing or extra. When changing a key, fix `src/UI/Bindings.cs` and this table together.

**Rebinding (T38a)**: on the start screen 「설정」→「단축키 재지정」 [Settings → Hotkey rebinding], change the keys (`A`~`Z`) of the 11 card commands — press `변경` [change] then the new key, keys already in use · the fixed keys of the table below (including `R`) are rejected (reason shown), `기본값` [default] per line · `모두 기본값으로` [all to default]. Only the changed ones are saved in `Keys` of `settings.json`. This table (and the CONTROLS sync test) is the **default keys** (`Bindings.AllDefaults()`), while the in-game `F1` help · the console `keys` · the card labels show the changed keys (`Bindings.All()`).

<!-- BINDINGS:BEGIN -->
| 키 | 동작 |
|---|---|
| `M` | 이동 |
| `S` | 정지 |
| `A` | 공격 |
| `Q` | 속보 |
| `H` | 사수 |
| `X` | 수류탄 |
| `Y` | 연막탄 |
| `U` | 철조망 절단 |
| `T` | 지점 포격 |
| `V` | 돌격만 |
| `D` | 하마/승마 |
| `E` | 자율 정찰 |
| `C` | 돌격 |
| `F` | 돌격 사격 (중대 토글) |
| `K` | 사격 통제 (자유/금지/응사) |
| `L` | 대열 정비 |
| `O` | 포격 종류 |
| `J` | 집결 지점 (전역) |
| `W` | 참호 돌격 (전역) |
| `G` | 중대 병합 (Ctrl+G 격자) |
| `Tab` | 명령/전투 카드 탭 |
| `.` | 카드 쪽 넘김 (명령이 9개 넘을 때) |
| `Space` | 선택 부대로 이동 |
| `1~9` | 연대 선택 |
| `Alt+1~9` | 대대 선택 (0 = 목록) |
| `Ctrl+Alt+1~9` | 중대 편성 → 대대 |
| `Ctrl+Shift+1~9` | 대대 → 연대 편입 |
| `Ctrl+1~9` | 대대 번호 변경 |
| `I` | 분대 아이콘 |
| `U` | 머리 위 표시 단계 (간결/보통/상세) |
| `B` | 사선 (누르는 동안) |
| `N` | 소리 |
| `[ ]` | 게임 속도 |
| `+ -` | 확대/축소 |
| `F1` | 도움말 (이 목록) |
| `F2` | 병종 도감 (전투 중 · 열려 있는 동안 정지) |
| `F3` | 일시정지 |
| `F6/F7` | 빠른 저장/불러오기 |
| `Enter` | 콘솔 |
| `Esc` | 취소/선택 해제 |
<!-- BINDINGS:END -->

> Translation note: the table between the BINDINGS markers is intentionally left in Korean — it is compared line by line against the in-game strings `Bindings.All()` by the selftest `uiicons`, so translating it here would break the sync test. English gloss: 이동 move · 정지 stop · 공격 attack · 속보 quick step · 사수 hold · 수류탄 grenade · 연막탄 smoke grenade · 철조망 절단 barbed wire cutting · 지점 포격 point barrage · 돌격만 charge only · 하마/승마 dismount/mount · 자율 정찰 autonomous reconnaissance · 돌격 assault · 돌격 사격 assault fire · 사격 통제 fire control · 대열 정비 line tidying · 포격 종류 barrage type · 집결 지점 rally point · 참호 돌격 trench assault · 중대 병합 company merge (격자 grid) · 명령/전투 카드 탭 command/combat card tab · 카드 쪽 넘김 card page turn · 선택 부대로 이동 go to selected unit · 연대 선택 regiment selection · 대대 선택 battalion selection (목록 list) · 중대 편성 → 대대 company formation → battalion · 대대 → 연대 편입 battalion → regiment assignment · 대대 번호 변경 battalion number change · 분대 아이콘 squad icons · 머리 위 표시 단계 (간결/보통/상세) overhead display level (concise/normal/detailed) · 사선 (누르는 동안) line of fire (while held) · 소리 sound · 게임 속도 game speed · 확대/축소 zoom in/out · 도움말 (이 목록) help (this list) · 병종 도감 (전투 중 · 열려 있는 동안 정지) branch codex (in combat · paused while open) · 일시정지 pause · 빠른 저장/불러오기 quick save/load · 콘솔 console · 취소/선택 해제 cancel/clear selection.

---

*This document was organized by reading the code. When controls change, fix the relevant section and the text in `src/UI/Strings.cs` together.*

---

> Planned (unimplemented, user decision 2026-10-21): number 1~9 = regiment selection, Alt+number = battalion selection (the current Alt+number 'battalion assignment' moves to another key)
