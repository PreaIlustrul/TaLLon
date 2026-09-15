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
