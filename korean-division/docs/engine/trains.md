# Engine notes - trains (rail tracks, train movement, weapons, freight, demo mission) (엔진 노트 - trains) [split from ENGINE_NOTES.md 2026-10-23, body verbatim]

## Rail track terrain (철도 선로 지형) (2026-10-22, branch `rail`, T07, FormatVersion 53)
- `TerrainType.Rail` (last enum value): passable · movement multiplier 1 · no cover · does not block vision/line of fire (= bare ground). No shell craters form (not a TryCrater target). Track destruction and armored trains are out of scope.
- Map json paint `"line": [x0, y0, x1, y1, ...]` = Bresenham 1-tile-wide polyline (`MapFile.LineTiles`). Rendering: gravel procedural tile (MapRenderer) + sleepers/rails overlay `src/Render/RailRenderer.cs` (TerrainVersion cache).
- Test `KD_SELFTEST_ONLY=terrain` (6 checks in the rail block).

## Train movement (열차 이동) (2026-10-22, branch `t30a-train-move`, T30a, FormatVersion 58 · Replay 23)
- `src/Core/Train.cs`: a train = an ordered sequence of `TrainCar : Entity`, kept in `World.Trains` outside World.Units/byId ([SnapExt], WriteTrains at the end of ORG_). Arc length is an integer (1 tile = 65536, diagonal 92682), head S, car i = S − i·spacing. With no trains, checksum and Step are unaffected.
- Path = 8-direction BFS between mission json `trains[].path` waypoints (rail tiles) (fixed neighbor order, no revisiting tiles already passed), `loop` = circular line. Script: waypoints + `waits` seconds of stopping, reverse at the end and repeat. Control: `TrainMoveCommand(f, train, dir)` (-1/0/1, turns the script off), API `World.TrainMove`.
- Acceleration/deceleration = balance.json `trains` (stoppable speed √(2·brake·distance)), no passing through the end of the track. A unit on the track within the lookahead tiles ahead -> stop (detected every 6 ticks). Test `KD_SELFTEST_ONLY=train`.

## Train weapons and car damage (열차 무기 · 객차 피해) (2026-10-22, branch `t30b-train-weapons`, T30b, FormatVersion 59)
- `TrainCarDef`: weapons (existing weapon keys) · category (default armored = armored-vehicle rules) · engine. `TrainCar.Guns` (TrainGun: TargetId · Cooldown · AimLeft ticks), `StepTrainGun` at the end of StepTrains (FindTarget every AcquireInterval -> aim -> line of fire -> Fire). Ammo unlimited.
- Shared combat path: FindTarget car candidates (trainMask), shooter Entity for Fire/FireSpray, bonus = `World.CategoryOf`, Explode -> `SplashTrains`, `World.Find` -> `FindCar` fallback, fog car vision, CanMelee not possible for cars.
- HP 0 = wreck (stays on the train). All locomotives destroyed -> `Train.Stalled` (brake · stop, ignores commands). Save: trains = end of ENTS, entity reference tag 3 = car. Test `KD_SELFTEST_ONLY=trainweapons`.

## Freight train objective (화물열차 목표) (2026-10-22, branch `t30c-freight-objective`, T30c, FormatVersion 59 unchanged)
- Mission objective gets `"train": "<train tag>"` (escort · capture · eliminate). Target = the freight cars of the trains with that tag (`TrainCarDef.Freight`, balance.json wagon). `src/Core/Mission/FreightObjective.cs` (validation · evaluation), branch on the first line of EvalObjective.
- escort = count living freight cars inside the area (shortfall, or all remaining trains Stalled -> failure). capture = sole control within the freight car radius (default 3) for seconds, no ownership change (wiped out -> failure). eliminate = percent% of freight cars destroyed (progress as a 0~1 ratio).
- No new save state (ObjProgress · car HP). AI ObjectiveFor: reuses escort/capture centered on freight cars. UI: objective text · world/minimap wagon markers (MissionText.FreightMarkers) · result `trains` losses. Test `KD_SELFTEST_ONLY=freight`.

## Armored train demo mission (장갑열차 시연 미션) (2026-10-22, branch `t30e-demo-mission`, T30e, FormatVersion 59 unchanged)
- `assets/missions/train_escort_demo.json`: escort (Q-E1 recommended), inline mapData 150x64, armored train + freight train with scripted movement, 900-second time limit. Not linked to the campaign. Test `KD_SELFTEST_ONLY=traindemo` (golden = `assets/data/train_demo.golden`, update with `KD_TRAIN_DEMO_UPDATE=1`). No code or balance changes.
