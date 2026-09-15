# TaLLon — Long-term goals

TaLLon is an on-demand window manager for Windows 11. It stays dormant until a special key
(Copilot key by default) is used, then takes over the screen with a canvas + tiling/floating
window management, and hands the desktop back when dismissed.

## North star
A daily-driver window manager for a Windows 11 laptop that feels like dwm/vxwm but is
opt-in: nothing changes about Windows until you summon it.

## Goals (split into tasks in task_plan.md)
- G1  Dormant-until-summoned: background listener, zero interference when inactive.
- G2  Special-key driven command language (Special + key), special key remappable.
- G3  Canvas mode: replaces desktop with a background image; windows opened inside are managed.
- G4  vxwm-style management: master/stack tiling, monocle, floating; Special+T toggles
      "tile" vs "restore to where they were".
- G5  Command menu (Special+Space): exit, launch any installed app, focus windows, actions.
- G6  Settings app ("TaLLon Settings"): OBS/DaVinci-like dark UI to edit keys, background,
      launchers. Pilot quality first, polish later.
- G7  Stability: never lose the user's windows/taskbar; crash-safe restore.
- G8  Later: multi-monitor, infinite canvas (vxwm's signature), workspaces/tags, status bar,
      window rules, animations, per-app float rules, mouse drag support, run-at-login toggle.
