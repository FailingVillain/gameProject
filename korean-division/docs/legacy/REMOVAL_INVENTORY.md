# 제거 목록 (방향 변경 §0-1, 2026-10-19)

> 조사: Gemini Flash (agentHub batch, 3작업 $0.35) — 줄 번호는 `0fd33d2` 기준. Claude 가 몇 줄 직접 확인함 (WorldCombat 86-97 Build/Repair/Refuel 분기, Game.cs 1406 SetupDemo, AiHelpers 8-11 건물 정의, StressTest 361 'R' 시나리오 — 모두 일치).
> 아래 '결정' 이 최종 기준. 원 목록의 DELETE/MODIFY 는 지도로만 쓴다.

## 결정 (Claude, 구현 기준)
- **Building 클래스는 유지** — 도시 · 점령지가 Building 이다. 건설 · 수리 · 생산 · 변환 · 수입 · 저장 · 충전 필드만 뺀다. `Complete`/`Progress` · 큐 · 집결 · 포탑 무기는 제거.
- balance.json `buildings`: **metropolis · city · village · mine · oilfield 유지** (점령 대상, 수입 없음). barracks · depot · turret · factory · supply_plant · fuel_tank · research_lab 삭제.
- 시작 거점(metropolis, headquarters) 은 그대로 세력마다 1개 소유하고 시작 → 그 앞에 **주어진 병력** 스폰.
- 승패: (일반 게임) 도시(city 플래그)를 하나도 갖지 않거나 **유닛이 전멸**하면 탈락. 대도시 상실 후 위치 공개(revealDelay) 유지. 교전 테스트(BattleMode) 규칙은 그대로.
- 자원 5종 · `Fixed` 자원 표 · Faction 재고/수입/인구 · 생산 · 건설 · 수리 · 공병(worker) · 보급소/연료탱크 · 연료 · 자동 재보급 여정 · 공병 주유(+ 'G' 키 충돌) · 자원 송금 · 자금 치트 → **삭제**.
- 탄약(`Unit.Ammo`, 무기 `ammo`)은 **유지** → 새 회복 방식으로 교체 (Supply.cs 재작성).
- PassiveAi: 건설형 → '제자리 방어' AI 로 축소 (이름 유지, 명령줄 호환).
- 외교: 자원 송금 · 인구 기반 전력 계산 제거, 나머지 유지.
- 저장: FormatVersion 7, ReplayFile 3 (명령 종류 삭제).

## 원 목록 (Gemini)
### src\Core\Defs.cs
- 10:16 - `ResourceDef` class - **DELETE**
- 19:24 - `Fixed` helper class (1/1000 fixed-point) - **DELETE** (or **MODIFY** if retained for non-resource math)
- 62:63 - `WeaponDef.AmmoCost` - **DELETE**
- 108:110 - `UnitDef.Cost`, `Costs`, `CostMilli` - **DELETE**
- 111:111 - `UnitDef.Supply` (population/supply usage) - **DELETE**
- 112:112 - `UnitDef.BuildTime` (unit production time) - **DELETE**
- 127:128 - `UnitDef.FuelCap` - **DELETE**
- 144:144 - `UnitDef.CanBuild` - **DELETE**
- 151:156 - `UnitDef.Upkeep`, `FuelUse`, `UpkeepMilli`, `FuelUseMilli` - **DELETE**
- 161:168 - `ConvertDef` class (factory resource conversion) - **DELETE**
- 170:213 - `BuildingDef` class - **DELETE** or **MODIFY** (strip construction/repair/resource/factory/storage fields; keep capture/city definition fields for cities/capture sites)
  - 175:177 - `Cost`, `Costs`, `CostMilli` - **DELETE**
  - 178:178 - `BuildTime` - **DELETE**
  - 183:183 - `Supply` - **DELETE**
  - 184:186 - `Income`, `Incomes`, `IncomeMilli` - **DELETE**
  - 187:187 - `Produces` - **DELETE**
  - 188:189 - `Requires` - **DELETE**
  - 191:191 - `Buildable` - **DELETE**
  - 193:193 - `Convert` - **DELETE**
  - 202:204 - `SupplyStorage`, `FuelStorage` - **DELETE**
  - 205:210 - `ResupplyRadius`, `ResupplyRate`, `RefuelRadius`, `RefuelRate` - **DELETE**
- 215:239 - `EconomyDef` class - **DELETE** or **MODIFY** (remove resource amounts, queues, repair, supply storage, builders, refuel)
  - 217:219 - `StartFunds`, `StartResources`, `StartMilli` - **DELETE**
  - 220:220 - `SupplyCap` - **DELETE**
  - 221:221 - `QueueSize` - **DELETE**
  - 222:223 - `RepairCostRatio`, `ConstructionCancelRefund` - **DELETE**
  - 225:226 - `StartUnits` (worker references) - **MODIFY**
  - 228:230 - `BaseSupplyStorage`, `BaseFuelStorage` - **DELETE**
  - 233:238 - `MaxBuilders`, `BuilderEfficiency`, `EngineerRefuelRate`, `EngineerRefuelRange` - **DELETE**
- 320:320 - `Defs.Economy` - **MODIFY** (or **DELETE** if economy def stripped)
- 324:324 - `Defs.Resources` - **DELETE**
- 329:329 - `Defs.Buildings` - **MODIFY** (only keep city/capture site defs)
- 352:357 - `Defs.Load` funds/resource indexing - **DELETE**
- 362:362 - `Defs.Load` loop over `Buildings` - **MODIFY**
- 380:390 - `Defs.Validate` building/production/conversion requirements - **DELETE** / **MODIFY**
- 395:400 - `FundsKey`, `ResourceCount`, `SuppliesIndex`, `FuelIndex` - **DELETE**
- 402:414 - `Defs.Table(...)` - **DELETE**
- 416:417 - `Defs.AsDouble(...)` - **DELETE**
- 419:440 - `Defs.BuildResourceTables()` - **DELETE**
- 450:450 - `Defs.Headquarters` - **MODIFY**
- 452:454 - `Defs.SiteKinds`, `Defs.SiteKind` - **MODIFY** (capture points / cities)
- 465:465 - `Defs.AllText` references to Buildings - **MODIFY**

---

### src\Core\Entity.cs
- 35:96 - `Building` class - **MODIFY** (keep as city/capture point entity; delete construction, repair, production, conversion, rally)
  - 41:42 - `Complete`, `Progress` - **DELETE**
  - 45:47 - `Queue`, `ProductionTime`, `Rally` - **DELETE**
  - 49:52 - `ProductionOn`, `Converting`, `RepairDebt` - **DELETE**
  - 58:58 - `IncomeActive` - **DELETE** or **MODIFY**
  - 83:83 - `Weapon => Complete ? ... : null` - **MODIFY** (remove Complete check)
  - 85:86 - `Providing` property - **DELETE**
- 99:191 - `Faction` class - **MODIFY** (delete stock, income, upkeep, supply cap/used, afford/pay/refund logic)
  - 112:120 - `Stock`, `IncomeMilli`, `UpkeepMilli`, `Short`, `incomeRemainder` - **DELETE**
  - 122:124 - `SupplyUsed`, `SupplyCap` - **DELETE**
  - 125:134 - `Faction` constructor resource allocation - **MODIFY**
  - 136:136 - `SupplyFree` - **DELETE**
  - 139:146 - `Funds`, `Income` - **DELETE**
  - 148:164 - `CanAfford(...)`, `Pay(...)`, `Refund(...)` - **DELETE**
  - 170:181 - `CollectIncome(...)` - **DELETE**
  - 183:190 - `FloorDiv(...)`, `IncomeRemainder(...)` - **DELETE**

---

### src\Core\Unit.cs
- 5:5 - `UnitOrder.Build`, `UnitOrder.Repair`, `UnitOrder.Refuel` enum values - **DELETE**
- 112:115 - `Unit.BuildDef`, `Unit.BuildX`, `Unit.BuildY`, `Unit.WorkTarget`, `Unit.Working` - **DELETE**
- 123:123 - `InitSupply()` call in constructor - **DELETE** / **MODIFY**
- 126:126 - `partial void InitSupply()` - **DELETE** / **MODIFY**
- 144:145 - `Unit.Immovable` check for `Working` - **MODIFY** (remove `Working`)

---

### src\Core\SupplyUnit.cs
- 1:17 - `SupplyTrip` enum - **DELETE**
- 20:62 - `Unit` partial class:
  - 23:23 - `Ammo` - **KEEP** (combat ammo remains in game)
  - 24:25 - `Fuel` - **DELETE**
  - 28:34 - `SupplyState`, `SupplyDepot`, `SupplyGroup`, `ReturnPos`, `ReturnOrder`, `TripTick`, `NextSupplyTrip` - **DELETE**
  - 37:39 - `RefuelTarget`, `RefuelClaimTick` - **DELETE**
  - 41:45 - `InitSupply()` - **MODIFY** (remove `Fuel = Def.FuelCap;`)
  - 55:59 - `UsesFuel`, `HasFuel`, `FuelRatio` - **DELETE**
  - 60:61 - `Resupplying` property - **DELETE**
- 64:72 - `Building` partial class - **DELETE** (`BuildersNow`, `RepairersNow`, `RepairBlocked`, `ActiveBuilders`)
- 74:82 - `Faction` partial class - **DELETE** (`SupplyStorageMilli`, `FuelStorageMilli`, `SuppliesSpentOnAmmo`, `ResupplyTripsStarted`)
- 84:90 - `World` partial class (`BattleAmmo`, `AmmoEnabled`) - **KEEP**

---

### src\Core\Supply.cs
- Entire file (lines 1-516) - **DELETE** or **MODIFY** (delete fuel, engineer refuel, auto-resupply depot trips, supply/fuel storage caps, builder stats; keep only unit ammo tracking if not moved to combat)
  - 22:33 - `SupplyWork` class - **DELETE**
  - 35:38 - `ResupplyIntervalTicks`, `TripCheckTicks`, `SupplyTicks` - **DELETE**
  - 45:52 - `ConsumeAmmo` - **MODIFY** (remove `AmmoRateAt` / `MaybeTriggerTrip` call)
  - 56:68 - `AmmoRateAt`, `FuelRateAt` - **DELETE**
  - 70:73 - `IsResupplyFacility` - **DELETE**
  - 74:142 - `Mix`, `CoverageKey`, `EnsureCoverage`, `Stamp` - **DELETE**
  - 147:179 - `UpdateSupply` - **DELETE** (or rewrite purely for combat ammo)
  - 182:211 - `PeriodicSupply` - **DELETE**
  - 214:226 - `AddFuel` - **DELETE**
  - 228:234 - `RefillRounds` - **DELETE**
  - 239:250 - `MaybeTriggerTrip` - **DELETE**
  - 253:303 - `StartTrips` - **DELETE**
  - 306:400 - `StepTrip`, `BeginRefill`, `FinishRefill`, `RestoreOrder`, `EndTrip` - **DELETE**
  - 404:416 - `TryAutoEngineerRefuel` - **DELETE**
  - 418:424 - `StartRefuel` - **DELETE**
  - 427:454 - `UpdateRefuel` - **DELETE**
  - 458:469 - `HashSupply` - **DELETE** / **MODIFY**
  - 471:489 - `WriteSupplyExtImpl` - **DELETE** / **MODIFY**
  - 491:515 - `ReadSupplyExtImpl` - **DELETE** / **MODIFY**

---

### src\Core\Commands\SupplyCommands.cs
- Entire file (lines 1-51) - **DELETE**
  - 4:4 - `RefuelCommand` - **DELETE**
  - 8:13 - `World.Refuel(...)` - **DELETE**
  - 15:24 - `World.ExecRefuel(...)` - **DELETE**
  - 27:32 - `World.AssignedBuilders(...)` - **DELETE**
  - 35:39 - `World.BuilderSpeed(...)` - **DELETE**
  - 42:50 - `World.AssignBuilderImpl(...)` - **DELETE**

---

### src\Core\Commands\GameCommand.cs
- 19:25 - `BuildCommand`, `RepairCommand`, `CancelConstructionCommand`, `TrainCommand`, `CancelTrainCommand`, `SetRallyCommand`, `ToggleProductionCommand` derived type attributes - **DELETE**
- 28:28 - `DebugFundsCommand` derived type attribute - **DELETE**
- 33:33 - `TransferResourceCommand` derived type attribute - **DELETE**
- 36:37 - `AssignBuilderCommand`, `RefuelCommand` derived type attributes - **DELETE**
- 59:66 - `BuildCommand`, `RepairCommand`, `CancelConstructionCommand`, `TrainCommand`, `CancelTrainCommand`, `SetRallyCommand`, `ToggleProductionCommand` records - **DELETE**
- 72:73 - `DebugFundsCommand` record - **DELETE**
- 80:81 - `TransferResourceCommand` record - **DELETE**

---

### src\Core\CommandExt.cs
- 3:4 - `CombatSetting.AutoResupply`, `ResupplyThresholdPct`, `MaxResupplyDistance`, `ReturnAfterResupply`, `AutoRefuel` - **DELETE**
- 9:10 - `AssignBuilderCommand` record - **DELETE**
- 17:17 - `World.AssignBuilderOrder(...)` - **DELETE**
- 24:28 - `ExecSetCombatSetting` cases for resupply/refuel - **DELETE**
- 35:41 - `World.AssignBuilder`, `World.ExecAssignBuilder` - **DELETE**
- 47:49 - `WriteCommandExtImpl` serializing resupply/refuel settings - **DELETE**
- 57:59 - `ReadCommandExtImpl` deserializing resupply/refuel settings - **DELETE**
- 67:69 - `HashCombatSettings` hashing resupply/refuel settings - **DELETE**

---

### src\Core\Commands.cs
- 26:28 - Command definitions for `"build"`, `"repair"`, `"refuel"` in `UnitCommands` - **DELETE**

---

### src\Core\World.cs
- 14:15 - `defs.ResourceCount` passed to `Faction` constructor - **MODIFY**
- 52:58 - `World` constructor initializing `Faction.Stock` with `defs.Economy.StartMilli` - **DELETE**
- 70:81 - `World.CreateStandard` spawning HQ as placeable building - **MODIFY** (cities/headquarters remain as pre-placed cities/capture entities)
- 81:81 - `b.IncomeActive = true;` - **DELETE** / **MODIFY**
- 86:86 - OrderBy checking `defs.Units[kv.Key].CanBuild` for worker spawn priority - **DELETE**
- 93:97 - Spawning `map.ResourceSites` and setting `r.IncomeActive = true;` - **MODIFY** (resource sites become capture points/cities, remove income)
- 98:98 - `world.UpdateEconomyStats();` call - **DELETE**
- 136:147 - `World.PlaceBuilding(...)` - **DELETE** or **MODIFY** (only used internally to place pre-existing map cities/capture sites; delete construction state/complete flag)
- 182:182 - `UpdateBuilding(b, dt);` - **MODIFY** (only update turret/capture; remove construction/repair/production)
- 184:184 - `UpdateEconomy(dt);` - **DELETE**
- 206:219 - `RefreshPaths()` comment and call on building construction - **MODIFY**
- 240:251 - `RemoveDead()` building occupant removal - **MODIFY**
- 300:301 - Building owner mask `bldgMask` - **KEEP** (needed for targeting buildings)

---

### src\Core\WorldEconomy.cs
- 12:138 - Entire Construction & Repair section - **DELETE**:
  - 14:14 - `WorkReach`
  - 16:60 - `UpdateBuild(Unit u, float dt)`
  - 61:73 - `UpdateRepair(Unit u, float dt)`
  - 75:86 - `ProgressConstruction(Building b, float dt)`
  - 88:113 - `ProgressRepair(Building b, float dt)`
  - 115:115 - `repairCost`
  - 118:124 - `ApproachBuilding(Unit u, Building b, float dt)`
  - 126:138 - `PushOutOf(Unit u, Building b)`
- 145:159 - `UpdateBuilding(Building b, float dt)` - **MODIFY** (delete construction, repair, queue production, and rally logic)
- 161:175 - `ExitPoint(Building b, Vector2 target)` - **DELETE**
- 184:242 - `UpdateCapture(float dt)` - **MODIFY** (keep capture progress and ownership change; remove `IncomeActive`)
- 245:253 - `OnOwnerChanged(Building b, int previousOwner)` - **MODIFY** (delete queue refund, queue clear, production time, rally)
- 256:261 - `UpdateEconomy(float dt)` - **DELETE**
- 263:264 - `TicksPerSecond`, `convCommit` - **DELETE**
- 270:324 - `UpdateEconomyStats()` - **DELETE**
- 333:371 - `UpdateVictory(float dt)` - **MODIFY** (check city ownership instead of non-capturable buildings / headquarters; remove `b.IncomeActive = false`)

---

### src\Core\WorldCommands.cs
- 5:5 - `CommandResult.NoFunds`, `CommandResult.NoSupply`, `CommandResult.QueueFull`, `CommandResult.InvalidPlacement`, `CommandResult.MissingRequirement` - **DELETE**
- 61:75 - `Execute` cases: `BuildCommand`, `RepairCommand`, `CancelConstructionCommand`, `TrainCommand`, `CancelTrainCommand`, `SetRallyCommand`, `ToggleProductionCommand` - **DELETE**
- 87:90 - `Execute` case: `DebugFundsCommand` - **DELETE**
- 92:93 - `Execute` cases: `AssignBuilderCommand`, `RefuelCommand` - **DELETE**
- 151:156 - `World.Build(...)` - **DELETE**
- 158:165 - `World.CheckBuild(...)` - **DELETE**
- 168:172 - `World.Repair(...)` - **DELETE**
- 174:177 - `World.CancelConstruction(...)` - **DELETE**
- 179:184 - `World.Train(...)` - **DELETE**
- 186:194 - `World.CheckTrain(...)` - **DELETE**
- 197:200 - `World.CancelTrain(...)` - **DELETE**
- 203:206 - `World.SetProduction(...)` - **DELETE**
- 209:210 - `World.ToggleProduction(...)` - **DELETE**
- 212:214 - `World.SetRally(...)` - **DELETE**
- 217:221 - `World.HasRequirements(...)` - **DELETE**
- 223:226 - `World.MissingRequirements(...)` - **DELETE**
- 228:239 - `World.CanPlaceAt(...)` - **DELETE**
- 351:353 - `ResetOrder` resetting `BuildDef`, `WorkTarget`, `Working` - **MODIFY** (remove those fields)
- 357:368 - `World.ExecBuild(...)` - **DELETE**
- 370:382 - `World.ExecRepair(...)` - **DELETE**
- 384:389 - `World.ExecCancelConstruction(...)` - **DELETE**
- 391:400 - `World.ExecTrain(...)` - **DELETE**
- 402:410 - `World.ExecCancelTrain(...)` - **DELETE**

---

### src\Core\WorldCombat.cs
- 86:96 - `UpdateUnitCore` cases: `UnitOrder.Build`, `UnitOrder.Repair`, `UnitOrder.Refuel` - **DELETE**
- 94:96 - Call to `UpdateRefuel(u, dt)` - **DELETE**
- 370:370 - Retaliate comment referring to workers (`autoAttack=false`) - **MODIFY**

---

### src\Core\WorldChecksum.cs
- 22:23 - Hashing `f.Stock`, `f.IncomeRemainder` - **DELETE**
- 28:30 - Hashing `f.Short`, `f.SupplyUsed`, `f.SupplyCap` - **DELETE**
- 33:33 - `HashSupply(ref h)` - **MODIFY** (remove fuel, depots, resupply trips, supply storage)
- 45:49 - Hashing unit `u.WorkTarget`, `u.BuildDef`, `u.BuildX`, `u.BuildY`, `u.Working` - **DELETE**
- 65:70 - Hashing building `b.Progress`, `b.Complete`, `b.Queue`, `b.ProductionTime` - **DELETE**
- 73:74 - Hashing building `b.IncomeActive`, `b.ProductionOn` - **DELETE**
- 80:81 - Hashing building `b.Rally` - **DELETE**

---

### src\Core\MapFile.cs
- 13:13 - Comment referencing resource sites - **MODIFY**
- 103:110 - Loading `Sites` and adding to `map.ResourceSites` - **MODIFY** (rename / treat as capture points / cities rather than resource sites)
- 148:154 - `Site` class - **MODIFY** (change comment/type away from resource buildings)

---

### src\Core\GameMap.cs
- 21:22 - `occupant`, `passable` arrays - **KEEP** (needed for cities / capture point footprint)
- 30:30 - `ResourceSites` field - **MODIFY** (rename / retain for capture points / cities)
- 31:36 - `SiteSize`, `SiteCenterTile`, `MinSiteGap`, `MinStartGap` - **MODIFY** / **KEEP** (for capture points)
- 55:57 - `IsWalkOnOccupied`, `IsBuildable` - **MODIFY** (`IsBuildable` can be deleted if player construction removed)
- 68:75 - `SetOccupant` - **KEEP** (for cities / capture points)
- 81:87 - `CanPlace` - **DELETE** (no dynamic building placement)
- 236:242 - Map generation placement of `"village"`, `"mine"`, `"oilfield"` - **MODIFY** (convert to neutral cities / capture points)
- 287:293 - Radial generation placement of `"village"`, `"mine"`, `"oilfield"` - **MODIFY** (convert to neutral cities / capture points)
- 300:309 - `NearBase` generating villages, mines, oilfields - **MODIFY**
- 327:355 - `AddSite`, `AddMirrored`, `ClearSite` - **MODIFY** / **KEEP** (for capture points)

---

### src\Core\Battle.cs
- 6:6 - Summary mentioning economy/buildings - **MODIFY**
- 94:94 - `d.CanBuild` check in spawn ordering (engineers) - **DELETE**
- 144:145 - Placing `map.ResourceSites` - **MODIFY** (capture points / cities)
- 146:146 - `world.UpdateEconomyStats();` call - **DELETE**

### src\Game.cs
- 23:23 - Demo option comment mentioning barracks construction (`보병 교전 + 병영 건설`): MODIFY (update/remove barracks demo comment)
- 39:39 - `enum CardMode { Main, BuildMenu }`: DELETE `BuildMenu` or whole enum if unused
- 74:74 - `PendingCommand` comment mentioning `repair / rally`: MODIFY
- 75:75 - `BuildingDef? pendingBuild;`: DELETE
- 76:76 - `CardMode cardMode = CardMode.Main;`: DELETE
- 80:80 - `HashSet<int> assignedEngineers`: DELETE
- 81:82 - `ResearchUnlocked` property referencing `research_lab` building: DELETE
- 183:185 - Initial camera centering on HQ/City building (`b.Def.Headquarters`, `b.Def.City`): MODIFY (cities stay, drop HQ check)
- 187:187 - Initial selection of HQ building: DELETE or MODIFY (cities only)
- 189:194 - `SetupDemo(hq)` trigger: DELETE
- 222:223 - `SetView` camera centering referencing `b.Def.Headquarters` / `b.Def.City`: MODIFY (keep city, drop HQ)
- 246:246 - `StatusText` check for `pendingBuild != null`: DELETE
- 267:267 - `PendingLabel` check for `rally`: DELETE
- 273:274 - `SelectedBuilding => Selection.Count == 1 ? Selection[0] as Building : null;`: MODIFY (only capturable city/point buildings remain, if selectable)
- 286:298 - `CommandCard()` build menu logic (`u.Def.CanBuild`, `CardMode.BuildMenu`, buildings iteration, `missing`, `CostLine`, `pendingBuild`): DELETE
- 308:324 - `CommandCard()` building commands (`!b.Complete`, `cancelbuild`, `b.Def.Produces`, `train:`, `f.SupplyFree`, `rally`): DELETE
- 340:345 - `CombatCard` resupply/refuel settings (`cb:resupply`, `cb:thresh`, `cb:dist`, `cb:return`, `cb:refuel`): DELETE
- 357:367 - `CombatPress` resupply/refuel cases: DELETE
- 397:398 - `CostLine` helper (resource cost / supply / build time): DELETE
- 460:461 - `Update()` resetting `cardMode` if no builder units and resetting `ResearchUnlocked`: DELETE
- 468:468 - Crosshair targeting check for `pendingBuild != null`: DELETE
- 477:477 - `HashSet<int> soundPendingBuild`: DELETE
- 494:494 - Shot sound for `su.Def.CanBuild => "tool_hit"`: DELETE
- 496:496 - Shot sound for `Building => "turret_gun"`: DELETE
- 511:526 - Unit production ready sound & building complete sounds (`soundPendingBuild`, `unit_ready`, `build_complete`): DELETE
- 560:566 - `PlayVoiceEvents` site capture checks: KEEP
- 638:640 - HUD clicks on formation / groups / command card: KEEP
- 641:641 - `Hud.QueueSlotAt` click -> `World.CancelTrain`: DELETE
- 650:650 - `if (pendingBuild != null) PlaceBuilding(shift);`: DELETE
- 672:672 - Right click cancel `pendingBuild`: DELETE
- 708:709 - Keyboard `Escape` handling for `pendingBuild`, `CardMode.BuildMenu`, `CancelConstruction`: DELETE
- 713:714 - Hotkey `G`: `Ctrl+G` toggles grid, plain `G` merges selected companies (`MergeSelected()`): KEEP (not tied to removed features)
- 716:721 - Keyboard `Tab` cycling to `CardTab.Research`: DELETE or MODIFY (remove research tab)
- 786:787 - `ClearSelection()` reset `cardMode` & cancel pending: MODIFY (remove `cardMode`)
- 799:799 - `AddToSelection` clearing `assignedEngineers`: DELETE reference to `assignedEngineers`
- 822:825 - `EntityAt` building check: MODIFY (only capturable cities/points stay)
- 871:872 - Comment mentioning "own building": MODIFY
- 882:886 - Box selection picking own buildings: DELETE
- 1114:1115 - `CancelPending()` resetting `pendingBuild`: DELETE
- 1124:1137 - `Press()` handling `bld:` and `train:`: DELETE
- 1140:1140 - `Press()` handling `build`: DELETE
- 1144:1145 - `Press()` handling `cancelbuild`, `repair`, `rally`, `refuel`: DELETE
- 1175:1180 - `ResolvePending()` cases for `repair` and `refuel`: DELETE
- 1181:1184 - `ResolvePending()` case for `rally`: DELETE
- 1195:1200 - `SmartCommand()` building rally point: DELETE
- 1212:1220 - `SmartCommand()` unit refuel by builder/engineer: DELETE
- 1221:1243 - `SmartCommand()` building repair, construct, and `assignedEngineers` logic: DELETE
- 1249:1276 - `PlacementTile` and `PlaceBuilding` methods: DELETE
- 1289:1293 - `ShowResult` results for `NoFunds`, `NoSupply`, `QueueFull`, `InvalidPlacement`, `MissingRequirement`: DELETE
- 1396:1401 - Console command `funds`: DELETE
- 1406:1431 - `SetupDemo` method (barracks, builder, HQ production queue, funds): DELETE
- 1450:1461 - `BuildSpotNear` method: DELETE
- 1571:1571 - `BuildingRenderer.DrawAll`: MODIFY (only cities/capture points)
- 1576:1576 - `DrawPlacementGhost()` call: DELETE
- 1608:1628 - `DrawPlacementGhost()` method: DELETE

---

### src\UI\Hud.cs
- 11:11 - `CardButton.CostLine`: DELETE
- 13:14 - `CardTab.Research`: DELETE (or MODIFY `CardTab`)
- 18:18 - Top bar comment mentioning `자금 · 인구`: MODIFY
- 145:154 - `QueueSlotAt` and `QueueSlot` methods (building production queue): DELETE
- 270:270 - `OrderName` infantry/armor front: KEEP
- 329:330 - Hotkey `G` button `S.Merge + " (G)"` and tooltip: KEEP
- 388:389 - Top bar calling `DrawPlayerInfo`: MODIFY
- 437:476 - Resource display constants and helpers (`ResColors`, `NetNegative`, `prevShort`, `EnsureResNames`, `CheckShortages`, `StorageCap` for supplies/fuel): DELETE
- 479:507 - `DrawPlayerInfo` (stocks, incomes, supply cap/used, shortages, storage cap): DELETE or MODIFY (if player stats completely replaced)
- 538:539 - `DrawInfo` calling `DrawBuilding(b, mouse)`: MODIFY (cities/points only)
- 570:573 - `SupplyBars(u)` / ammo & fuel bars in `DrawUnit`: DELETE fuel bar, keep or delete ammo per design (fuel definitely DELETE)
- 578:578 - Unit rank check `!u.Def.CanBuild`: MODIFY
- 593:594 - Unit upkeep and `FuelUse` display: DELETE
- 604:647 - `DrawBuilding` complete/progress, income lines, builders working (`b.ActiveBuilders`), `supply_plant` conv, rally: DELETE building construction/production/income/builder lines; KEEP capturable progress (lines 620-634)
- 648:664 - `DrawBuilding` production queue rendering (`b.Queue`, `b.ProductionTime`): DELETE
- 666:684 - `supply_plant` toggle rendering & hotkey `P`: DELETE
- 686:687 - `SupplyBars` returning `FuelRatio`: DELETE fuel part
- 689:689 - `FuelColor` definition: DELETE
- 781:782 - Company card ammo bar: KEEP (ammo) or check if fuel is referenced (fuel not referenced here)
- 834:835 - Multi-selection fuel bar (`fu`): DELETE
- 847:847 - `StateLabel` for `UnitOrder.Refuel`: DELETE
- 852:853 - `StateLabel` for `UnitOrder.Build`, `UnitOrder.Repair`: DELETE
- 869:886 - Card tabs handling `CardTab.Research` and placeholder text: DELETE
- 919:930 - `CostOf`, `Affordable`, `CostSegments` on `CardButton`: DELETE
- 953:963 - `DrawTooltipBox` overload with cost segments: DELETE
- 968:972 - `DrawIcon` handling `bld:`: DELETE

---

### src\UI\Input.cs
- Entire file: KEEP (all generic input wrappers)

---

### src\UI\Strings.cs
- 26:26 - `Hint1` mentioning repair (`/ 수리`): MODIFY
- 28:28 - `Hint3` mentioning `G: 병합`: KEEP
- 42:43 - `StateBuild`, `StateRepair`: DELETE
- 45:48 - `EngineersWorking`, `CmdRefuel`, `TipRefuel`, `StateRefuel`: DELETE
- 49:51 - `StateResupplyGo`, `StateResupplying`, `StateReturning`: DELETE
- 68:70 - `Funds`, `Supply` ("인구"), `PerSec`: DELETE
- 89:89 - `Contested = "경합 중 — 수입 정지"`: MODIFY (keep for contest, remove income reference)
- 91:95 - `Income`, `SupplyProvides`, `Constructing`, `Producing`, `Cost`: DELETE
- 98:98 - `Rally`: DELETE
- 100:105 - `NoFunds`, `NoSupply`, `QueueFull`, `InvalidPlacement`, `MissingRequirement`, `Requires`: DELETE
- 107:107 - `BuildPending`: DELETE
- 111:112 - `VictorySub`, `DefeatSub` referencing buildings ("모든 건물을 파괴했습니다"): MODIFY
- 135:140 - `CmdBuild`, `TipBuild`, `CmdRepair`, `TipRepair`: DELETE
- 141:146 - `CmdRally`, `TipRally`, `CmdCancel`, `TipCancelBuild`, `TipQueue`: DELETE
- 165:165 - `ConsoleFunds`: DELETE
- 170:171 - `ConsoleHelp2` (spawn mentioning population), `ConsoleHelp3` (`funds`): DELETE
- 206:209 - `SupportSend`, `All`, `SendFunds`, `SentFunds`: DELETE
- 214:214 - `SupportNote` mentioning population and production: DELETE
- 227:227 - `EvFunds`: DELETE
- 268:270 - `BoardFunds`, `BoardSupply`: DELETE
- 272:272 - `BoardBuildings`: DELETE
- 286:290 - `ResSteel`, `ResFuel`, `ResSupplies`, `ResManpower`, `ShortMark`: DELETE
- 294:298 - `Upkeep`, `FuelUse`, `MovingOnly`, `ShortSupplies`, `ShortFuel`: DELETE
- 299:304 - `PlantOn`, `PlantOff`, `PlantToggle`, `PlantConv`, `SendRes`, `SentRes`: DELETE
- 305:306 - `ResourceLabel`, `Provides`: DELETE
- 358:358 - `Merge` ("병합"): KEEP
- 373:373 - `MergeTip` ("선택한 중대들을 한 연대로 병합 (G)"): KEEP
- 427:427 - `TabResearch`: DELETE
- 430:433 - `TabTipResearch`, `TabTipResearchLocked`, `ResearchPlaceholder`, `ResearchPlaceholderSub`: DELETE
- 434:438 - `CbResupply`, `CbThreshold`, `CbDistance`, `CbReturn`, `CbRefuel`: DELETE
- 447:451 - `CbTipResupply`, `CbTipThreshold`, `CbTipDistance`, `CbTipReturn`, `CbTipRefuel`: DELETE
- 462:462 - `FuelLabel`: DELETE
- 467:468 - `AssignNone`, `AssignNotice` (engineers): DELETE

---

### src\UI\MainMenu.cs
- Entire file: KEEP (all setup, battle test presets, replay, and save/load menu flows stay; no direct building/resource/production/engineer code)

---

### src\UI\BattlePanel.cs
- Entire file: KEEP (battle test HUD tracking units/kills/losses)

---

### src\UI\SpectatorPanel.cs
- 78:84 - `Tally()` counting `buildings` and `points`: MODIFY (drop `buildings`, keep capturable `points`)
- 151:151 - Board sorting by `SupplyUsed`: DELETE or MODIFY
- 214:229 - `DrawRow` displaying `BoardFunds`, `BoardSupply`, `BoardBuildings`: DELETE funds, supply, and buildings items

---

### src\UI\CommandConsole.cs
- Entire file: KEEP (generic console window)

---

### src\UI\Icons.cs
- 36:43 - `build` (hammer) and `repair` (wrench): DELETE
- 47:50 - `rally` (flag): DELETE
- 51:54 - `cancel` (X): KEEP (used elsewhere)

---

### src\Render\BuildingRenderer.cs
- 20:25 - Ghost rendering for fogged buildings: MODIFY (capturable cities/points only)
- 47:48 - `if (b.Def.Capturable) DrawResource(b, r, cam); else DrawBody(...)`: MODIFY (only capturable buildings exist)
- 50:57 - Rally line rendering: DELETE
- 63:65 - Construction progress & production queue progress bars: DELETE
- 109:154 - `DrawBody` shapes for `supply_plant`, `barracks`, `depot`, `factory`, `turret`: DELETE
- 160:169 - `DrawScaffold` (building construction scaffolding): DELETE
- 178:184 - `building.*.gun` turret rendering: DELETE
- 200:265 - Shapes for cities, villages, mines, oilfields: KEEP / MODIFY (cities/points stay, remove mine/oilfield if resources are removed)
- 266:291 - `DrawResource`: KEEP (capturable point / city capture ring rendering)

---

### src\Render\SimView.cs
- 17:19 - `FuelRatio(Unit u, out float ratio)`: DELETE

---

### src\Render\Minimap.cs
- 49:57 - `SiteColor` for mine/oilfield: MODIFY (keep city/metropolis/village, delete mine/oilfield if resource nodes removed)
- 71:74 - Special markers for mine/oilfield: MODIFY
- 84:91 - Minimap drawing of non-capturable buildings (`world.Buildings` where `!b.Def.Capturable`): DELETE

---

### src\Render\MapRenderer.cs
- Entire file: KEEP (terrain chunk rendering and grid)

---

### src\Core\Snapshot\WorldSnapshot.cs
- 40:41 - `WriteSupplyExtImpl`, `ReadSupplyExtImpl`: DELETE
- 46:47 - `WriteSupplyExt`, `ReadSupplyExt`: DELETE
- 73:73 - `supl` section write (`WriteSupplyExt`): DELETE
- 93:93 - `o.Tag("SUPL"); o.Raw(supl.Buffer.Span);`: DELETE
- 140:155 - `WriteEntities` for Buildings: `Complete`, `Progress`, `Queue`, `ProductionTime`, `Rally`, `ProductionOn`, `Converting`, `RepairDebt`: DELETE fields
- 178:180 - `WriteLinks` for Units: `BuildDef`, `BuildX`, `BuildY`, `WorkTarget`, `Working`: DELETE fields
- 266:266 - `r.Tag("SUPL"); world.ReadSupplyExt(r, ctx);`: DELETE
- 335:361 - `ReadEntities` for Buildings: reading `Complete`, `Progress`, `Queue`, `ProductionTime`, `Rally`, `ProductionOn`, `Converting`, `RepairDebt`: DELETE fields
- 407:407 - Unit order check `order > (byte)UnitOrder.Refuel`: MODIFY (remove `Refuel`, `Build`, `Repair`)
- 421:425 - `ReadLinks` reading `BuildDef`, `BuildX`, `BuildY`, `WorkTarget`, `Working`: DELETE fields

---

### src\Core\Snapshot\FactionSnapshot.cs
- 10:10 - `w.I(SupplyUsed); w.I(SupplyCap);`: DELETE
- 11:15 - `w.I(Stock.Length)` and loop writing `Stock`, `IncomeMilli`, `UpkeepMilli`, `Short`, `incomeRemainder`: DELETE
- 23:23 - `SupplyUsed = r.I(); SupplyCap = r.I();`: DELETE
- 24:30 - Reading `Stock`, `IncomeMilli`, `UpkeepMilli`, `Short`, `incomeRemainder`: DELETE

---

### src\Core\Snapshot\Snapshot.cs
- 51:51 - `SnapshotHeader.Buildings`: KEEP or MODIFY (if only tracking city count)
- 82:82 - `FormatVersion = 6`: MODIFY (increment due to schema change)
- 120:120 - `h.Buildings = world.Buildings.Count;`: KEEP or MODIFY

---

### src\Core\Snapshot\MapSnapshot.cs
- 17:18 - `ResourceSites` write: MODIFY (only capturable cities/points stay; no resource nodes)
- 47:48 - `ResourceSites` read: MODIFY
- 63:69 - `RestoreOccupant`: MODIFY (capturable cities/points only)

### `src\AI\StandardAi.cs`
- `src\AI\StandardAi.cs:8` - `int WorkerTarget = 4,` - DELETE: 일꾼 유지 설정 제거
- `src\AI\StandardAi.cs:17-18` - `int MaxBarracks = 6, int MaxTurrets = 4,` - DELETE: 병영/포탑 설정 제거
- `src\AI\StandardAi.cs:60-64` - 요약 주석 빌드/일꾼/병영/보급고/포탑 설명 - MODIFY: 건설/생산 내용 제거 및 순수 전투 요약으로 수정
- `src\AI\StandardAi.cs:79` - `Economy(w, s);` - DELETE: 경제/건설/생산 호출 제거
- `src\AI\StandardAi.cs:80` - `SetRallies(w, s, s.Gather);` - DELETE: 생산 건물 랠리포인트 호출 제거
- `src\AI\StandardAi.cs:81-85` - `MaintainConstruction`, `MaintainRepairs` 호출 블록 - DELETE: 건물 건설/수리 호출 제거
- `src\AI\StandardAi.cs:89` - `LeashWorkers(w, s);` - DELETE: 일꾼 리쉬 제거
- `src\AI\StandardAi.cs:143-203` - `void Economy(World w, StandardAiState s)` - DELETE: 자금, 건설, 생산(TrainWorkers, TrainTanks, TrainArmy, PlanSupplies, ManagePlants) 전체
- `src\AI\StandardAi.cs:205-220` - `Vector2 TurretAnchor(World w, StandardAiState s, int index)` - DELETE: 포탑 배치 위치 계산 제거
- `src\AI\StandardAi.cs:222-244` - `UnitDef PickUnit(World w, StandardAiState s)` - DELETE: 자원 및 생산용 병종 선택 로직 제거
- `src\AI\StandardAi.cs:306` - `bool plansTanks = ...` - MODIFY: 공장(Factory) 유무 대신 전차 유닛 보유 여부 기반으로 수정
- `src\AI\StandardAi.cs:311` - `D.SitePenalty(site.Def, plansTanks)` - MODIFY: 자원/공장 기반 페널티 제거 또는 거점 가치 기반으로 수정
- `src\AI\StandardAi.cs:318` - `KnownEnemyTurretNear(s, site.Pos, 11)` - DELETE / MODIFY: 적 포탑 확인 로직 제거 또는 유지 여부 결정
- `src\AI\StandardAi.cs:464` - `(m.Def.Weapon != null ? 4 : 0) + (m.Def.Headquarters ? -3 : 0)` - MODIFY: 본부/건물 무기 가중치 대신 거점/유닛 가중치로 수정

---

### `src\AI\AiHelpers.cs`
- `src\AI\AiHelpers.cs:8-10` - `public readonly BuildingDef Hq, Barracks, Depot, Turret; BuildingDef? Factory, Plant;` - DELETE: 제거된 건물 정의 필드 제거
- `src\AI\AiHelpers.cs:11` - `public readonly UnitDef Worker...` - DELETE: Worker 정의 필드 제거
- `src\AI\AiHelpers.cs:15` - `public readonly int Steel, Fuel, Supplies;` - DELETE: 제거된 자원 인덱스 필드 제거
- `src\AI\AiHelpers.cs:23-26` - 자원 및 Worker 캐싱 코드 - DELETE: 자원/일꾼 초기화 제거
- `src\AI\AiHelpers.cs:36-41` - Barracks, Depot, Plant, Turret, Factory, Tank 캐싱 코드 - DELETE: 건물 정의 검색/캐싱 제거
- `src\AI\AiHelpers.cs:51` - `public bool IsArmyProducer(BuildingDef b)` - DELETE: 생산 건물 판별 메서드 제거
- `src\AI\AiHelpers.cs:54-60` - `public float SitePenalty(BuildingDef site, bool hasFactory)` - MODIFY: Steel/Fuel 자원 수입 체크 및 공장 체크 제거, 도시/거점만 남김
- `src\AI\AiHelpers.cs:118-119` - `public Building? Hq; public Vector2 Home;` - MODIFY: 사령부 건물 대신 거점/시작 위치 중심으로 수정
- `src\AI\AiHelpers.cs:132` - `public readonly List<Unit> Units = new(), Workers = new(), Army = new();` - MODIFY: Workers 리스트 제거
- `src\AI\AiHelpers.cs:133` - `public readonly List<Building> Buildings = new(), Barracks = new(), Factories = new();` - DELETE: 일반 건물/병영/공장 리스트 제거
- `src\AI\AiHelpers.cs:135` - `public readonly List<Building> Cities = new(), Plants = new();` - MODIFY: Plants(공장) 제거, Cities 유지
- `src\AI\AiHelpers.cs:136` - `public long[] Spent = Array.Empty<long>();` - DELETE: 자원 지출 예약 배열 제거
- `src\AI\AiHelpers.cs:138-140` - `KnownEnemy`, `SpotFailTick`, `Reserved` - DELETE: 건물 기억/건설 부지 실패/건설 비용 예약 필드 제거
- `src\AI\AiHelpers.cs:141` - `public long LastRepairTick ...` - DELETE: 수리 틱 필드 제거
- `src\AI\AiHelpers.cs:216-224` - `Refresh` 내 Workers/Buildings/Plants/Reserved/Spent 초기화 - DELETE: 자원/건설 캐시 초기화 제거
- `src\AI\AiHelpers.cs:229-236` - `u.Def.CanBuild`, `UnitOrder.Build` 비용 예약 로직 - DELETE: 일꾼/건설 비용 예약 로직 제거
- `src\AI\AiHelpers.cs:246-265` - 건물 수집 및 KnownEnemy 기록 로직 - MODIFY: 일반 건설 건물/생산 건물/변환 공장 처리 제거 (Capturable 거점/도시만 유지)
- `src\AI\AiHelpers.cs:276` - `s.Gather = WalkablePoint(w, s.Home + s.Front * (D.Hq.Size / 2f + 6));` - MODIFY: Hq 크기 기반 집결지 계산 수정
- `src\AI\AiHelpers.cs:288-299` - `ForgetStale(World w, TState s)` - DELETE: 일반 적 건물 망각 로직 제거
- `src\AI\AiHelpers.cs:318-323` - `UpdateTargetFaction` 내 `KnownEnemy` 거리 계산 - MODIFY: 적 건물 거리 판정 제거
- `src\AI\AiHelpers.cs:411-420` - `CountBuildings` - DELETE: 건물 수 및 일꾼 건설 대기 집계 제거
- `src\AI\AiHelpers.cs:423-430` - `Queued` - DELETE: 건물 생산 대기열 집계 제거
- `src\AI\AiHelpers.cs:444` - `!u.Def.CanBuild` - MODIFY: 일꾼 제외 조건 제거
- `src\AI\AiHelpers.cs:449-454` - `KnownEnemyTurretNear` - DELETE: 적 포탑 기억 탐색 제거
- `src\AI\AiHelpers.cs:457-514` - `FindSpot`, `RingOffsets` - DELETE: 건물 건설 부지 탐색 알고리즘 제거
- `src\AI\AiHelpers.cs:516-527` - `NearestIdleWorker` - DELETE: 일꾼 탐색 메서드 제거
- `src\AI\AiHelpers.cs:530-545` - `TryBuild` - DELETE: 건물 건설 지시 메서드 제거
- `src\AI\AiHelpers.cs:548-554` - `CanPayOthers` - DELETE: 자원 지불 가능 여부 메서드 제거
- `src\AI\AiHelpers.cs:556-559` - `SpendOthers` - DELETE: 자원 예약 차감 메서드 제거
- `src\AI\AiHelpers.cs:562-577` - `MaintainConstruction` - DELETE: 미완공 건물 이어짓기 유지 메서드 제거
- `src\AI\AiHelpers.cs:580-598` - `MaintainRepairs` - DELETE: 건물 수리 메서드 제거
- `src\AI\AiHelpers.cs:602-616` - `TrainWorkers` - DELETE: 일꾼 생산 메서드 제거
- `src\AI\AiHelpers.cs:618-625` - `Queued(..., List<Building> where)` - DELETE: 생산 대기열 개수 조회 제거
- `src\AI\AiHelpers.cs:628-640` - `TrainArmy` - DELETE: 병영 병력 생산 메서드 제거
- `src\AI\AiHelpers.cs:643-657` - `TrainTanks` - DELETE: 군수공장 전차 생산 메서드 제거
- `src\AI\AiHelpers.cs:663-669` - `NeedsPlant` - DELETE: 보급품 변환 공장 필요 판정 제거
- `src\AI\AiHelpers.cs:676-698` - `PlanSupplies`, `SupplyPlan` - DELETE: 보급품 유지비 및 공장 계획 제거
- `src\AI\AiHelpers.cs:705-718` - `ManagePlants` - DELETE: 보급품 공장 가동/정지 토글 제거
- `src\AI\AiHelpers.cs:721-727` - `SetRallies` - DELETE: 병영/공장 집결지 설정 제거
- `src\AI\AiHelpers.cs:781` - `u.Order != UnitOrder.Build && u.Order != UnitOrder.Repair` - MODIFY: 건설/수리 오더 체크 제거
- `src\AI\AiHelpers.cs:786-788` - `LeashWorkers` - DELETE: 일꾼 리쉬 메서드 제거

---

### `src\AI\BattleAi.cs`
- `src\AI\BattleAi.cs:1-255` - KEEP: 자원/건설/공병 없이 순수 전투 유닛 조작만 수행

---

### `src\AI\PassiveAi.cs`
- `src\AI\PassiveAi.cs:1-64` - DELETE: 일꾼/병영/보급고/공장/포탑 건설 및 생산 기반 AI 전체 제거

---

### `src\AI\StandardAiDiplomacy.cs`
- `src\AI\StandardAiDiplomacy.cs:95-103` - `SupportAlly` - DELETE: 자금 송금(SendResource) 지원 메서드 제거
- `src\AI\StandardAiDiplomacy.cs:80` - `SupportAlly(w, s, f);` - DELETE: 자금 지원 호출 제거
- `src\AI\StandardAiDiplomacy.cs:112` - `w.Factions[f].SupplyUsed` - MODIFY: 인구수 기반 전력 계산 대신 전투 유닛 수 기반으로 수정

---

### `src\SelfTest.cs`
- `src\SelfTest.cs:24` - `TestEconomyAndProduction(opt); TestConstruction(opt); Tests.SupplySelfTest.Run(opt, Check);` - DELETE: 자원/생산/건설/보급 테스트 호출 제거
- `src\SelfTest.cs:34-35` - `TestEconomyAndProduction(opt); TestConstruction(opt);` - DELETE: 경제/생산 및 건설 테스트 호출 제거
- `src\SelfTest.cs:41-42` - `Tests.SupplySelfTest.Run(opt, Check);` - DELETE: 보급/연료/공병 테스트 실행 제거
- `src\SelfTest.cs:91-94` - `Rich(Faction f)` - DELETE: 자원 채우기 헬퍼 제거
- `src\SelfTest.cs:99-109` - `FreeSpot` - DELETE: 건물 건설 부지 탐색 제거
- `src\SelfTest.cs:164-166` - 공병 2기 및 인력/수입 불변식 검증 - DELETE: 공병 수 및 인력/수입 검증 제거
- `src\SelfTest.cs:203` - `Check(Enumerable.Range(1, 4).All(f => w.Units.Count(u => u.Owner == f && u.Def.CanBuild) == 2), ...)` - DELETE: 공병 검증 제거
- `src\SelfTest.cs:275-296` - `TestFixedPoint(GameOptions opt)` - DELETE: 자원/수입 고정소수점 테스트 전체 제거
- `src\SelfTest.cs:334` - `w.Issue(new DebugFundsCommand(...))` - DELETE: 자금 치트 테스트 제거
- `src\SelfTest.cs:346-381` - `TestEconomyAndProduction(GameOptions opt)` - DELETE: 자금, 인구, 일꾼/보병 생산 대기열 및 환불 테스트 전체 제거
- `src\SelfTest.cs:383-429` - `TestConstruction(GameOptions opt)` - DELETE: 보급고/병영 건설, 자원 부족, 수리, 다인 공사 테스트 전체 제거
- `src\SelfTest.cs:466-486` - 포탑 및 적 보급고 파괴 테스트 - DELETE / MODIFY: 포탑 및 보급고 건물 파괴 테스트 제거
- `src\SelfTest.cs:488-513` - 군수공장 건설 및 전차 생산 테스트 - DELETE: 군수공장 요구조건/건설/전차 생산 검증 제거
- `src\SelfTest.cs:551-552` - `ai.CommandLog.Any(r => r.Command is BuildCommand ...)` - DELETE: AI 군수공장 건설 검증 제거
- `src\SelfTest.cs:567` - `w.Faction(1).Income ... SupplyCap` - DELETE / MODIFY: 자원 수입 및 인구 상한 검증 제거
- `src\SelfTest.cs:590-598` - 적 보급고 건설 후 HQ 파괴 테스트 - DELETE: 보급고 기반 생존 테스트 제거

---

### `src\Tests\SupplySelfTest.cs`
- `src\Tests\SupplySelfTest.cs:1-646` - DELETE: 탄약/보급소 충전/연료/연료탱크/공병 주유/다인 건설/수리/공병 배정 등 제거된 기능 전용 테스트 파일 전체 제거

---

### `src\Tests\AiSelfTest.cs`
- `src\Tests\AiSelfTest.cs:39` - `public int MaxBarracks, MaxDepots, MaxTurrets, MaxArmy, MaxSites, MaxBuildings;` - MODIFY: 건물 통계(MaxBarracks, MaxDepots, MaxTurrets, MaxBuildings) 제거
- `src\Tests\AiSelfTest.cs:117-118` - 일꾼, 병영, 포탑 통계 출력 - DELETE: 일꾼/병영/포탑 출력 제거
- `src\Tests\AiSelfTest.cs:124-136` - 병영/보급고/포탑 카운트 - DELETE: 건물 수집 로직 제거
- `src\Tests\AiSelfTest.cs:160` - `CountBuildings` - DELETE: 일반 건물 집계 제거
- `src\Tests\AiSelfTest.cs:194-218` - corners4 보급품/강철 재고 및 공장 검증 - DELETE: 보급품/강철 재고 및 공장 부재 검증 제거
- `src\Tests\AiSelfTest.cs:243` - `check(ss.MaxBarracks >= 1, ...)` - DELETE: 병영/보급고 건설 검증 제거
- `src\Tests\AiSelfTest.cs:248` - `check(ps.MaxBarracks >= 1 ...)` - DELETE: 수비 AI 병영 검증 제거
- `src\Tests\AiSelfTest.cs:268` - `check(st[1].MaxBarracks >= 1 && st[2].MaxBarracks >= 1, ...)` - DELETE: 양측 병영 건설 검증 제거
- `src\Tests\AiSelfTest.cs:287` - `passive` AI 포함 - MODIFY: PassiveAi 제거에 따른 AI 목록 수정
- `src\Tests\AiSelfTest.cs:335` - `check(withBarracks == players, ...)` - DELETE: 병영 건설 검증 제거
- `src\Tests\AiSelfTest.cs:364` - `check(withBarracks == players, ...)` - DELETE: 16인 병영 건설 검증 제거

---

### `src\Tests\SnapshotSelfTest.cs`
- `src\Tests\SnapshotSelfTest.cs:78` - `w.Factions[f].Stock[i] = Fixed.From(20000);` - DELETE: 자원 지급 제거
- `src\Tests\SnapshotSelfTest.cs:85` - `w.Train(1, hq, Defs.Units["worker"]);` - DELETE: 일꾼 생산 명령 제거
- `src\Tests\SnapshotSelfTest.cs:87-93` - `w.Build(1, worker, bd, x, y)` - DELETE: 공병 건물 건설 명령 제거
- `src\Tests\SnapshotSelfTest.cs:114-119` - 생산 건물 집결지/생산 명령 - DELETE: 생산 건물 랠리/생산 제거
- `src\Tests\SnapshotSelfTest.cs:126` - `w.CancelConstruction(1, b);` - DELETE: 건설 취소 명령 제거
- `src\Tests\SnapshotSelfTest.cs:659` - `Audit1(typeof(Faction), ... Stock IncomeMilli UpkeepMilli Short incomeRemainder SupplyUsed SupplyCap)` - MODIFY: 자원/유지비/인구 필드 감사 목록 수정

---

### `src\Tests\StressTest.cs`
- `src\Tests\StressTest.cs:23-24` - 시나리오 `R 재보급러시` 설명 - DELETE: 보급 여정 시나리오 설명 제거
- `src\Tests\StressTest.cs:42-43` - `SupplyMs`, `Trips`, `Refilled` - DELETE: 보급 통계 필드 제거
- `src\Tests\StressTest.cs:146` - `World.SupplyProf = 0;` - DELETE: 보급 프로파일링 초기화 제거
- `src\Tests\StressTest.cs:193-195` - `SupplyMs`, `Trips`, `Refilled` 집계 - DELETE: 보급 통계 수집 제거
- `src\Tests\StressTest.cs:361-390` - `case 'R':` (재보급 러시 시나리오) - DELETE: 보급소 배치 및 재보급 여정 스트레스 시나리오 전체 제거
- `src\Tests\StressTest.cs:466-467` - `world.Faction(f).Stock ...` - DELETE: 세력 자원 지급 제거
- `src\Tests\StressTest.cs:502-504` - 시나리오 R 출력 블록 - DELETE: 보급 통계 콘솔 출력 제거

---

### `src\Tests\UiFuzz.cs`
- `src\Tests\UiFuzz.cs:44` - `Building? aimBuilding;` - DELETE / MODIFY: 공병 배정 대상 건물 필드 제거
- `src\Tests\UiFuzz.cs:177-179` - 콘솔 명령 `funds`, `spawn worker`, `select worker` - DELETE: 자금/일꾼 콘솔 명령어 제거
- `src\Tests\UiFuzz.cs:212-214` - `Shift 공병 배분` 주석 - DELETE: 공병 배정 관련 설명 제거
- `src\Tests\UiFuzz.cs:231-240` - `shiftBurst` 및 `shift-assign-click` (공병 배정) - DELETE: 공병 배정 입력 주입 제거
- `src\Tests\UiFuzz.cs:333` - `생산 대기열` 클릭 - DELETE: 생산 대기열 UI 클릭 주입 제거
- `src\Tests\UiFuzz.cs:382-387` - 자금 치트 지급 및 건물 생산 대기열 채우기 - DELETE: 자금 지급 및 유닛 훈련 명령 제거
- `src\Tests\UiFuzz.cs:415-416` - 생산 건물 선택, 미완공 건물 선택 - DELETE: 생산/건설 중 건물 선택 시나리오 제거
- `src\Tests\UiFuzz.cs:448-456` - case 12: 일꾼 건물 짓기 - DELETE: 일꾼 건설 시나리오 제거
- `src\Tests\UiFuzz.cs:457-464` - case 13: 생산 대기열 채우기/취소 및 자금 치트 - DELETE: 생산 대기열 및 자금 시나리오 제거
- `src\Tests\UiFuzz.cs:487-498` - case 21: 공병 여러 기 + 미완공 건물 배정 - DELETE: 다인 공병 배정 시나리오 제거
- `src\Tests\UiFuzz.cs:506-508` - case 19: 일반 건물 배치 - MODIFY: 일반 건설 가능 건물 배치 제거, 점령 거점만 유지

---

### `src\Tests\ReplaySelfTest.cs`
- `src\Tests\ReplaySelfTest.cs:46-53` - `BuildCommand`, `RepairCommand`, `CancelConstructionCommand`, `TrainCommand`, `CancelTrainCommand`, `ToggleProductionCommand`, `SetRallyCommand` 표본 - DELETE: 제거된 건설/수리/생산/랠리 명령 표본 제거
- `src\Tests\ReplaySelfTest.cs:56` - `new DebugFundsCommand(2, 0, -5_000)` - DELETE: 자금 치트 명령 표본 제거
- `src\Tests\ReplaySelfTest.cs:62` - `new TransferResourceCommand(1, 2, 0, 150_500)` - DELETE: 자원 송금 명령 표본 제거
- `src\Tests\ReplaySelfTest.cs:65-66` - `AssignBuilderCommand`, `RefuelCommand` 표본 - DELETE: 공병 배정 및 주유 명령 표본 제거
- `src\Tests\ReplaySelfTest.cs:97-101` - 집결점(SetRallyCommand) 직렬화 검증 - DELETE: 랠리포인트 직렬화 테스트 제거
- `src\Tests\ReplaySelfTest.cs:104-107` - `TrainCommand` CommandRecord 직렬화 검증 - MODIFY: 이동/공격 등 전투 명령으로 교체
- `src\Tests\ReplaySelfTest.cs:145-153` - 자금 사라짐에 따른 `TrainCommand` 거부 검증 - DELETE: 자금 및 생산 명령 검증 제거
- `src\Tests\ReplaySelfTest.cs:164-166` - 인구 상한 검증 (`f.SupplyUsed = f.SupplyCap`) - DELETE: 인구 상한 검증 제거
- `src\Tests\ReplaySelfTest.cs:209-215` - `SetRally`, `Train`, `CancelTrain` 수동 명령 - DELETE: 수동 생산/랠리 명령 주입 제거
- `src\Tests\ReplaySelfTest.cs:231-232` - `SetRallyCommand` 검증 - DELETE: 리플레이 내 집결점 명령 검증 제거

---

### `src\Tests\CoreV2SelfTest.cs`
- `src\Tests\CoreV2SelfTest.cs:1-276` - DELETE: 5자원, 유지비, 연료, 공장 변환, 소도시 공병 생산 및 환불, 보급고 생존 등 제거된 시스템 검증 파일 전체 제거

---

### `src\Tests\DetCheck.cs`
- `src\Tests\DetCheck.cs:44` - `건물 포탑 하나` 설명 주석 - MODIFY: 포탑 제거 설명 수정
- `src\Tests\DetCheck.cs:48-68` - `ScriptBattle` - KEEP: 순수 지형 위 유닛 간 충돌 스크립트 유지

---

### `src\Tests\Invariants.cs`
- `src\Tests\Invariants.cs:53-63` - `w.UpdateEconomyStats();`, `fa.Stock[r] < 0`, `fa.SupplyUsed` - DELETE: 자원 음수 재고 및 인구 집계 불변식 검증 제거
- `src\Tests\Invariants.cs:89` - `if (u.Working && u.WorkTarget == null) Fail(...)` - DELETE: 일꾼 작업 대상 불변식 검증 제거
- `src\Tests\Invariants.cs:90` - `u.Order != UnitOrder.Repair` - MODIFY: 수리 명령 예외 처리 제거
- `src\Tests\Invariants.cs:100` - `u.HasFuel` - MODIFY: 연료 체크 제거
- `src\Tests\Invariants.cs:122-134` - 건물 건설 진행도(`b.Progress`), 대기열(`b.Queue`), 생산 진행도(`b.ProductionTime`), 랠리(`b.Rally`) 검증 - DELETE: 건물 건설/생산 불변식 검증 제거
- `src\Tests\Invariants.cs:139` - `if (b.IncomeActive && b.Owner == 0) Fail(...)` - DELETE / MODIFY: 자원 수입 검증 제거 (점령 상태만 확인)

---

### `src\Tests\FactionsSelfTest.cs`
- `src\Tests\FactionsSelfTest.cs:63` - 광산/유전 포함 시설 목록 검증 - MODIFY: 광산/유전 대신 남아있는 도시/거점 기준으로 수정 또는 제거

---

### `src\Tests\DiplomacySelfTest.cs`
- `src\Tests\DiplomacySelfTest.cs:50` - `int aid = ev.Count(e => e.Kind == DiploEventKind.ResourceSent);` - DELETE: 자원 지원 송금 이벤트 집계 제거
- `src\Tests\DiplomacySelfTest.cs:240-254` - `Transfers` 내 `w.SendResource` 자금 송금 테스트 - DELETE: 자금 송금 검증 제거
- `src\Tests\DiplomacySelfTest.cs:256-267` - `Transfers` 내 인구 초과 및 생산 불가(`Train` -> `NoSupply`) 검증 - DELETE: 인구 초과 및 생산 검증 제거
- `src\Tests\DiplomacySelfTest.cs:289-291` - 동맹 시설 수입 정지 검증 (`site.IncomeActive`) - DELETE: 시설 수입 플래그 검증 제거
- `src\Tests\DiplomacySelfTest.cs:346` - `w.SendResource(1, 3, 0, Fixed.From(77.7));` - DELETE: 리플레이 외교 테스트 내 자금 송금 명령 제거
- `src\Tests\DiplomacySelfTest.cs:354` - `TransferResourceCommand` - DELETE: 리플레이 검증 내 자원 송금 명령 체크 제거
- `src\Tests\DiplomacySelfTest.cs:362` - `passive` AI 생성 - MODIFY: PassiveAi 제거에 따른 세력 컨트롤러 수정

---

### `src\Tests\BalanceTool.cs`
- `src\Tests\BalanceTool.cs:26-34` - `Weights`, `CostEq(Defs defs, UnitDef u)` - DELETE / MODIFY: 5자원 가중치 합산 비용 계산 제거 (기본 전투 가치 또는 고정 비용으로 단순화)
- `src\Tests\BalanceTool.cs:78-79` - `SustainX`, `SustainY` (보급품 소모 환산 비용) - DELETE: 보급품 소모 비용 필드 제거
- `src\Tests\BalanceTool.cs:80-81` - `DryX`, `DryY` (탄약 바닥 유닛 수) - DELETE: 탄약 고갈 카운트 제거
- `src\Tests\BalanceTool.cs:94-105` - `FactionGame` 내 탄약/재보급/연료 통계 필드 전체 (`AmmoApi`, `AmmoZeroSamples`, `TripSamples`, `FuelEmptySamples` 등) - DELETE: 탄약/연료/보급 통계 필드 제거
- `src\Tests\BalanceTool.cs:124-128` - `SeriesColumns` 내 자원/공장/보급/연료/탄약 관련 열 - DELETE: 제거된 경제 및 보급 지표 컬럼 제거
- `src\Tests\BalanceTool.cs:131-139` - `PAmmo`, `PFuel`, `PTrip` 리플렉션 속성 - DELETE: 탄약/연료/보급 리플렉션 프로퍼티 제거
- `src\Tests\BalanceTool.cs:473` - `wp.AmmoCost * supW` - DELETE: 탄약 비용 계산 제거
- `src\Tests\BalanceTool.cs:477-482` - 탄창 초과 사격(`DryX`, `DryY`) 집계 - DELETE: 탄약 초과 집계 제거
- `src\Tests\BalanceTool.cs:553-565` - 사격 시 `ShotsAmmo`, `SuppliesShot` 보급품 소모 추정 - DELETE: 탄약 무기 사격/보급 소모 추정 제거
- `src\Tests\BalanceTool.cs:571` - `SampleAmmo` 호출 - DELETE: 탄약/재보급 표본 채취 호출 제거
- `src\Tests\BalanceTool.cs:602-628` - `SampleAmmo` 메서드 - DELETE: 탄약/연료/재보급 표본 수집 메서드 전체 제거
- `src\Tests\BalanceTool.cs:632-663` - `SampleSeries` 내 자원 스톡/보급품 순수입/공장/보급고/병영 카운트 - DELETE / MODIFY: 자원/건설 건물 수집 제거
- `src\Tests\BalanceTool.cs:924-956` - `SustainSections` (유닛별 사격 통계 · 유지비) - DELETE: 탄약 유지비 및 탄창 초과 보고서 섹션 제거
- `src\Tests\BalanceTool.cs:1000-1096` - `GameSections` 내 자원 0 비율/공장/보급품 최저/자원 시계열 보고서 - DELETE / MODIFY: 경제/자원/공장/보급 보고서 섹션 제거
- `src\Tests\BalanceTool.cs:1061-1077` - `B-탄약` 보고서 섹션 - DELETE: 탄약/재보급/연료 시나리오 평균 보고서 섹션 제거

