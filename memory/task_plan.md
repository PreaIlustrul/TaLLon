# Task plan (phases)

Legend: [ ] todo  [~] in progress  [x] done  [!] blocked/concern

## Phase 0 — Setup (2026-09-15)
- [x] Toolchain check (git, gh, node present; dotnet SDK missing -> installed via winget)
- [x] Project folder C:\Users\cock\projects_claude\tallon, memory/ files, git init
- [ ] Public GitHub repo PreaIlustrul/tallon, first push

## Phase 1 — Research
- [x] vxwm semantics (dwm fork: master/stack tiled, monocle, floating; new windows -> master)
- [x] Copilot key behaviour (LWin+LShift+F23 chord, needs 3-state hook interception)
- [x] Prior art survey: GlazeWM, komorebi, FancyWM, workspacer, Whim, bug.n, FancyZones
- [x] Decide fork vs own build -> own build in C#/.NET 8 WPF (see decisions.md D1)

## Phase 2 — Core library (TaLLon.Core)
- [ ] Win32 interop (hooks, windows, DWM, monitors, SendInput)
- [ ] Config model + JSON store (%APPDATA%\TaLLon\config.json) + live reload
- [ ] Low-level keyboard hook + Copilot chord detector + Special-key state (hold + leader)
- [ ] Key chord parsing ("Special+Shift+Enter")
- [ ] Window enumeration/filtering (manageable top-level windows)
- [ ] WinEvent watcher (new/destroyed/foreground windows)
- [ ] Master/stack + monocle layout with DWM frame offset correction
- [ ] Session state machine: Enter (snapshot, minimize others, hide taskbar, show canvas), Exit (restore)
- [ ] App catalog (Start Menu .lnk + AppsFolder)

## Phase 3 — App (TaLLon.App)
- [ ] Tray icon + single instance + config watcher
- [ ] Canvas window (background image, no-activate, sits under managed windows)
- [ ] Menu window (Special+Space): actions + apps + open windows, fuzzy search
- [ ] Bindings: toggle WM, menu, tile toggle, terminal, close, focus next/prev, master resize, monocle, launchers
- [ ] Settings window (dark OBS-style): general, keys, launchers, appearance
- [ ] Desktop shortcut for settings + daemon

## Phase 4 — Test & harden
- [ ] Build, run, manual test on this laptop (hook, chord, canvas, tiling, restore)
- [ ] Crash-safety: taskbar/windows restore on unhandled exception / process exit
- [ ] Write README + docs/keybindings.md

## Phase 5 — Later (from goals G8)
- [ ] Multi-monitor, workspaces/tags, infinite canvas, mouse drag, window rules, run-at-login UI
