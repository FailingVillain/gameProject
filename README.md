gameProject = monorepo of `korean-division` (C# 1920 campaign RTS) + `game-tools` (art/audio pipelines that feed it).
Layout: `korean-division/`, `game-tools/` (tracked); `kd-develop/`, `gt-develop/` = untracked work folders for git worktrees.
Build: `cd korean-division && dotnet build -o bin/main`; release via `korean-division/publish.ps1`.
