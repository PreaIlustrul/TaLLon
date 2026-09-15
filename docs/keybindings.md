# TaLLon key bindings

Everything is **Special + key**. *Special* is the Copilot key by default (configurable to
CapsLock, Right Alt, F13–F24, …). Two ways to use it:

- **Hold** it like Ctrl and press the command key.
- **Tap** it, then press the command key within the leader timeout (2.5 s default). A pill at
  the top of the screen shows that leader mode is armed.

| Binding | Action | vxwm equivalent |
|---|---|---|
| Special+W | Open / close TaLLon (enter or leave the canvas) | — |
| Special+Space | Command menu (exit, launch any app, focus a window, …) | Mod+p (dmenu) |
| Special+T | Tile / restore: arrange side by side ⇄ put windows back where they were | Mod+t / Mod+f |
| Special+Enter | Open terminal (Windows Terminal, falls back to PowerShell) | Mod+Shift+Return |
| Special+Q | Close focused window | Mod+Shift+c |
| Special+J / K | Focus next / previous window | Mod+j / k |
| Special+H / L | Shrink / grow the master area | Mod+h / l |
| Special+M | Toggle monocle (every window full-screen, one at a time) | Mod+m |
| Special+F | Toggle floating for the focused window | Mod+Shift+Space |
| Special+Z | Zoom focused window into the master slot | Mod+Return |
| Special+E | Launcher: File Explorer (example launcher) | — |
| Special+B | Launcher: default browser (example launcher) | — |
| Special+, | Open the settings app | — |
| Special+Shift+Q | Quit the background listener completely | Mod+Shift+q |

All of these are editable in **TaLLon Settings → Key bindings**, or directly in
`%APPDATA%\TaLLon\config.json`.

## Scripting

A running TaLLon can be driven from any script:

```
TaLLon.exe --send Special+W            # hold-style chord
TaLLon.exe --send Special+Space --leader   # tap-style chord
```
