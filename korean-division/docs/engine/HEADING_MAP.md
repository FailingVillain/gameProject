# ENGINE_NOTES old heading -> new file map (ENGINE_NOTES 옛 제목 -> 새 파일 대응표) (split 2026-10-23, old line numbers = docs/legacy/ENGINE_NOTES_FULL_2026-10-23.md)

The "old heading" column is kept in the original Korean (it is the search key against the legacy file). The "new file § new heading" column gives the file and the English heading (short title) used there.

| Old line | Old heading | New file § new heading |
|---|---|---|
| L69 | ### v0.3 notes | engine/foundations.md § v0.3 notes |
| L77 | ### a3 notes (교전 테스트 · 밸런스 도구 · AI 자원 · 세력 언어/보이스) | engine/foundations.md § a3 notes: combat test, balance tool, AI resources, faction language/voice |
| L84 | ### 저장 / 불러오기 (스냅샷) | engine/save-snapshot.md § Save / load (snapshot) |
| L93 | ### 방향 변경 §0-1: 주어진 병력 · 탄약 회복 · 보급 마차 · 엄폐 (2026-10-19, `src/Core/Supply.cs` · `SupplyUnit.cs`, 테스트 `Tests/Su.. | engine/ammo-supply.md § Direction change §0-1: given forces, ammo regeneration, supply wagon, cover |
| L103 | ### 교전 개편 1차 (2026-10-18, ROADMAP §2-5) | engine/combat-core.md § Combat overhaul stage 1 |
| L111 | ### 교전 개편 2차 (2026-10-18, ROADMAP §2-4b~g · §2-6) | engine/combat-core.md § Combat overhaul stage 2 |
| L129 | ### 장갑 면역 · 신규 유닛 (2026-10-19, ROADMAP §2-4f) | engine/combat-core.md § Armor immunity and new units |
| L133 | ### 교전 4차 (2026-10-19, `src/Core/Combat4.cs`, 테스트 `Tests/Combat4SelfTest.cs`) | engine/combat-core.md § Combat overhaul stage 4 |
| L138 | ### 부대 편제 · 깃발 대형 (2026-10-18, ROADMAP §2-4b 부대 편제 2단계, `src/Core/Organisation.cs` · `OrgFormation.cs`, 테스트 `T.. | engine/org-formation-movement.md § Unit organization and flag formation |
| L145 | ### 유닛 시트 LOD (2026-10-19, `src/Render/LodSprite.cs`, 테마 `assets/themes/kd1920`) | engine/ui-render-perf.md § Unit sheet LOD |
| L164 | ### 정적 팀 (2026-10-19, 외교 시스템 대체, `src/Core/WorldTeams.cs`, 테스트 `Tests/TeamSelfTest.cs`) | engine/campaign-mission.md § Static teams |
| L170 | ### 지형 · 높이 · 지형 시야 (2026-10-19, ROADMAP ③ '지형 · 높이', `src/Core/GameMapTerrain.cs` · `FogVision.cs` · `FogOfWa.. | engine/terrain-environment.md § Terrain · height · terrain vision |
| L186 | ### 분대 · 부사관 · 계급 · 빈자리 점검 · 지휘 계통 지연 (2026-10-19, ROADMAP §2-7 "④ 구현", `src/Core/Squads.cs` · `CommandDelay.c.. | engine/command-ranks.md § Squads · NCOs · ranks · vacancy check · chain-of-command delay |
| L195 | ### 날씨 · 시간대 (2026-10-19, ROADMAP '날씨 · 시간', `src/Core/Weather.cs` · `Render/WeatherFx.cs`, 테스트 `Tests/Weather.. | engine/terrain-environment.md § Weather · time of day |
| L201 | ### 참호 자동 점유 · 포탄 구덩이 (2026-10-19, `src/Core/Trenches.cs` · `Render/MapRenderer.Edits.cs`, 테스트 `Tests/TrenchSe.. | engine/trenches-breach.md § Trench auto-occupation and shell craters |
| L212 | ### 미션 틀 (2026-10-19, `src/Core/Mission/` · `src/AI/MissionAi.cs` · `src/GameMission.cs` · `src/UI/MissionMenu.. | engine/campaign-mission.md § Mission frame |
| L227 | ### 전술 AI 1단계 (2026-10-19, ROADMAP ⑤, `src/AI/Tactical/` — `TacticalAi.cs` · `TacticalState.cs` · `AiProfile.c.. | engine/ai-tactical.md § Tactical AI stage 1 |
| L243 | ### 전술 AI 2단계 (2026-10-18, ROADMAP ⑤, `src/AI/Tactical/` — `TacticalAi.Strategy.cs` · `TacticalAi.Regiment.cs`.. | engine/ai-tactical.md § Tactical AI stage 2 |
| L250 | ### 전술 AI 3단계 A — 기동 계획 · 미션 목표 · 철수 (2026-10-19, `src/AI/Tactical/TacticalAi.Manoeuvre.cs`, 테스트 `Tests/S3aSel.. | engine/ai-tactical.md § Tactical AI stage 3A - maneuver plan · mission objective · withdrawal |
| L259 | ### 포탄 비행 시간 (2026-10-19, `src/Core/ShellFlight.cs`, 테스트 `Tests/ShellFlightSelfTest.cs` = `KD_SELFTEST_ONLY=sh.. | engine/artillery.md § Shell flight time |
| L266 | ### 방열 사계 (2026-10-19, `src/Core/Combat2.cs` '방열 사계', 테스트 `ShellFlightSelfTest.TraverseArc`) | engine/artillery.md § Emplacement field of fire |
| L274 | ### N11 포병 사거리 · 거리별 퍼짐 · 지도 크기 5단계 (2026-10-19) | engine/artillery.md § N11 Artillery range · spread by distance · 5 map size tiers |
| L280 | ### N21 포병 AI (2026-10-19, 브랜치 `artyai`, `src/AI/Tactical/TacticalAi.Artillery.cs`, 테스트 `KD_SELFTEST_ONLY=arty.. | engine/artillery.md § N21 Artillery AI |
| L291 | ### N22 포병 기여 · 보병포 · 정찰병 (2026-10-19, 브랜치 `arty2`, 테스트 `KD_SELFTEST_ONLY=arty2`, HANDOFF N29) | engine/artillery.md § N22 Artillery contribution · infantry gun · scouts |
| L299 | ### 사거리 · 시야 · 지도 ×1.5 (2026-10-19, ROADMAP §2-8) | engine/terrain-environment.md § Range · vision · map ×1.5 |
| L307 | ### 숲 전투 (2026-10-19, 브랜치 `forest`, 테스트 `KD_SELFTEST_ONLY=forest`) | engine/terrain-environment.md § Forest combat |
| L311 | ### 정찰병 (2026-10-19, ROADMAP '정찰병 유닛', `src/Core/Scouts.cs`, 테스트 `Tests/ScoutSelfTest.cs` = `KD_SELFTEST_ONLY=.. | engine/units-factions.md § Scouts |
| L324 | ### 충돌 · 대형 재설계 · 중대 행진 사격 (2026-10-18, 브랜치 `collision`, 분석 `docs/legacy/COLLISION_ANALYSIS.md`, 측정 `--collide.. | engine/org-formation-movement.md § Collision · formation redesign · company march fire |
| L336 | ### 묶음 3 (2026-10-19): 돌격 사격 토글 · 사병 진급 · 비전투 어택땅 · 잔재 정리 | engine/org-formation-movement.md § Bundle 3: charge-fire toggle · private promotion · non-combat attack-ground · cleanup of leftovers |
| L341 | ### 플레이어 지점 포격 (2026-10-19, `src/Core/GroundFire.cs`, 테스트 `Tests/GroundFireSelfTest.cs` = `KD_SELFTEST_ONLY=gr.. | engine/artillery.md § Player ground fire |
| L348 | ### 전투 대형 · 역할군 · 중대 전술 틀 1단계 (2026-10-19, 브랜치 `formations`, `src/Core/CombatFormations.cs`, 자료 `assets/data/f.. | engine/org-formation-movement.md § Combat formations · role groups · company tactical frame stage 1 |
| L360 | ### 경기관총 사격 방향 · 스프라이트 정합 (N2 / HANDOFF 4o, 2026-10-19, 테스트 `Tests/LmgAimSelfTest.cs` = `KD_SELFTEST_ONLY=lmga.. | engine/units-factions.md § LMG fire direction · sprite alignment |
| L366 | ### 지휘 계급 · 임시 진급 · 승계 · 장교 경험치 (N3, 2026-10-19, `src/Core/CommandRanks.cs`, 테스트 `Tests/CommandSelfTest.cs` = .. | engine/command-ranks.md § Command ranks · temporary promotion · succession · officer experience |
| L384 | ### 전술 틀 2단계 — 역할 행동 · 지휘관 전환 · 중화기 보폭 (N4, 2026-10-19, `src/Core/TacticDrill.cs` · `HeavyPacing.cs`, 자료 `tact.. | engine/command-ranks.md § Tactical frame stage 2 - role behavior · commander switching · heavy-weapon pacing |
| L398 | ### 공간 해시 · 지형 파생 캐시 (N16, 2026-10-19, `World.RebuildHash` · `GameMapTerrain.cs`, 테스트 `Tests/SpatialHashSelfTe.. | engine/ui-render-perf.md § Spatial hash · terrain-derived cache |
| L403 | ### N12 대형 강박 줄이기 · 장교 위치 · 정찰병 묶음 (2026-10-19 사용자 승인, 브랜치 `n12`, 테스트 `Tests/N12SelfTest.cs` = `KD_SELFTEST_ON.. | engine/org-formation-movement.md § N12 reducing formation obsession · officer position · scout bundle |
| L413 | ### gapfill 대열 정비 — 명령 · 장교 판단 · 병사 숙련 (2026-10-19 사용자 결정, 브랜치 `gapfill`, `src/Core/CloseRanks.cs`, 테스트 `Tests.. | engine/org-formation-movement.md § gapfill closing ranks - command · officer judgement · soldier skill |
| L431 | ### 최대 확대 LOD0 누락 감사 (N19, 2026-10-19, 테스트 `Tests/LodAuditSelfTest.cs` = `KD_SELFTEST_ONLY=lodaudit`) | engine/ui-render-perf.md § Max-zoom LOD0 omission audit |
| L436 | ### ai412 — 돌격 허가 (결정 4) · 대대 역할 (결정 12) · 부사관 기본 편성 (2026-10-19 사용자 승인, 브랜치 `ai412`, 테스트 `Tests/Ai412SelfTest.. | engine/command-ranks.md § ai412 - assault clearance (decision 4) · battalion roles (decision 12) · default NCO organization |
| L442 | ### 중대 후퇴 임계 30~40% (2026-10-19, 브랜치 `retreat`, 데이터만) | engine/org-formation-movement.md § Company retreat threshold 30~40% |
| L445 | ### 추격 · 후퇴 속보 · 기병 측면 (2026-10-19 사용자 결정, 브랜치 `pursuit`, `src/Core/Pursuit.cs`, 테스트 `Tests/PursuitSelfTest.cs.. | engine/org-formation-movement.md § Pursuit · retreat quick step · cavalry flank |
| L453 | ### cavcmd — 기병 장교 · 기병 부사관 대리 (2026-10-19 사용자 결정, 브랜치 `cavcmd`, 테스트 `Tests/CavCmdSelfTest.cs` = `KD_SELFTEST_.. | engine/command-ranks.md § cavcmd - cavalry officers · cavalry NCO deputies |
| L460 | ### 후퇴 사격 · 후퇴 뒤 재정비 · 포병만 아군 오사 · 기병 조금 엄격 · 미션 배치 (2026-10-19 사용자 결정, 브랜치 `pursuit2`, 테스트 `Tests/Pursuit2Sel.. | engine/org-formation-movement.md § Retreat fire · regroup after retreat · artillery-only friendly fire · cavalry slightly stricter · mission placement |
| L468 | ### UI 묶음: 수류탄 X · 아군 오사 계수 · 분대 상태 아이콘 (2026-10-19, 브랜치 `uiicons`) | engine/ui-render-perf.md § UI bundle: grenade X · friendly-fire counter · squad status icons |
| L475 | ### N21 돌격보병 = 카빈 + 수류탄 2 · 한국군 전용 (2026-10-19 사용자 결정, 브랜치 `carbine`) | engine/units-factions.md § N21 Assault infantry = carbine + 2 grenades · Korean army only |
| L482 | ### 병종 이미지 마커 (2026-10-19 사용자 결정, 브랜치 `rankui`, 표시 전용 — 시뮬 · 저장 · 체크섬 무관) | engine/ui-render-perf.md § Branch image markers |
| L484 | ### 교전 거리 · 엄폐 (engage, 2026-10-19, `WorldCombat.cs` ReactRange · Retaliate, `src/Core/CoverSeek.cs`, AI `Tact.. | engine/combat-core.md § Engagement distance · cover |
| L496 | ## N35 장교 교전 회피 · 정찰병 자위 사격 (2026-10-19, `src/Core/OfficerConduct.cs`) | engine/combat-core.md § N35 officer engagement avoidance · scout self-defense fire |
| L500 | ### 세력별 계급 표 (ranks 브랜치, 2026-10-19, 원문 `docs/RANKS_BY_FACTION.txt`, `src/Core/Ranks.cs`, `assets/data/ranks.j.. | engine/command-ranks.md § Per-faction rank table |
| L508 | ## 마적 · 공용 유닛 · 소총 변형 (N26 · N53 · N54, 2026-10-19, 브랜치 `factions`) | engine/units-factions.md § Bandits · common units · rifle variants |
| L516 | ### tweak1 — N44 전투 조정 묶음 (2026-10-19 사용자 결정, 브랜치 `tweak1`, 테스트 `Tests/Tweak1SelfTest.cs` = `KD_SELFTEST_ONLY=.. | engine/combat-core.md § tweak1 - N44 combat tweak bundle |
| L522 | ### 제압 재설계 N25 (2026-10-19 사용자 결정, 브랜치 `supp`, `src/Core/Suppression.cs`, 테스트 `Tests/SuppressionSelfTest.cs` =.. | engine/suppression-surrender.md § Suppression redesign N25 |
| L532 | ### 도하 · 병목 돌파 · 역습 교리 (crossing, 2026-10-19 사용자 결정, `src/AI/Tactical/TacticalAi.Crossing.cs` · `TacticalAi.Co.. | engine/ai-tactical.md § River crossing · bottleneck breakthrough · counterattack doctrine |
| L545 | ## 돌격보병 소탕 (N23) · 장교 경험 성향 (N55) — 2026-10-19 | engine/command-ranks.md § Assault infantry sweep (N23) · officer experience style (N55) |
| L550 | ## 경기관총 중대 편입 · 하향 (N64, 2026-10-20, 브랜치 `lmg`) | engine/units-factions.md § LMG joins the company · nerf |
| L554 | ### 탄약 (N76, 브랜치 ammo, 2026-10-20) | engine/ammo-supply.md § Ammo |
| L559 | ### 탄약 A안 (N76b, 브랜치 ammo2, 2026-10-20) | engine/ammo-supply.md § Ammo plan A |
| L563 | ## F1 접적 · 구역 사격 위치 (1차, 2026-10-20, 브랜치 `f1`, `src/Core/Contact.cs`) | engine/org-formation-movement.md § F1 contact · zone firing position |
| L568 | ## F1 2차 · 회전 · 선까지 전진 · 표시 (2026-10-20, 브랜치 `f2`, `src/Core/ContactF2.cs`) | engine/org-formation-movement.md § F1 stage 2 - rotation · advance to the line · display |
| L576 | ### 캠페인 최소판 (2026-10-20, vs1) | engine/campaign-mission.md § Campaign minimum version |
| L581 | ### 제압 재설계 2 (supp, 2026-10-20, `src/Core/Suppression.cs`, 테스트 `KD_SELFTEST_ONLY=supp`, **FormatVersion 41**) | engine/suppression-surrender.md § Suppression redesign 2 |
| L600 | ## F6 엄폐물 소품 층 (2026-10-20, props) | engine/terrain-environment.md § F6 cover prop layer |
| L608 | ### 캠페인 1-2 두만강 도하 · 병력 이월 (2026-10-20, c102) | engine/campaign-mission.md § Campaign 1-2 Tumen River crossing · force carry-over |
| L613 | ### 전술 AI 3단계 B — 방어 · 예비 · 역습 (2026-10-21, 브랜치 `ai3b`, `src/AI/Tactical/TacticalAi.Reserve3B.cs`, 테스트 `KD_SEL.. | engine/ai-tactical.md § Tactical AI stage 3B - defense · reserve · counterattack |
| L617 | ## N79b 항복 (2026-10-20, 브랜치 `surr`, `src/Core/Surrender.cs`) | engine/suppression-surrender.md § N79b Surrender |
| L625 | ### 새 유닛 (units, 2026-10-20, N63 · N68 · N72 · N78, Snapshot 44, 테스트 `KD_SELFTEST_ONLY=units`, 관전 미션 `units_sh.. | engine/units-factions.md § New units |
| L632 | ### 엄폐물 밀도 상향 (2026-10-21, propdens) | engine/terrain-environment.md § Cover prop density increase |
| L640 | ## 참호 돌격 (N32, 2026-10-20, 브랜치 `n32`, `src/Core/TrenchAssault.cs` · `src/AI/Tactical/TacticalAi.TrenchAssault... | engine/trenches-breach.md § Trench assault |
| L654 | ### 새 병종 4종 (newunits, 2026-10-22, N68 · N71, 테스트 `KD_SELFTEST_ONLY=units`) | engine/units-factions.md § 4 new branches |
| L657 | ## 저장 호환 (2026-10-22, 브랜치 `savecompat`) | engine/save-snapshot.md § Save compatibility |
| L665 | ## arty3 포병 · 중화기 묶음 (2026-10-22, 브랜치 `arty3`, `src/Core/Arty3.cs`, FormatVersion 51) | engine/artillery.md § arty3 artillery · heavy weapons bundle |
| L675 | ## 대형 완화 · choke (2026-10-22, 브랜치 `formation`, `src/Core/FormRelax.cs` · `src/Core/Choke.cs`, FormatVersion 51.. | engine/org-formation-movement.md § Formation relaxation · choke |
| L681 | ## 참호 돌파 묶음 (N59 연막 · 철조망 절단, N60 경박격포, N65 연막 수류탄 — 2026-10-22, `src/Core/Breach.cs`) | engine/trenches-breach.md § Trench breach bundle |
| L689 | ## N74 명령 대기열 · N75 사선 막힘 사수 표시 (2026-10-22) | engine/command-ranks.md § N74 Order queue · N75 blocked-line gunner display |
| L693 | ### 전술 AI 3B2 — 대체 중대 · bn_campaign 측정 (2026-10-22, 브랜치 `ai3b2` (wip/orderq2 위 스택), FormatVersion 55) | engine/ai-tactical.md § Tactical AI 3B2 - replacement company · bn_campaign measurement |
| L697 | ## T24 D08 수정안 ①②③ (2026-10-22, wip kd/t24-s1s3, 값은 후보 — 사용자 승인 대기) | engine/ai-tactical.md § T24 D08 fixes ①②③ |
| L703 | ## T24 ② 도하 예외 (2026-10-22, kd/t24-crossing-fix) | engine/ai-tactical.md § T24 ② river crossing exception |
| L707 | ## T10 대포병 · 조명탄 · 행군 피로 (FieldOps.cs, 2026-10-22, 기본 끔) | engine/artillery.md § T10 Counter-battery · flares · march fatigue |
| L714 | ### 전투공병 infantry_pioneer (T32a, 2026-10-22, 브랜치 `kd/t32a-pioneer`, ai3b2 위 스택) | engine/units-factions.md § Combat engineer infantry_pioneer |
| L716 | ## 철도 선로 지형 (2026-10-22, 브랜치 `rail`, T07, FormatVersion 53) | engine/trains.md § Rail track terrain |
| L721 | ## 열차 이동 (2026-10-22, 브랜치 `t30a-train-move`, T30a, FormatVersion 58 · Replay 23) | engine/trains.md § Train movement |
| L726 | ## 열차 무기 · 객차 피해 (2026-10-22, 브랜치 `t30b-train-weapons`, T30b, FormatVersion 59) | engine/trains.md § Train weapons and car damage |
| L731 | ## 화물열차 목표 (2026-10-22, 브랜치 `t30c-freight-objective`, T30c, FormatVersion 59 그대로) | engine/trains.md § Freight train objective |
| L736 | ## 장갑열차 시연 미션 (2026-10-22, 브랜치 `t30e-demo-mission`, T30e, FormatVersion 59 그대로) | engine/trains.md § Armored train demo mission |
| L739 | ## T32b 전술 AI 연막 수류탄 (2026-10-22, `src/AI/Tactical/TacticalAi.Smoke.cs` · `TrenchAssault.cs` TaSmokeHook, 기본 꺼.. | engine/ai-tactical.md § T32b tactical AI smoke grenade |
| L745 | ## 지휘 편의 ①⑤ 전투 자동 저장 · 전과 보고 (2026-10-22, T17 = 구 `battlereport` 복원) | engine/campaign-mission.md § Command convenience ①⑤ battle autosave · result report |
| L749 | ## T48b1 장교 신원 키 (OfficerKey, 2026-10-22) | engine/command-ranks.md § T48b1 Officer identity key |
| L754 | ## T55s 민가 양식 선택 (렌더 전용) | engine/ui-render-perf.md § T55s House style selection |
