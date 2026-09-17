# Progress log (append-only; newest at bottom)

## 2026-09-15 — session 1
- Checked toolchain: no dotnet SDK, no Rust. Installed .NET 8 SDK with winget (first attempt
  failed with "multiple sources" -> rerun with `--source winget`). SDK 8.0.425 OK.
- Researched vxwm, Copilot key, Windows WMs; wrote findings.md and decisions.md.
- Created project skeleton + memory files, git init (branch main).
- Wrote TaLLon.Core (Win32, config, KeyChord, KeyboardHook, SpecialKeyEngine, WindowQuery,
  WinEventWatcher, Layouts, WindowManager, AppCatalog) and TaLLon.App (App, ActionRouter, TrayIcon,
  CanvasWindow, OsdWindow, MenuWindow, KeyChordBox, SettingsWindow, Dark.xaml theme).
- Build gotchas hit: UseWindowsForms + ImplicitUsings makes `Application`, `TextBox`, `Color`
  ambiguous -> `<Using Remove="System.Windows.Forms"/>` and `<Using Remove="System.Drawing"/>`.
  Git Bash converts `taskkill /IM` into a path -> use PowerShell Stop-Process. A running daemon
  locks bin/Release, stop it before building.
- Assets generated with tools/make-assets.ps1 (System.Drawing): default-bg.png 2560x1440, tallon.ico.
- Tests run (all passed): tools/smoke-test.ps1 (enter, two Notepads tiled pixel-exact
  (8,8)-(2107,2392) + (2115,8)-(3832,2392) on 3840x2400, Special+T restore, menu, Special+Q close,
  exit restores desktop + taskbar); tools/ui-test.ps1 (settings screenshot, leader mode enter/exit,
  unbound chord OSD). Screenshots need SetProcessDPIAware() or you capture one quadrant.
- UI fixes after screenshots: editable ComboBox had no PART_EditableTextBox in the custom
  template -> plain dropdown; ToggleButton checked-state trigger lost to local Background value
  -> moved defaults into a Style.
- Added `--send <chord> [--leader]` and `--settings --page <name>` CLI switches.
- Wrote README.md, docs/keybindings.md, LICENSE (MIT), .gitattributes.

## 2026-09-16 — session 2 (v0.2 rework from user feedback)
- Read the v0.1 log: physical Copilot key leaked Win+Shift (timer too short); Alt-Tab restored a
  minimised window that was then tiled full-screen over the canvas ("taken out of the environment").
- Rewrote Core: Win32 (DeferWindowPos, work area, mouse hook, NtSuspend/Resume, hit test),
  HookThread (dedicated thread), SpecialKeyEngine (tap/hold, no timer, system shortcut takeover,
  caption right-click), Environment (infinite + tiling + overview + pin + hibernate), SessionState
  (session.json, pinned, CrashRestore, Watchdog), Layouts (grid, cascade, neighbour), SystemStatus.
- Rewrote App: single-instance App with --watchdog/--send/--restore/--tray, MainWindow (Launch
  button + 7 pages), TrayIcon, IconCache (IShellItemImageFactory with alpha), StartupSetup (Run key,
  Start-menu shortcut), CanvasWindow (scroll pan, click-unfocus), TopBarWindow, FocusBorderWindow,
  FocusSinkWindow, MenuWindow (icons, pinned), WindowContextMenu, AppPickerDialog, radio/network
  status via WinRT (TFM net8.0-windows10.0.19041.0).
- Build gotchas: namespace TaLLon.Core.System shadows System → renamed Status; `global::` inside
  an interpolated string is parsed as a format specifier; App.Main property collides with the
  generated Main(); class Startup collides with Application.Startup event; Environment ambiguity.
- Tests: tools/smoke-test.ps1 (tap enter, cascade, spawn 1/12, tiling, overview, mode round-trip,
  edge pan, menu, close, tap exit, kill → watchdog restore) all pass; screenshots verified.
- Fixed after screenshots: canvas above windows (RaiseManagedAboveCanvas), bar icon glyphs, Wi-Fi.
- Docs updated: README, docs/keybindings.md; memory decisions D12–D18; renamed repo/folder to TaLLon.
- Repo renamed to github.com/PreaIlustrul/TaLLon and pushed. Local folder rename tallon -> TaLLon
  is BLOCKED while a Claude session has it as its working directory ("Device or resource busy");
  do it from the parent folder next session: `mv tallon TaLLon_x && mv TaLLon_x TaLLon`, then start
  TaLLon.exe once from the new path (it repairs the Run key, Start-menu and desktop shortcuts need
  tools/install-shortcuts.ps1 again).
- Running now: TaLLon.exe --tray from the tallon\ path; desktop shortcut TaLLon.lnk; Start-menu
  TaLLon.lnk; HKCU Run "TaLLon" -> exe --tray.
