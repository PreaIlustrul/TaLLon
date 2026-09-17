# PROGRESS.md — resume-here file

If you are a new session picking this up, read in order:
1. `memory/task_plan.md`  — what's done / what's next (checkboxes, open questions)
2. `memory/decisions.md`  — why things are the way they are (D1–D18)
3. `memory/findings.md`   — research and gotchas you don't need to rediscover
4. `memory/progress.md`   — detailed log of every session (newest at bottom)

Build:      `dotnet build src/TaLLon.sln -c Release`
Run:        `src\TaLLon.App\bin\Release\net8.0-windows10.0.19041.0\TaLLon.exe` (`--tray` to start hidden)
Test:       `powershell -ExecutionPolicy Bypass -File tools/smoke-test.ps1` (needs TaLLon running)
Shortcut:   `powershell -ExecutionPolicy Bypass -File tools/install-shortcuts.ps1`
Stop first: a running TaLLon locks bin/ — `Stop-Process -Name TaLLon` before building.

## Current status
v0.2 (2026-09-16): tap-to-enter, infinite + tiling modes, overview, top bar, pin/hibernate,
crash-proof exit, single app. Open questions for the user are listed in `memory/task_plan.md`
Phase 5. Latest session log is at the bottom of `memory/progress.md`.
