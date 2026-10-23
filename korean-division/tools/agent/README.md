# tools/agent
- heavy_gate.ps1 v2 install (activation): just merge this branch (tools/gate-v2) into main - the call path (`korean-division\tools\agent\heavy_gate.ps1`) is unchanged; the conductor merges when no agent is using it; test `powershell -File heavy_gate_test.ps1` (ALL PASS), check `heavy_gate.ps1 -DryRun`.
- dispatch.ps1 <id>[,<id>] [-DryRun|-Plan|-Selftest]: assemble the order prompt from card front-matter (spec docs/specs/DISPATCH_TEMPLATE_SPEC.md). roadmap_check.ps1 now ends with ROADMAP HITS|NONE.
