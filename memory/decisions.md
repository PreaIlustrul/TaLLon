# Decisions (architecture) — each with the reason

D1 (2026-09-15) Own implementation in C# / .NET 8 / WPF rather than forking.
  Why: GlazeWM is Rust + GPL-3 (license contagion, no .NET tooling); komorebi is PolyForm/custom
  (not freely forkable); workspacer is MIT but unmaintained and "always-on" by design; FancyWM is
  C# but its architecture (always-on virtual-desktop panels) doesn't fit "dormant until summoned +
  canvas". WPF gives a quick path to the dark settings GUI and overlays. We borrow *techniques*:
  dwm/vxwm master-stack math, copilot-key-fix's 3-state chord interception (MIT), komorebi's
  DWM extended-frame offset correction.

D2 Two projects: TaLLon.Core (class lib, all Win32/logic, unit-testable) and TaLLon.App (WPF:
  tray daemon + canvas + menu + settings, `--settings` flag opens settings only).
  Why: one exe to install/run; settings edits reach the daemon through a config-file watcher.

D3 Special key handling = "hold" modifier with automatic "leader" fallback.
  Why: unknown whether the Copilot key auto-repeats / can be held on every laptop. If the key is
  tapped (down+up with no other key), TaLLon enters leader mode for N ms and the next key
  completes the chord. Both styles work with one config.

D4 Copilot key detection: 3-state deferral in the WH_KEYBOARD_LL hook (LWin down -> wait <=30ms
  for LShift -> F23). If F23 arrives, all three are swallowed and Special goes down; otherwise the
  buffered events are replayed with SendInput (tagged with our dwExtraInfo so we skip them).
  Why: the OS must never see LWin/LShift of the chord, or Start menu/Copilot fire.

D5 "Replace the desktop" = minimize (SW_SHOWMINNOACTIVE) every visible top-level window we did
  not create, hide the taskbar, show a full-screen no-activate canvas window; on exit restore with
  SW_SHOWNOACTIVATE + re-show taskbar. Not using Windows virtual desktops.
  Why: virtual desktop create/switch APIs are undocumented COM interfaces that break between
  builds (this machine is 10.0.26200). Minimize/restore is boring but robust.

D6 Tiling = dwm/vxwm master-stack on the primary monitor; Special+T toggles tiled <-> "restore
  previous rects" (every managed window remembers its pre-tile rect).
  Why: user asked for vxwm behaviour plus an explicit tile/restore toggle.

D7 Config is JSON at %APPDATA%\TaLLon\config.json, human-editable; settings app is a thin editor.
  Why: pilot speed; power users can edit by hand; easy to back up.

D8 DPI: PerMonitorV2 manifest; all window geometry done in physical pixels via Win32, not WPF DIPs.
  Why: SetWindowPos wants physical pixels; mixing units is the #1 tiling bug on laptops with scaling.

D9 Default bindings (all "Special+..."): W toggle TaLLon, Space menu, T tile/restore, Enter terminal,
  Q close window, J/K focus next/prev, H/L master ratio, M monocle, F float focused, comma settings.
  Why: vxwm muscle memory where it doesn't collide with Windows habits; everything is remappable.

D10 `--send` injects UNTAGGED key events (dwExtraInfo = 0) so the daemon's hook treats them as
  real input; TaLLon's own replays/ALT trick are TAGGED (0x7A11) and skipped by the hook.
  Why: one exe doubles as a scripting client and a test harness without a second code path.

D11 Settings app is the same exe with `--settings` (separate process, no hook, no mutex).
  Why: the daemon keeps running; the settings process writes config.json and the daemon's
  FileSystemWatcher applies it live. If the daemon is not running, settings still work.
