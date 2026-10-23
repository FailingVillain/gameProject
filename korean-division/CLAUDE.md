# Korean Division - charter (L0, <=2KB)
C# .NET 8 + Raylib-cs deterministic tactical campaign game. Repo `C:/gameProject/korean-division`.

## Absolute rules
1. **Determinism**: state changes only via commands (`WorldCommands.cs`). DetMath + hash RNG; no wall clock / render state / dictionary enumeration order. New state = checksum + `SnapshotSelfTest.Audit` + save + bump `Snapshot.FormatVersion`.
2. **Push only when the user commands it.** Local commits are free.
3. **Commit**: `git -c user.name=seung -c user.email=seungwoo3656@gmail.com commit`, add by file name (no `commit -a`), trailer `Co-Authored-By: Claude <actual model> <noreply@anthropic.com>`.
4. **No commands that trigger a permission prompt** (delete, kill by image name, write outside the repo, push). Do not work around it: append one line (= ops/DECISIONS candidate) to the end of `docs/DECISIONS_PENDING.md` and continue. Details `docs/procedures/permissions.md`.
5. **Report**: within the card's line count, conclusion + numbers + needed decisions only. Long logs via grep / Gemini flash.
6. Report in **Korean**. Do not build features outside the request; only propose. Past ~200k context, wrap up and stop.
7. **Launching the game window** (visual check, screenshot, uifuzz) via `tools/agent/run_game.ps1`: secondary monitor, else primary.

## Read next
- Role card `C:/Users/seung/.claude/agents/<role>.md` (sim feature art docs measure merger reviewer release scout spec) + task card.
- Common rules `tools/agent/AGENT_RULES.md` (<=3KB), procedures `docs/procedures/` (merge-procedure, gate, permissions, measurement).
- Build, flags, verification, naming rules, external tools, latest selftest: `docs/PROJECT_NOTES.md` (Grep). Limits: `ops/LIMITS.md`. Decisions: `ops/DECISIONS.md`. Workflow, doc ownership: `ops/PIPELINE.md`.
- Abbreviations (2 capital letters, e.g. WR, DC, ST): `ops/ABBREVIATIONS.md` (read once, ~1KB). New md files are written in English (user-edited ones stay Korean).
- Symbol locations: Grep `PROJECT_MAP.md`. Big docs (`docs/engine/*` = engine notes by topic, index in `docs/ENGINE_NOTES.md`; ROADMAP_LONG; legacy/): never read whole, Grep sections only (engine notes: Grep `docs/engine/`).
