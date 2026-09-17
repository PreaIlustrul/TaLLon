# TaLLon

**An on-demand window manager for Windows 11.**

Tap the *special key* (the Copilot key by default) and your desktop becomes TaLLon: an infinite
canvas with a top bar, where every open program is a window you can drag around, tile, pin or put
to sleep. Tap the key again and Windows is exactly as you left it.

> Status: **v0.2 pilot**. Tested on the author's laptop (Windows 11 Pro 26200, 3840×2400 at 200 %).
> See [Known limitations](#known-limitations--concerns).

## What it does

- **Dormant until tapped.** TaLLon sits in the tray. A low-level keyboard hook (on its own thread,
  re-armed after sleep) waits for the special key. Nothing else about Windows changes.
- **Copilot key done right.** The key really sends `LWin+LShift+F23`; TaLLon intercepts the whole
  chord so Windows never opens Copilot or the Start menu. Any other key can be the special key.
- **Tap = enter / exit. Hold + key = command.** No "leader" waiting mode.
- **Everything comes with you.** On entry every open window is cascaded in the middle of the
  canvas; the taskbar hides; the work area is adjusted so maximise fills the space under the bar.
- **Infinite mode** (default): windows live on an unbounded plane. Pan by pushing the mouse against
  a screen edge or two-finger scrolling over the canvas; the bar shows your coordinates; new windows
  spawn centred at 1/12 of the screen; right-click a title bar to **pin** (position remembered across
  sessions) or **hibernate** (process suspended until focused).
- **Tiling mode**: dwm/vxwm master-stack. `Special+arrows` move focus, `Special+Shift+arrows` swap.
- **Overview**: `Special+Tab`, `Alt+Tab` or `Win+Tab` lay every window out in a grid; click one to
  jump to it.
- **Top bar** (Omarchy-inspired): coordinates and mode on the left; menu, home, settings, overview,
  mode and the clock in the centre; Wi-Fi, Bluetooth, volume and battery on the right.
- **Focus ring** around the focused window; clicking the canvas focuses nothing.
- **Command menu** (`Special+Space`): pinned apps, commands, open windows and every installed
  program, with icons and search.
- **One app.** The TaLLon window has a *Launch TaLLon* button and all settings (special key, every
  binding, launchers with an app picker, pinned menu apps, appearance, infinite/tiling parameters,
  diagnostics with a raw key log). Closing it keeps TaLLon in the tray. Starts with Windows.
- **Crash-proof exit.** A watchdog process restores the taskbar, work area and every window
  placement if TaLLon is killed while active; the next start does the same from `session.json`.

Full key table: [docs/keybindings.md](docs/keybindings.md).

## Build & run

Requires the .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8 --source winget`).

```bash
dotnet build src/TaLLon.sln -c Release
powershell -ExecutionPolicy Bypass -File tools/install-shortcuts.ps1   # Desktop shortcut
```

- `src\TaLLon.App\bin\Release\net8.0-windows10.0.19041.0\TaLLon.exe` — the app (also registers
  itself in the Start menu and at login on first start).
- `TaLLon.exe --tray` starts hidden; `--send <chord>` drives a running instance; `--restore` puts
  the desktop back.

Config: `%APPDATA%\TaLLon\config.json` (hot-reloaded). Log: `%APPDATA%\TaLLon\TaLLon.log`.

## Testing

`tools/smoke-test.ps1` drives a running TaLLon end-to-end (enter, spawn, tiling, overview, panning,
menu, exit, then kills the process to prove the watchdog restores the desktop) and screenshots each
stage. `tools/ui-test.ps1` covers the main window.

## Architecture

```
src/TaLLon.Core            class library, no UI
  Native/Win32.cs             P/Invoke surface (hooks, windows, DWM, work area, SendInput, NtSuspendProcess)
  Input/HookThread.cs         WH_KEYBOARD_LL + WH_MOUSE_LL on a dedicated thread
  Input/SpecialKeyEngine.cs   Copilot chord, tap vs hold, chords, Alt/Win-Tab takeover, caption right-click
  Windows/                    window enumeration/filtering, SetWinEventHook watcher
  Layout/Layouts.cs           master-stack, overview grid, cascade, directional neighbour
  Session/Environment.cs      enter/exit, infinite viewport, tiling, overview, pin, hibernate
  Session/SessionState.cs     session.json, pinned windows, CrashRestore, Watchdog
  Status/SystemStatus.cs      battery, volume (Core Audio), Wi-Fi (netsh fallback)
  Apps/AppCatalog.cs          Start menu + shell:AppsFolder enumeration, launching
src/TaLLon.App             WPF: main window, tray, canvas, top bar, focus ring, menu, context menu,
                           app picker, icon cache (IShellItemImageFactory), WinRT radio/network status
memory/                    project memory (plan, findings, decisions, progress) — start at PROGRESS.md
```

## Known limitations / concerns

- **Physical Copilot key**: chord detection now waits for F23 without a timer, which fixed the
  first version's leak on this laptop. Use *Diagnostics → Show raw key events* to see what the
  key sends if anything is odd.
- **Touchpad gestures** (three-finger swipes) are handled inside Windows and do not reach TaLLon.
  Map them to a shortcut in *Settings → Bluetooth & devices → Touchpad → Advanced gestures* and
  bind that shortcut in TaLLon.
- **Window content does not scale.** A window at 1/12 of the screen shows 1/12 of its content; a
  scaled "always maximised" look would need thumbnail rendering (see memory/decisions.md D14).
- **Hibernate** suspends the whole process (like Process Lasso). Apps with live network
  connections may drop them. explorer.exe is never suspended.
- **Primary monitor only**; other monitors are left alone.
- Title-bar buttons stay the native three (custom two-button bars need custom frames per app).

## License

MIT — see [LICENSE](LICENSE).
