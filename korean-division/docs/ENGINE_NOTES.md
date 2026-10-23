# Engine notes - index (엔진 노트 - 색인) (split 2026-10-23; full original = docs/legacy/ENGINE_NOTES_FULL_2026-10-23.md, do not read it whole)

> The body moved into per-topic files (content verbatim, zero lines lost). Read only the section you need: `Grep -n '^###\? ' docs/engine/<file>` and then Read just that range.
> Old heading -> new file full map: docs/engine/HEADING_MAP.md (includes old line numbers). Add new sections at the end of the matching topic file.

| File (docs/engine/) | KB | Contents (examples of old top-level sections) |
|---|---|---|
| foundations.md | 9 | original preamble · old TOC (line numbers invalid) · v0.3 notes · a3 notes |
| save-snapshot.md | 5 | save/load (snapshot), save compatibility (savecompat) |
| ammo-supply.md | 5 | given troops · ammo recovery · supply wagon (§0-1), ammo N76/N76b |
| combat-core.md | 21 | combat overhaul rounds 1-4, armor immunity, engagement range · cover, N35 officer avoidance, tweak1 |
| suppression-surrender.md | 14 | suppression redesign N25 / 2, N79b surrender |
| artillery.md | 23 | shell flight · emplacement arc · N11/N21/N22 artillery · ground fire · arty3 · T10 FieldOps |
| ai-tactical.md | 26 | tactical AI 1 / 2 / 3A / 3B / 3B2, river crossing · counterattack, T24 amendment, T32b smoke AI |
| terrain-environment.md | 16 | terrain · height · sight, weather, range x1.5, forest combat, cover props · density |
| trenches-breach.md | 10 | trench auto-occupation · shell craters, trench assault N32, trench breach bundle |
| org-formation-movement.md | 47 | unit organization, collision · formation, combat formation frame, N12, gapfill, pursuit · retreat, F1/F2 contact, formation relaxation · choke |
| command-ranks.md | 29 | squad · NCO · rank, command rank N3, tactical frame stage 2, ai412, cavcmd, per-faction ranks, N74 order queue, OfficerKey |
| units-factions.md | 18 | scouts, LMG, assault infantry carbine, bandits (마적) · common units, new units / branches, combat engineers |
| trains.md | 4 | railway tracks, train movement · weapons · cargo objectives, armored train demo mission |
| campaign-mission.md | 12 | mission frame, static teams, campaign minimum version / 1-2 river crossing, battle autosave · result report |
| ui-render-perf.md | 14 | unit sheet LOD, LOD0 audit, UI icons, branch markers, spatial hash, civilian house styles |

## Old top-level sections -> new file (옛 최상위 절 -> 새 파일) (summary; full ~95 rows in HEADING_MAP.md)
| Old section (line) | New file |
|---|---|
| `##` top-level sections (L496 N35 · L500 per-faction ranks · L508 bandits · L545 N23 · L550 N64 · L563 F1 · L568 F1 2nd · L600 F6 · L617 N79b · L640 N32 · L657 save compat · L665 arty3 · L675 formation relaxation · L681 breach · L689 N74 · L697/L703 T24 · L707 T10 · L716~L736 trains · L739 T32b · L745 command convenience · L749 OfficerKey · L754 T55s) | the same-topic file in the table above (see HEADING_MAP.md) |
| `###` sections (L69~L654, most) | the same-topic file in the table above |
| L1~L68 preamble · old TOC | foundations.md |
