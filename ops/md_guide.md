# md_guide - the conductor's md collection (by layer · shortcuts)
Updated: 2026-10-23 · This file is an **index**. The source of truth for content is each file. Write records split by the **layers** below (one layer per line).
Links are relative to this file (ops/). If one does not open, paste the path into Explorer.
Pipeline source of truth: [PIPELINE.md](PIPELINE.md) (card → implementation → review → measurement → merge).

## L0 Charter·rules (above all layers; no edits without user approval)
- Global instructions: [~/.claude/CLAUDE.md](file:///C:/Users/seung/.claude/CLAUDE.md) · project: [README](../README.md), [CLAUDE.md](../CLAUDE.md), [PROJECT_MAP.md](../PROJECT_MAP.md)
- Common agent rules: [AGENT_RULES.md](../korean-division/tools/agent/AGENT_RULES.md) · [tools/agent/README.md](../korean-division/tools/agent/README.md)
- Role cards: [~/.claude/agents/](file:///C:/Users/seung/.claude/agents) (feature, sim, spec, docs, reviewer, merger, measure, release, scout, art) · lessons: [agent-lessons/](file:///C:/Users/seung/.claude/agent-lessons)
- Limits·policy: [LIMITS.md](LIMITS.md) · [SAVE_COMPAT_POLICY.md](SAVE_COMPAT_POLICY.md) · [LESSONS.md](LESSONS.md) · [LESSONS_ARCHIVE.md](LESSONS_ARCHIVE.md)
- Safety hook (read-only reference): [kd_guard.ps1](file:///C:/Program%20Files/ClaudeCode/hooks/kd_guard.ps1) · [kd_scope.json](file:///C:/Program%20Files/ClaudeCode/hooks/kd_scope.json)

## L1 Input·decisions (user ↔ conductor)
- Awaiting user decision: [USER_DECISIONS_QUEUE.md](USER_DECISIONS_QUEUE.md) · decision ledger (source of truth): [DECISIONS.md](DECISIONS.md) · [DECISIONS_ARCHIVE_2026-10-22.md](DECISIONS_ARCHIVE_2026-10-22.md)
- Agent question fragments: [../korean-division/docs/DECISIONS_PENDING.md](../korean-division/docs/DECISIONS_PENDING.md) (+ each branch's docs/decisions_pending.d/)
- Awaiting approval: [APPROVAL_NEEDED.md](APPROVAL_NEEDED.md) · awaiting deletion: [DELETE_LIST.md](DELETE_LIST.md)
- Self-made decision log (record everything): [AUTO_DEFAULTS_LOG.md](AUTO_DEFAULTS_LOG.md) · [AUTONOMOUS_DECISIONS_2026-10-22.md](AUTONOMOUS_DECISIONS_2026-10-22.md)

## L2 Work definition·queue (cards)
- Active queue: [QUEUE.md](QUEUE.md) · 0.3 queue·sleep-mode rules: [QUEUE_0.3.md](QUEUE_0.3.md) · done: [QUEUE_DONE.md](QUEUE_DONE.md) · backlog: [BACKLOG_2026-10-22.md](BACKLOG_2026-10-22.md)
- Card index: [CARD_INDEX.md](CARD_INDEX.md) · [CARD_NAMES.md](CARD_NAMES.md) · [CARD_REGISTRY.md](CARD_REGISTRY.md) · card bodies: [korean-division/tasks/](../korean-division/tasks)
- FV number reservations: [FV_RESERVATIONS.md](FV_RESERVATIONS.md) · follow-up items: [FOLLOWUPS.md](FOLLOWUPS.md) · version milestones: [VERSIONS.md](VERSIONS.md)
- Roadmap: [ROADMAP_SHORT](../korean-division/docs/ROADMAP_SHORT.md) · [ROADMAP_LONG](../korean-division/docs/ROADMAP_LONG.md) · [PIPELINE_ROADMAP.md](PIPELINE_ROADMAP.md)

## L3 Execution state·handoff (what is running now)
- Current state: [STATE_LIVE.md](STATE_LIVE.md) (auto) · [STATE.md](STATE.md) · gate: [gate_status.json](gate_status.json) · [gate_wait.log](gate_wait.log)
- Handoff: [HANDOFF_0.3.md](HANDOFF_0.3.md) (auto measurement + 10 hand lines) · [../korean-division/docs/HANDOFF.md](../korean-division/docs/HANDOFF.md) (situation-save procedure)
- Sleep·wake report: [WAKE_REPORT.md](WAKE_REPORT.md)
- Art progress: [ART_STATE.md](ART_STATE.md) · worktree cleanup: [WORKTREE_CLEANUP.md](WORKTREE_CLEANUP.md)
- Merge line: [MERGE_PLAN.md](MERGE_PLAN.md) · [MERGE_QUEUE.md](MERGE_QUEUE.md) · [MERGE_PROGRESS.md](MERGE_PROGRESS.md)

## L4 Verification·review·measurement (output quality)
- Review: [REVIEW_2026-10-23.md](REVIEW_2026-10-23.md) (6 branches before merge) · pipeline audit: [PIPELINE_AUDIT_2026-10-22.md](PIPELINE_AUDIT_2026-10-22.md)
- Baseline·measurement: [BASELINE_CURRENT.md](../korean-division/docs/BASELINE_CURRENT.md) · [PERF_BASELINE_2026-10-22.md](PERF_BASELINE_2026-10-22.md) · [PERF_OPT_PLAN.md](PERF_OPT_PLAN.md) · (to be generated) SOAK_2026-10-23.md, PERF_MAPSIZE_2026-10-23.md
- Daily report: [REPORT_2026-10-22.md](REPORT_2026-10-22.md) · [PIPELINE_LOG.md](PIPELINE_LOG.md)
- Rotated old log entries (verbatim, read only on demand): [_archive/](_archive/) - e.g. PIPELINE_LOG_to_2026-10-23.md (older rows of PIPELINE_LOG.md)

## L5 Design·reference docs (game content; read by coding agents)
- Engine·project: [ENGINE_NOTES.md](../korean-division/docs/ENGINE_NOTES.md) · [PROJECT_NOTES.md](../korean-division/docs/PROJECT_NOTES.md) · [CONTROLS.md](../korean-division/docs/CONTROLS.md)
- Campaign: [CAMPAIGN_MVP_SPEC](../korean-division/docs/CAMPAIGN_MVP_SPEC.md) · [CAMPAIGN_HOME_DESIGN](../korean-division/docs/CAMPAIGN_HOME_DESIGN.md) · [C1E1_EPISODE_SCHEMA](../korean-division/docs/C1E1_EPISODE_SCHEMA.md) · [C1E1_MAPS](../korean-division/docs/C1E1_MAPS.md) · [SETTING_NOTES](../korean-division/docs/SETTING_NOTES.md) · [CHARACTERS](../korean-division/docs/CHARACTERS.md)
- Personnel·officers: [OFFICER_DOCTRINES](../korean-division/docs/OFFICER_DOCTRINES.md) · personnel-management UI design is PERSONNEL_UI.md on branch spec/personnel-ui
- Tactical AI: [TACTICAL_AI_DESIGN](../korean-division/docs/TACTICAL_AI_DESIGN.md) · [TACTICAL_AI_STAGE3_PLAN](../korean-division/docs/TACTICAL_AI_STAGE3_PLAN.md)
- Art: [ART_SPEC](../korean-division/docs/ART_SPEC.md) · [PRONE_SPRITE_REVIEW](../korean-division/docs/PRONE_SPRITE_REVIEW.md) · review images: [docs/art_review/](../korean-division/docs/art_review)
- Translation·distribution·sound: [I18N_STATUS](../korean-division/docs/I18N_STATUS.md) · [DISTRIBUTION](../korean-division/docs/DISTRIBUTION.md) · [SOUND_POLICY_2026-10-22](../korean-division/docs/SOUND_POLICY_2026-10-22.md) · UI: [UI_BACKLOG](../korean-division/docs/UI_BACKLOG.md)

## L6 Memory (what persists across sessions)
- [MEMORY.md index](file:///C:/Users/seung/.claude/projects/C--Users-seung/memory/MEMORY.md) · folder: [memory/](file:///C:/Users/seung/.claude/projects/C--Users-seung/memory)
- Gemini delegation: [C:/works/agentHub](file:///C:/works/agentHub) · policy [GEMINI_FIRST_POLICY_2026-10-23.md](file:///C:/works/agentHub/GEMINI_FIRST_POLICY_2026-10-23.md)

## Recording rules (per layer)
1. Write one item in the file of **one layer** only. A fact spanning layers goes in the higher layer's source of truth; lower layers only link.
2. Write ownership is [PIPELINE.md §2](PIPELINE.md). No ops/ edits on work branches, only fragment files (docs/decisions_pending.d, engine_notes.d).
3. Reports·logs go to L3 (WAKE_REPORT), decisions to L1; when you create a new file, add one line to this index.
