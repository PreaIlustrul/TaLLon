# Findings (research, discoveries, constraints)

## vxwm (the reference behaviour)
- Repo: https://github.com/wh1tepearll/vxwm (mirror of codeberg). Dual MIT (dwm-derived).
- dwm fork. Layouts: tiled (master area left, stack right; nmaster & mfact adjustable), monocle
  (all windows maximised), floating (free; dialogs always float).
- New windows go to the master area; extra windows fill the stack.
- Signature feature: "infinite tags/canvas" — the screen is a viewport over a huge canvas.
- Default keys (Mod4): Shift+Return terminal; p dmenu; j/k focus next/prev; i/d nmaster +/-;
  h/l mfact; Return zoom to master; t/f/m layouts; space cycle layout; Shift+space toggle float;
  Shift+c close; 1-9 tags; Tab all tags; Shift+q quit.
  -> TaLLon maps these onto Special+key (see docs/keybindings.md).

## Copilot key
- Sends 3 events ~1 ms apart: LWin down (vk 0x5B, sc 0x15B), LShift down (vk 0xA0, sc 0x2A),
  F23 down (vk 0x86, sc 0x6E). Source: https://github.com/estereotipau/copilot-key-fix (MIT, AHK v2).
- Remapping only F23 (PowerToys/SharpKeys) fails: LWin/LShift still reach the OS.
- Working approach: LL hook, defer LWin for ~30 ms waiting for LShift+F23; swallow the chord.
- Windows 24H2+ Settings > Personalization > Text input can remap the Copilot key to launch an
  app; not needed for us but note it may change what the key sends if the user changed it.
- UNKNOWN until tested on this laptop: does holding the key auto-repeat F23? (D3 covers both.)

## Windows tiling WMs (prior art)
- GlazeWM https://github.com/glzr-io/glazewm — Rust, GPL-3, i3-like, YAML config, binding modes,
  recommends Alt over Win because the OS reserves Win combos.
- komorebi — Rust, custom licence (not freely forkable). Tiny RAM, BSP layouts, hides taskbar
  optionally, corrects DWM invisible borders (DWMWA_EXTENDED_FRAME_BOUNDS).
- FancyWM https://github.com/FancyWM/fancywm — C#, activation via Shift+Win command overlay,
  two-pass layout that respects WM_GETMINMAXINFO constraints. Good idea to copy later.
- workspacer — C#, MIT, unmaintained. Whim — C#, plugin architecture, comparison page:
  https://dalyisaac.github.io/Whim/intro/comparison.html
- PowerToys FancyZones — MIT C++ zone-snapping, not a WM.

## Win32 techniques
- WH_KEYBOARD_LL runs on the installing thread's message loop; callback must return fast
  (LowLevelHooksTimeout). Dispatch work to UI thread asynchronously.
- SetWinEventHook (EVENT_OBJECT_SHOW/DESTROY, EVENT_SYSTEM_FOREGROUND, MINIMIZE*) with
  WINEVENT_OUTOFCONTEXT for window lifecycle without DLL injection.
- Windows 10/11 add ~7 px invisible resize borders to GetWindowRect; use
  DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS) to compute the delta before SetWindowPos.
- Cloaked windows (DWMWA_CLOAKED != 0) must be filtered out (UWP background, other virtual desktops).
- Taskbar hwnd class "Shell_TrayWnd" (+ "Shell_SecondaryTrayWnd" per monitor); hiding with
  ShowWindow is what komorebi does. Must restore on every exit path.
- Virtual desktop internals (IVirtualDesktopManagerInternal) change per build; avoided (D5).

## Environment
- Windows 11 Pro 10.0.26200, git 2.55, gh 2.97 (logged in as PreaIlustrul), node 24, winget.
- .NET 8 SDK 8.0.425 installed 2026-09-15 via winget (Microsoft.DotNet.SDK.8, source winget).
  dotnet is at C:\Program Files\dotnet (may need adding to PATH in a fresh Git Bash).
