# Task plan (phases)

Legend: [ ] todo  [~] in progress  [x] done  [!] blocked/concern

## Phase 0 — Setup (2026-09-15)
- [x] Toolchain, project folder, memory files, git, public GitHub repo (renamed to TaLLon 2026-09-16)

## Phase 1 — Research
- [x] vxwm semantics, Copilot key behaviour, prior-art survey, fork-vs-own decision (D1)

## Phase 2/3 — v0.1 pilot (2026-09-15) — superseded by v0.2, kept for history
- [x] Core (hook, chord engine, tiling session), WPF app (canvas, menu, OSD, settings, tray)

## Phase 4 — v0.2 rework from user feedback (2026-09-16)
- [x] No leader mode: tap = enter/exit, hold+key = command (D12)
- [x] Copilot chord: hold LWin back until F23 / other key / LWin-up, no timer (D13)
- [x] Hook on its own thread; rehook on resume, session switch, every 5 min
- [x] One app: main window with Launch button + settings; close → tray; single instance
- [x] Naming: TaLLon everywhere (exe, tray, log, icon, repo, folder)
- [x] Start with Windows (Run key, `--tray`), Start-menu shortcut (self-repairing), desktop shortcut
- [x] Everything managed: no minimising; existing windows cascade in the centre; exit restores placements
- [x] Crash-proof: session.json + watchdog process + restore on next start + Diagnostics button
- [x] Infinite mode: world coords, viewport, edge pan, scroll pan, spawn at 1/12, Go home, coordinates
- [x] Tiling mode: master-stack, Special+arrows focus, Special+Shift+arrows swap, float, zoom
- [x] Mode switch keeps positions both ways
- [x] Overview grid (Special+Tab, Alt+Tab, Win+Tab); click a window to jump to it
- [x] Top bar (coords, mode, menu/home/settings/overview/mode, clock, wifi/bt/volume/battery, exit)
- [x] Focus ring; click canvas = no focus (focus sink window)
- [x] Right-click title bar → pin / hibernate / float / close
- [x] Pinned windows persisted (pinned-windows.json), hibernated on re-entry
- [x] Min/max semantics: minimise a maximised window → normal; minimise a normal window → no-op
      (infinite) / leaves the grid (tiling)
- [x] Menu: icons everywhere, pinned apps section; settings page to pin apps
- [x] Launcher rows: Browse → app picker (all installed apps, searchable, icons) + file browse
- [x] Diagnostics page: raw key log, restore desktop, log folder
- [x] Smoke test rewritten incl. kill test; all passing on this laptop

## Phase 5 — Open questions for the user (see progress.md 2026-09-16 summary)
- [ ] Q1 "content always as if maximised, only shrunk" → needs thumbnail rendering (D14). Go / no-go?
- [ ] Q2 Two-button title bars (close + max/min) — only feasible with custom frames. Skip?
- [ ] Q3 Touchpad 3-finger gestures: map to shortcuts in Windows Settings and bind? (D15)
- [ ] Q4 Tab+drag resize (both modes) — needs a mouse hook that swallows drags; next?
- [ ] Q5 Window groups — proposal in progress.md

## Phase 6 — Next candidates
- [ ] Tab+drag resize; tiling resize that re-flows neighbours
- [ ] Thumbnail-based scaled windows (if Q1 = go)
- [ ] Multi-monitor
- [ ] Bar: click volume/wifi/bt to open the right Settings page; battery time remaining
- [ ] Menu: fuzzy matching, recent apps
- [ ] GitHub Actions release build, installer
