# Task plan (phases)

Legend: [ ] todo  [~] in progress  [x] done  [!] blocked/concern

## Phase 0 — Setup (2026-09-15)
- [x] Toolchain check (git, gh, node present; dotnet SDK missing -> installed via winget)
- [x] Project folder C:\Users\cock\projects_claude\tallon, memory/ files, git init
- [x] Public GitHub repo PreaIlustrul/tallon, first push

## Phase 1 — Research
- [x] vxwm semantics (dwm fork: master/stack tiled, monocle, floating; new windows -> master)
- [x] Copilot key behaviour (LWin+LShift+F23 chord, needs 3-state hook interception)
- [x] Prior art survey: GlazeWM, komorebi, FancyWM, workspacer, Whim, bug.n, FancyZones
- [x] Decide fork vs own build -> own build in C#/.NET 8 WPF (see decisions.md D1)

## Phase 2 — Core library (TaLLon.Core)
- [x] Win32 interop (hooks, windows, DWM, monitors, SendInput)
- [x] Config model + JSON store (%APPDATA%\TaLLon\config.json) + live reload
- [x] Low-level keyboard hook + Copilot chord detector + Special-key state (hold + leader)
- [x] Key chord parsing ("Special+Shift+Enter")
- [x] Window enumeration/filtering (manageable top-level windows)
- [x] WinEvent watcher (new/destroyed/foreground windows)
- [x] Master/stack + monocle layout with DWM frame offset correction
- [x] Session state machine: Enter (snapshot, minimize others, hide taskbar, show canvas), Exit (restore)
- [x] App catalog (Start Menu .lnk + AppsFolder) — 93 entries on this laptop

## Phase 3 — App (TaLLon.App)
- [x] Tray icon + single instance + config watcher
- [x] Canvas window (background image, no-activate, sits under managed windows)
- [x] Menu window (Special+Space): actions + apps + open windows, search
- [x] Bindings: toggle WM, menu, tile toggle, terminal, close, focus next/prev, master resize, monocle, float, zoom, launchers
- [x] Settings window (dark OBS-style): general, keys, launchers, appearance, about
- [x] Desktop shortcuts for settings + daemon (tools/install-shortcuts.ps1)
- [x] `--send <chord> [--leader]` CLI for scripting/testing, `--settings [--page x]`

## Phase 4 — Test & harden
- [x] Smoke test on this laptop (tools/smoke-test.ps1): chord detection, enter, adopt+tile two
      Notepads pixel-exact at 3840x2400/200%, restore, menu, close, exit -> desktop restored
- [x] Leader (tap) mode + unbound chord handling (tools/ui-test.ps1)
- [x] Crash-safety: taskbar/windows restore on unhandled exception / process exit
- [x] README + docs/keybindings.md
- [!] Physical Copilot key NOT yet tested by a human (injected chords only) — user to confirm
- [ ] Test with "stubborn" apps (min-size windows, Electron custom frames, UWP Settings)
- [ ] Test on external monitor / docking

## Phase 5 — Next (pick with user)
- [ ] Multi-monitor (per-monitor areas, move window to monitor)
- [ ] Workspaces/tags (vxwm 1-9) and/or the infinite canvas
- [ ] Per-app rules (always float, ignore), FancyWM-style min-size aware layout
- [ ] Mouse: drag to swap tiles; resize master by dragging the gap
- [ ] Menu polish: icons, recent apps, calculator/URL row, fuzzy matching
- [ ] Settings polish: live preview of bindings, import/export, themes
- [ ] Installer (winget/MSIX) and signed release builds via GitHub Actions
