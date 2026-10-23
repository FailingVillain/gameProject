# ABBREVIATIONS (27 entries; approved by user 2026-10-23)
Rules: exactly 2 capital letters, no prefix. Only multi-token terms with >=50 uses (counted over ops/docs/agents/memory). Defined here only; never invent others in files. In chat with the user, spell out on first use.
Not abbreviated on purpose (1-token words or rare): golden, branch, agent, merge, company, compatgate, queue_guard, kd_guard, sleep mode, render pool.

| Abbr | Meaning | Uses |
|---|---|---|
| ST | selftest | 527 |
| WR | wip-ready (card state) | 380 |
| FV | FormatVersion (Snapshot/Replay) | 338 |
| DC | detcheck (+ golden hash pair) | 224 |
| UD | user decision | 218 |
| SN | snaptest | 156 |
| ML | merge line (merge/0.2-final) | 137 |
| WT | worktree | 128 |
| AF | allowedFAIL | 102 |
| UA | user approval | 88 |
| NC | needs confirmation | 115 |
| IP | in progress | 121 |
| AD | arbitrary (agent-made) decision, logged in AUTO_DEFAULTS_LOG | 68 |
| CD | conductor (main session) | 327 |
| HJ | heavy job (build/selftest/render) | ~100 |
| MQ | MERGE_QUEUE / merge round | 54 |
| SC | savecompat | 73 |
| UF | --uifuzz | 70 |
| DF | default off (switch) | 70 |
| RO | read-only | 60 |
| BN | battalion | 329 |
| SZ | suppression | 273 |
| KT | KD_SELFTEST_ONLY (env var for one selftest group) | 94 |
| AB | ai3b2 (AI stack / branch family) | 80 |
| BJ | balance.json | 65 |
| IA | git merge-base --is-ancestor (merge-state check) | 58 |
| SP | spec/cards-c1e1 | 74 |

Notes: ST/BN/RO can look like ordinary words or units; if a sentence is ambiguous, spell it out. Card ids (T61d, G2, NEW-4, F1...) already act as abbreviations and stay.
Hash rule (approved 2026-10-23): quote commit hashes and golden/detcheck hash values with the first 8 characters in reports and docs (full value only when a tool needs an exact match).
