# Officer Schools · Enemy Faction Dispositions (2026-10-21)

> Long-term roadmap (officer dispositions). Schools = Korean Empire army officers only. Enemy officers = no school, **2~3 dispositions per faction** (Fengtian 4, bandits = share Fengtian's lowest disposition 1).
> AI tendency axes (common): command delay · maneuver preference (frontal ↔ flank/infiltration) · assault tendency · length of firepower preparation · retreat threshold (criterion = company casualty rate 30~40%) · how reserves are committed · behavior after reaching the objective (continue ↔ consolidate) · specialty.

## 1. Korean Empire army schools (user approved 2026-10-21)
| School | Officer disposition | Tactical disposition | AI tendency | Research link |
|---|---|---|---|---|
| German (독일) | Independent action · discretion (mission-type command), trusts NCOs | 1918 stormtrooper-style infiltration · weak-point breakthrough · flank bypass · elastic defense then immediate counterattack | short command delay · prefers flank/infiltration · uses assault infantry · medium retreat threshold · fast counterattack | infiltration tactics · assault troops |
| French (불란서) | Cautious · planned, higher-level control · timetable | "Firepower kills" — artillery preparation → phased advance to a fixed objective | long barrage preparation · advance after firepower · slow advance · few losses · consolidate at the objective | creeping barrage cooperation · artillery |
| Russian (노서아) | Stubborn · patient, accepts losses | Force concentration · frontal offensive · bayonet charge tradition · tenacious defense | prefers assaults · high retreat threshold · commits reserves all at once · weak reconnaissance/coordination | bayonet charge · defensive positions |
| Pyongyang (평양) (British style + Russian style) | Discipline · training, balanced | British-style rifle marksmanship · firepower and maneuver · consolidate after occupation + Russian-style tenacity | rifle fire efficiency · consolidate after occupation · medium-high retreat threshold · standard type (default for newly formed units) | marksmanship · formations |
| Beijing (북경) (Baoding Military Academy, Baoding school) | Pragmatic · political, preserves forces | Japanese-style offensive spirit (night raids · surprise attacks) + warlord-style pragmatism · encirclement/bypass · quick withdrawal when unfavorable | low retreat threshold · night/surprise · encirclement maneuver · short firepower preparation | night combat · cavalry/maneuver |
- People: 김 참장 · 김주현 · 남 참장 = German / imperial grandson 이태 = Pyongyang / imperial son 이석 · General 민 = Russian.

## 2. Enemy faction dispositions (user approved 2026-10-21 "go with the proposal")
### Japan (Kwantung Army) — 3
| Disposition | Character | AI tendency |
|---|---|---|
| Offensive faction (Kwantung Army elite) | Offensive spirit · night raids · bayonet charges, risk-taking — the lead in the "runaway" event | high assault tendency · night attacks · high retreat threshold · assault after machine gun cover |
| Railway guard | Holds the railway zone · stations, strongpoint defense | holds strongpoints · machine gun positions · armored trains · reinforcement along the railway · counterattacks are local |
| Staff type | Calculating · deceptive, regulates the level of local clashes | encirclement · bypass · firepower preparation · loss avoidance · negotiation/ceasefire when unfavorable (scripted) |

### Fengtian (봉천) — 4 (lower going down, #4 = shared with bandits)
| Disposition | Character | AI tendency |
|---|---|---|
| 1. Direct-line elite (장작림 guard · 강무당 graduates) | Relatively regular, loyal | uses artillery · tenacious defense · counterattack · medium retreat threshold |
| 2. 강무당 new-style faction (Japanese education influence) | Offensive, Japanese instructors' influence | bypass · surprise · night raids · good machine gun handling |
| 3. Old-style local army (순방대 · 녹림 origin officers) | Preserves forces, low morale | weak firepower · frontal standoff · low retreat threshold · commits reserves late |
| 4. Bandit type (lowest — **shared with bandits**) | Plunder · ambush, no discipline | ambush · surprise · large-blade charge · scatters and flees immediately when unfavorable · weak strongpoint defense |

### Russian White Army — 3
| Disposition | Character | AI tendency |
|---|---|---|
| Imperial officer corps (orthodox) | Honor · officers lead, morale-dependent | frontal assault · officers in front · weakens sharply if officers are lost |
| Ataman type (Semyonov-style Cossacks) | Cavalry raids · plunder · unstable | cavalry maneuver · raids · low retreat threshold · armored train |
| Railway defense type | Holds railways · cities, defeatist | strongpoint defense · armored train · quick withdrawal judgment |

### Red Army (including Far Eastern Republic) — 3
| Disposition | Character | AI tendency |
|---|---|---|
| Political commissar control type | No retreat · accepts losses | very high retreat threshold · mass offensive · repeated frontal attacks |
| Civil war maneuver faction (Red cavalry) | Cavalry mass breakthrough · tachanka | cavalry · tachanka maneuver · flank · pursuit |
| Partisan type (Far Eastern partisans) | Ambush · surprise · railway destruction | ambush · uses forest/mountains · hit and run · weak strongpoint defense |

## 3. How it is applied (approved)
- One disposition is assigned per mission · enemy unit (mission json). Even within the same faction each unit differs → a different opponent in each battlefield.
- The enemy commander's disposition is revealed in the intelligence (espionage) tab (accuracy by public sentiment · reconnaissance) → choose a counter strategy.
- Implementation = put a profile per disposition in the tactical AI profiles (`assets/data/ai_profiles.json`) and differentiate by the axis values above. Shares the same axes as the officer school · personality system (long-term).
