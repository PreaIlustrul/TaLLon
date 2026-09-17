# TaLLon key bindings

**Special** is the Copilot key by default (configurable to CapsLock, Right Alt, F13–F24, …).

- **Tap** Special on its own (press and release, nothing in between): open / close the environment.
- **Hold** Special and press another key: run a command. Everything below is `Special+key`.

| Binding | Action | Mode |
|---|---|---|
| Special+Escape | Exit the environment (a tap of Special does the same) | both |
| Special+Space | Command menu: pinned apps, commands, open windows, every installed app | both |
| Special+M | Switch mode: infinite ⇄ tiling | both |
| Special+Tab | Overview: all windows in a grid; press again or click one to leave | both |
| Special+Home | Pan the view back to (0, 0) | infinite |
| Special+Enter | Open terminal (Windows Terminal, falls back to PowerShell) | both |
| Special+Q | Close focused window | both |
| Special+← → ↑ ↓ | Focus the window in that direction | both |
| Special+Shift+← → ↑ ↓ | Swap the focused window with the one in that direction | both |
| Special+J / K | Focus next / previous window | both |
| Special+P | Pin / unpin focused window (keeps its grid position between sessions) | infinite |
| Special+H | Hibernate / wake focused window (suspends its process) | both |
| Special+F | Toggle floating for the focused window | tiling |
| Special+Z | Zoom focused window into the master slot | tiling |
| Special+- / Special+= | Shrink / grow the master area | tiling |
| Special+E | Launcher: File Explorer (example) | both |
| Special+B | Launcher: browser (example) | both |
| Special+, | Open the TaLLon window (settings) | both |
| Special+Shift+Q | Quit TaLLon completely | both |

Inside the environment TaLLon also takes over a few Windows shortcuts:

| Shortcut | Inside the environment |
|---|---|
| Alt+Tab, Win+Tab | Overview (same as Special+Tab) |
| Win (tap) | Command menu |
| Win+D | Un-focus everything |

Mouse: click a window to focus it; click the canvas to focus nothing; right-click a window's title bar
for pin / hibernate / float / close; drag windows freely (infinite mode); two-finger scroll over the
canvas or push the mouse against a screen edge to pan.

All of these are editable in **TaLLon → Key bindings**, or directly in `%APPDATA%\TaLLon\config.json`.

## Scripting

A running TaLLon can be driven from any script:

```
TaLLon.exe --send Special            # tap: open / close the environment
TaLLon.exe --send Special+M          # any chord
TaLLon.exe --restore                 # put the desktop back (taskbar, work area, windows)
```
