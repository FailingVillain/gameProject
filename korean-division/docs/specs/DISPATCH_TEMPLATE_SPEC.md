# DISPATCH_TEMPLATE_SPEC - structured cards + scripted agent prompt assembly
Status: draft (design only, no code) | date: 2026-10-23 | grade D中 H1 | owner: spec -> conductor decides
Basis: user-approved token items B1 (bundle small cards), B2 (slim common preamble), B6 (guard refuses prompts without the preflight line), plus the user's wish to try a hard-coded dispatch scheme in the later pipeline redesign.
Limit numbers are NOT written here (ops/LIMITS.md is the only source). Observed mismatch to resolve separately: LIMITS.md says 9 agents / opus 3, user message says 12, `agentHub/guard_config.json` says `max_total 8`, `max_by_model sonnet 4`.

## 0. Problem (numbers to be re-measured, section 9)
- Fixed start-up cost per agent is 24-37k tokens (global CLAUDE.md + memory index + role card + AGENT_RULES + the agent's first orienting reads).
- Each launch prompt written by hand is 600-900 tokens (card summary, base, hotspots, common tail 1-d, report rule). Most of it is copy-paste of 1-d.
- The conductor forgets pieces (preflight line, base hash, report cap): guard logs show `no_report_cap` / `no_files` warnings.

## 1. Card front-matter (machine-readable header of tasks/Tnn-*.md)
YAML block at the very top of the existing card; the human body below stays as-is (cards are about 1.5KB, front-matter adds about 250 bytes). Parsed by a PowerShell 5.1 script without a YAML library: only flat `key: value` lines and `[a, b]` inline lists are allowed (no nesting).
```
---
id: T58a                      # from card_id.ps1 only
title: <one line, Korean name (ID) rule: PIPELINE 2 (g)>
role: feature                 # sim|feature|art|docs|reviewer|measure|spec|scout (role card name)
tier: 中                      # 上|中|下 (informational; model is derived)
model: sonnet                 # opus|sonnet|haiku; default = from tier (上 opus, 中 sonnet, 下 sonnet; haiku avoided)
grade: D中 H1                 # D and H required; R S W optional
branch: feat/xyz              # new branch name
base: merge/0.2-final         # base branch; commit hash is filled by the script (git rev-parse) at assembly time
blockedBy: [T57]              # card ids; script checks QUEUE/MERGE_QUEUE status through preflight
hotspots: [src/WorldCombat.cs, src/UI/Hud.cs]   # exact repo-relative files (used by the conflict matrix, section 4)
fv: none                      # none | claim  (claim -> agent runs fv_claim.ps1, temp = base+1)
read_first: [docs/engine/combat.md#heading, src/Foo.cs:Bar()]  # paths (+ #heading or :symbol); no whole big docs
forbidden: [src/Sim/**, docs/ROADMAP_LONG.md]    # extra to the common prohibitions
report_lines: 8               # omit = role card default; the script writes the number into the prompt
gates: [build, detcheck, suite:snapshot]         # verification steps the agent must run through heavy_gate
bundle: [T58b]                # optional: ids the author already judged bundle-able (section 4)
status: queued
---
```
Rules: missing required key (id, role, grade, branch, base, hotspots, read_first, gates) = the assembler refuses (exit 2, names the key). Unknown keys are kept and ignored (forward compatible). Confirmed user decisions stay verbatim in the card body (spec rule), never in front-matter.

## 2. Generated prompt layout
Assembler: `tools/agent/dispatch.ps1 <id>[,<id>...] [-DryRun]` (new, PowerShell 5.1). It prints the final prompt and the Agent tool parameters (`subagent_type`, `model`, `description`) as JSON; the conductor passes them on unchanged. The conductor's own text per launch is the call plus an optional free line, about 50 tokens.
Layout, fixed order, target <= 450 tokens for a single card:
1. `ROLE`: role card name and the line `Card file: <path>. Read the card first, then only read_first.` (the card body carries the details; the prompt does not copy it).
2. `ORDER`: id, title, branch, `base <branch> @ <hash>`, blockedBy status (from preflight), FV line, hotspots.
3. `READ FIRST`: the read_first list; `DO NOT TOUCH`: forbidden list.
4. `GATES`: the gates list, plus `heavy jobs via heavy_gate` pointer only.
5. `PREFLIGHT`: the exact result line printed by `preflight.ps1 <id>`, pasted by the script (it runs preflight itself, see section 5). If preflight exits 1 (BLOCK) the assembler stops and prints the reason; no prompt is produced.
6. `TAIL`: the common tail (section 3), as a pointer plus the 4 mandatory items, not the full text.
7. `REPORT`: `Final report <= <report_lines> lines (Korean). Last line "임의 결정:" ...`.
Not generated (stays human): card body content, acceptance criteria text, anything the card did not decide.

## 3. Where the common tail lives
- Single source: new file `ops/DISPATCH_TAIL.md` (about 1.2KB), derived from PIPELINE 1-d (common prohibitions, fragment-file rule, 임의 결정 rule, one-line-per-role list). PIPELINE 1-d becomes a pointer to it (edit only on user instruction, per PIPELINE 2).
- The prompt includes only a 6-line condensed form generated from `ops/DISPATCH_TAIL.md` section `## inline` (no push; no ops/ edits on work branches; fragments via docs/*.d; Gemini flash for logs >150 lines; WIP-commit at 200k; report cap). Everything else lives in the file the agent already reads through AGENT_RULES.md. Rationale: the role card + AGENT_RULES are loaded anyway, so the tail must not duplicate them (B2).
- Role one-liners (1-d last bullet) live in `ops/DISPATCH_TAIL.md` section `## role:<name>`; the script appends only the section matching `role`.
- Limit numbers are never copied into the tail or prompt (rule from 1-d).

## 4. Bundling rules (B1)
Goal: 3-4 small cards in one agent so the 24-37k start-up cost is paid once.
A set may be bundled only if ALL hold:
1. Same `role`, same `model`, grade H <= 1 and D not 上; at most 4 cards; each card S/small (body <= 1.5KB, no "L" marker).
2. Hotspots overlap: they share an area (same top-level folder or at least one shared file), so one orientation read serves all. Disjoint areas gain nothing: do not bundle.
3. No card blockedBy another unmerged card in the set except as an ordered chain (A before B on the same branch, see 5).
4. fv is `none` for all, or exactly one card claims (never two FV claimers in one bundle).
5. Estimated total fits context: sum of read_first sizes + 4 x expected diff <= the 200k stop rule; if unsure, 3 cards.
6. Not bundle-able at all: sim 上 cards, art render cards (heavy lane), reviewer vs. its own author, merger work.
Branch policy (open question Q1): default = one branch per bundle `bundle/<firstid>-<n>` with one commit per card, each card gets its own `wip-ready` line; so review and merge stay per-bundle.
Conflict matrix (computed by the script from `hotspots`, not by hand). For every pair of cards A,B in the whole wave:
| A vs B | same file | same folder, different file | disjoint |
|---|---|---|---|
| both H<=1 | serialize or bundle in one agent in order | parallel OK | parallel OK |
| any H>=2 | serialize, never parallel | warn, conductor decides | parallel OK |
| shared FV claim | serialize (late binding makes it safe to parallelize, so only warn) | - | - |
Output of `dispatch.ps1 -Plan <ids>`: groups (bundles), the serial order inside a bundle, and a list `parallel-safe sets`. Same file in two different agents at the same time = refuse unless `-Force`.

## 5. dispatch_guard additions (B6)
Existing: `C:/works/agentHub/dispatch_guard.py` (PreToolUse hook on Agent/Task, config `guard_config.json`, mode warn, `check()` returns `(rule_id, message)` pairs, `block_on` list). Project copy under `tools/agent/` does not exist; the agentHub script is the single guard, so changes go there.
New rule `no_preflight`:
- Trigger: `len(prompt) > short_prompt_chars` and `subagent_type` in the work roles (config `preflight_roles`: feature, sim, art, docs; not reviewer/measure/scout/merger/spec) and the prompt has no match for `PREFLIGHT (OK|BLOCK) \S+ \(\d+ warning` (the exact last line `preflight.ps1` prints, see preflight.ps1 lines 127-128) and no match for `roadmap_check` result marker `ROADMAP (HITS|NONE)` (marker to be added to roadmap_check.ps1, see section 8).
- Regex lives in config: `"preflight_regex": "PREFLIGHT OK T\\d+\\w* \\("`. A pasted `PREFLIGHT BLOCK` line is not accepted (a blocked card must not be ordered).
- Failure message (verbatim, one line): `dispatch_guard: no_preflight - prompt lacks "PREFLIGHT OK <card id> (n warning(s))". Run tools/agent/preflight.ps1 <card id> and paste its last line (or use tools/agent/dispatch.ps1).`
- Rollout: add `no_preflight` to the check but NOT to `block_on` for the first week (warn + log), then switch on after the false-positive count in `guard_log.jsonl` is 0 for the week. Scripted prompts (dispatch.ps1) always contain the line, so they never trip it.
- Companion warn rules (cheap, same file): `no_base` (no `base ... @ <7+ hex>` string), `no_card_path` (no `tasks/T..` path for work roles). Both warn only.
- Fail-open stays: any internal error = allow (existing behavior).

## 6. What stays manual
- Writing the card body and acceptance criteria (spec), confirmed user decisions, FV claim decisions, review/merge ordering, stall judgement, anything not expressible as front-matter (one-off scouts, Gemini research, merger prompts, supervisor prompts with their own 12-line rule).
- Choosing which cards to launch and bundling the final answer (script proposes, conductor confirms).
- Mid-task course corrections via SendMessage (not recycling; see PIPELINE "Agent recycling: not adopted").

## 7. Migration in 3 small steps
Step 1 (guard only, no new tooling): add `no_preflight`/`no_base`/`no_card_path` to `dispatch_guard.py` in warn mode; add `ops/DISPATCH_TAIL.md` and point PIPELINE 1-d to it. Success: warn counts logged; zero change to the conductor workflow.
Step 2 (front-matter + assembler dry-run): add front-matter to NEW cards only (spec role writes it; `tasks/_TEMPLATE.md` updated); `dispatch.ps1 -DryRun` prints the prompt but the conductor still launches by hand with it. Old cards unchanged. Success: 10 consecutive cards assembled with no manual edits to the generated text.
Step 3 (use for real): conductor launches by `dispatch.ps1` output; bundling plan (`-Plan`) used for small cards; guard `no_preflight` switched to block. Success criteria below. Roll back by simply writing prompts by hand again (nothing else depends on the script).

## 8. Risks
- Rigidity: odd tasks do not fit the fields. Mitigation: free line + `-Extra "<text>"`, manual path always allowed (guard only checks the preflight line).
- Stale templates: tail file or front-matter fields drift from PIPELINE/AGENT_RULES. Mitigation: tail text is generated from one file; `dispatch.ps1 -Selftest` greps the output for the 4 mandatory 1-d fields and fails if one is missing; PIPELINE version bump rule (section 7 of PIPELINE) applies to the tail file.
- Bundle failure modes: one card blocks the rest, context exhaustion in the middle, review of a mixed diff is harder. Mitigation: card order by size, WIP-commit per card, cap at 4, bundle review reads per-commit.
- Bundling raises hotspot-conflict blast radius and stale-QUEUE risk (T18). Mitigation: preflight runs per card in the set; any BLOCK splits the card out.
- Wrong model from tier: derived `model` field overridable; guard keeps `model_missing` check.
- roadmap_check marker (`ROADMAP HITS|NONE` last line) does not exist yet; adding it is a small edit to a script owned by tools (own card, out of this spec).
- Front-matter parse in PowerShell 5.1 (no YAML): keep the flat grammar, test with a unit script like `pipe_ids_test.ps1`.

## 9. Measurable success criteria
Measure with the session logs: `python ops/guard_patch/usage_audit.py <YYYY-MM-DD>` (sums usage by session file and model under `~/.claude/projects/C--Users-seung`); subagent transcripts are `.../subagents/agent-*.jsonl` with sibling `.meta.json` (agentType, model).
| Metric | Definition | Before (record 3 days before step 3) | Target |
|---|---|---|---|
| Conductor tokens per launch | output_tokens of the Agent tool_use `prompt` field, mean over launches (count chars/3.5 if tokens absent) | 600-900 (stated) | <= 100 |
| Agent start-up cost | input+cache_creation tokens of an agent's first assistant message | 24-37k | -15% (B2 slimming), no loss in report quality |
| Tokens per card | total agent tokens / cards completed, bundled vs. single | measure | bundled <= 70% of single for sets of 3-4 |
| Guard hygiene | `guard_log.jsonl` count of `no_preflight`, `no_report_cap`, `no_files` per 20 launches | measure | 0 / 0 / 0 after step 3 |
| Rework | cards reordered because of missing base/hotspot/preflight info | measure (LEAKS) | 0 per wave |
Record the before value for each metric in `watch/LEAKS.md` (rule: no numbers, no new rule; PIPELINE section 7). Declare the scheme a failure and revert if, after 2 waves, tokens per launch is not <= 50% of before, or bundled cards show more fix cycles per card than single ones.

## 10. Open questions (-> docs/DECISIONS_PENDING.md candidates, not decided here)
- Q1 bundle branch policy: one branch per bundle (proposed) or one branch per card inside one agent?
- Q2 `tier` 下 maps to sonnet (haiku avoided at 30k tokens/call) - confirm.
- Q3 does `dispatch.ps1` also run `card_id.ps1`/`roadmap_check.ps1`, or is the card header line `중복 점검:` enough?
- Q4 resolve limit mismatch (LIMITS 9 / user 12 / guard 8) before step 1; guard values should read from one source.
