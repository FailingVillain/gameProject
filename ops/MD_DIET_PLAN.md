# MD_DIET_PLAN (2026-10-23; approved by user; language rule: new md = English unless the user edits it)

## Data (tools: ops/guard_patch/md_read_freq.py, term_freq.py)
- 163 md files, 1.5 MB. Read-tool counts over 1315 session logs. Caveat: Grep/Bash reads are not counted, so "never read" = candidate, not proof.
- Most read: AGENT_RULES.md (194 reads incl. old path), agent-lessons/common.md (72), ops/DECISIONS.md (29), korean-division/CLAUDE.md (21+8), agent-lessons/art.md, agents/art.md, ROADMAP_LONG.md (35), ops/QUEUE.md (19), ops/STATE.md (17), conductor-resume memory (14), HANDOFF.md (20), LESSONS.md (14), PIPELINE.md (12), LIMITS.md (8).

## Step 1 - translate to English (token saving; Korean ~1.5-2x tokens)
Order = read frequency. Keep file paths, identifiers, commands, Korean game proper names (units, places, people) verbatim. Never change meaning of a rule.
- T1: korean-division/tools/agent/AGENT_RULES.md, ~/.claude/agent-lessons/*.md, ~/.claude/agents/*.md (role cards), CLAUDE.md files in the repo.
- T2: ops/PIPELINE.md, LIMITS.md, LESSONS.md, SAVE_COMPAT_POLICY.md, STATE.md, QUEUE.md, QUEUE_0.3.md, md_guide.md, docs/HANDOFF.md.
- T3 (later): MEMORY.md + memory/*.md, ROADMAP_*.md, ENGINE_NOTES (after split), CARD_*.
- Stay Korean (user edits them): ops/DECISIONS.md, USER_DECISIONS_QUEUE.md, WAKE_REPORT.md, DELETE_LIST.md, APPROVAL_NEEDED.md, DECISIONS_PENDING*.
- Global ~/.claude/CLAUDE.md: translate only with user OK (outside write scope -> approval prompt).

## Step 2 - archive instead of delete (move to <dir>/_archive/, keep git history; real deletions go to DELETE_LIST.md)
Dead-doc candidates (never Read in logs; verify with Grep before moving):
- ops: DECISIONS_ARCHIVE_2026-10-22 (40KB), STATE_ARCHIVE_2026-10-22, QUEUE_DONE (22KB), DECISION_SESSION (16KB), WORKTREE_CLEANUP + _2026-10-22, ROLE_CARD_PATCHES_2026-10-22, PIPELINE_AUDIT_2026-10-22, REPORT_2026-10-22, AUTONOMOUS_DECISIONS_2026-10-22, MERGE_PROGRESS_UI, LESSONS_ARCHIVE.
- docs: TACTICAL_AI_STAGE3_REVIEW_2026-10-19 (39KB), TACTICAL_AI_STAGE3_PLAN, ISSUES_2026-10-20, T34_3CD_SPEC, CASUALTY_FOLLOWUP_PROPOSAL, COMMS_ALPHA_PLAN (move when its feature ships), HOUSE_VARIETY_PLAN, RIVER_DESIGN.
- Card tables CARD_INDEX / CARD_NAMES / CARD_REGISTRY (65KB total, never Read): check overlap, keep one.
- Old duplicate tree under OneDrive coding/korean-division (pre-migration paths show up in logs): see post-migration deletion in DELETE_LIST.

## Step 3 - compress logs (keep last N lines hot, rest to _archive)
- ops/PIPELINE_LOG.md 120KB -> last ~150 lines. AUTO_DEFAULTS_LOG.md 63KB -> last ~200 lines (never routinely read, per memory). MERGE_PROGRESS.md 22KB -> last 60 lines.
- korean-division/docs/ENGINE_NOTES.md 251KB: split by topic into docs/engine/*.md plus a 40-line index (only the index is read; topics are grepped).
- DECISIONS.md 34KB: move entries older than the current milestone into DECISIONS_ARCHIVE (user-edited file: ask before moving).

## Step 4 - rules
- New md files: English. Every new file gets one line in ops/md_guide.md under its layer.
- Reports from agents: <=12 lines (already). Logs: append-only with a size cap; rotate above 40KB.

## Abbreviations (32): see ops/ABBREVIATIONS.md (proposal; becomes active after user approval)
