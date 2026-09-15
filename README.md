# TaLLon

**An on-demand window manager for Windows 11.**

TaLLon stays dormant in the tray until you press the *special key* (the Copilot key by
default). Then the desktop becomes a clean canvas with your wallpaper, every window you open is
tiled dwm/vxwm-style, and a centred command menu launches anything installed. Press the key
again and Windows is exactly as you left it.

> Status: **pilot / v0.1**. It works on the author's laptop (Windows 11 Pro 26200, 3840×2400
> at 200 %). Expect rough edges; see [Concerns](#known-limitations--concerns).

## What it does

- **Dormant by default.** A low-level keyboard hook waits for the special key; nothing else
  changes about Windows.
- **Special key = Copilot key.** The Copilot key really sends `LWin+LShift+F23`; TaLLon
  intercepts the whole chord so Windows never opens Copilot or the Start menu. Any other key
  (CapsLock, Right Alt, F13–F24, …) can be the special key instead.
- **Hold or tap.** Hold Special like a modifier, or tap it and press the next key (leader mode).
- **Canvas.** `Special+W` minimises everything, hides the taskbar and shows a full-screen canvas
  with a background image (default included, any image configurable).
- **vxwm-style tiling.** New windows become *master* on the left; others stack on the right.
  `Special+T` toggles between tiled and "back where they were". Monocle, floating, master
  ratio, zoom-to-master, focus next/prev are all there.
- **Command menu.** `Special+Space` opens a centred palette: exit TaLLon, tile/restore, open
  terminal, focus an open window, or launch any of the programs installed on the PC (Start
  menu + Store apps), with search.
- **Launchers.** Bind any key to any program, file or URL.
- **Settings app.** `TaLLon Settings` (desktop shortcut) — a dark, OBS-style editor for the
  special key, every binding, launchers, background, tiling parameters and run-at-login.

Full list: [docs/keybindings.md](docs/keybindings.md).

## Build & run

Requires the .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8 --source winget`).

```bash
dotnet build src/TaLLon.sln -c Release
powershell -ExecutionPolicy Bypass -File tools/install-shortcuts.ps1   # desktop shortcuts
```

- `src\TaLLon.App\bin\Release\net8.0-windows\TaLLon.exe` — the listener (tray icon).
- `TaLLon.exe --settings` — the settings app.
- `TaLLon.exe --send Special+W` — drive a running TaLLon from scripts.

Config lives at `%APPDATA%\TaLLon\config.json` and is hot-reloaded. The log is next to it.

## Testing

`tools/smoke-test.ps1` drives a running daemon end-to-end (enter, open two Notepads, tile,
restore, menu, close, exit) and screenshots each stage; `tools/ui-test.ps1` covers the settings
app and leader mode.

## Architecture

```
src/TaLLon.Core      class library, no UI
  Native/Win32.cs        P/Invoke surface (hooks, windows, DWM, SendInput)
  Input/                 KeyChord parsing, WH_KEYBOARD_LL hook, SpecialKeyEngine (Copilot chord,
                         hold/leader state machine)
  Windows/               window enumeration/filtering, SetWinEventHook watcher
  Layout/Layouts.cs      master-stack + monocle geometry
  Session/WindowManager  enter/exit state machine, adoption, tiling, restore
  Apps/AppCatalog.cs     Start menu + shell:AppsFolder enumeration, launching
  Config/                JSON config + file watcher
src/TaLLon.App       WPF: tray, canvas, OSD pill, command menu, settings window
memory/              project memory (plan, findings, decisions, progress) — read PROGRESS.md
```

Why not a fork? GlazeWM is Rust/GPL, komorebi is source-available only, FancyWM/workspacer are
always-on designs. TaLLon borrows their *techniques* (see `memory/decisions.md`) but the
"dormant until summoned + canvas" model needed its own core.

## Known limitations / concerns

- **Copilot key hold-repeat is untested on real hardware** (the author's tests inject the
  chord). If holding the key doesn't keep it "down", use tap/leader mode — it works either way.
- **Primary monitor only** in this pilot. Windows on other monitors are minimised on enter.
- "Replace the desktop" is implemented by minimising other windows and hiding the taskbar; a
  crash handler restores both, but if TaLLon is killed with Task Manager while active, run it
  once more and exit (or press `Special+W` twice) to get the taskbar back.
- Some apps ignore `SetWindowPos` sizes (minimum sizes, custom frames); they will overlap.
- Virtual desktops are not used (the APIs are undocumented and change per build).

## License

MIT — see [LICENSE](LICENSE).
