using TaLLon.Core.Config;
using TaLLon.Core.Layout;
using TaLLon.Core.Native;
using TaLLon.Core.Windows;

namespace TaLLon.Core.Session;

public sealed class ManagedWindow
{
    public nint Hwnd;
    public uint Pid;
    public string ExePath = "";
    /// <summary>Infinite-mode rectangle in WORLD coordinates (visible frame).</summary>
    public Win32.RECT World;
    public bool Pinned, Hibernated, Floating, HiddenFromTiling, ExistedAtEnter;
    /// <summary>Placement before the environment opened; restored at Exit.</summary>
    public Win32.WINDOWPLACEMENT Original;
    public string Title => Win32.GetWindowTitle(Hwnd);
    public bool Alive => Win32.IsWindow(Hwnd);
    public override string ToString() => $"0x{Hwnd:X} \"{Title}\"{(Pinned ? " pin" : "")}{(Hibernated ? " zz" : "")}{(Floating ? " float" : "")}";
}

/// <summary>
/// The TaLLon environment. Enter() takes over the screen (every open window becomes a managed
/// window on the canvas), Exit() puts everything back exactly as it was.
///
/// Two modes:
///  * Infinite — windows live on an unbounded plane; the screen is a viewport (<see cref="Viewport"/>).
///  * Tiling   — dwm/vxwm master-stack over the visible area.
///
/// Single-threaded: call only from the UI thread (WinEvent callbacks already arrive there).
/// </summary>
public sealed class Environment
{
    private readonly WinEventWatcher _events;
    private TallonConfig _cfg;
    private readonly HashSet<nint> _pendingAdopt = new();
    private SessionState? _session;
    private Win32.RECT _originalWorkArea;
    private nint _dragging;
    private Dictionary<nint, Win32.RECT>? _overviewSaved;
    private List<PinnedWindowRecord> _pinnedRecords = new();

    public bool Active { get; private set; }
    public bool OverviewActive { get; private set; }
    public EnvironmentMode Mode { get; private set; } = EnvironmentMode.Infinite;
    public List<ManagedWindow> Managed { get; } = new();
    public nint Focused { get; private set; }
    /// <summary>World coordinate of the screen's top-left corner (infinite mode).</summary>
    public Win32.POINT Viewport { get; private set; }
    public double MasterRatio { get; private set; }
    public int MasterCount { get; private set; }
    /// <summary>Height of the top bar in physical pixels (set by the app); the usable area starts below it.</summary>
    public int TopBarHeight { get; set; }
    /// <summary>A window of ours that can take focus so that "nothing" is focused (clicking the canvas).</summary>
    public nint FocusSink { get; set; }

    /// <summary>The app plugs in its "do this later on the UI thread" primitive (WPF Dispatcher).</summary>
    public Action<int, Action> Delay { get; set; } = (ms, a) => Task.Delay(ms).ContinueWith(_ => a());

    public event Action? Entered;
    public event Action? Exited;
    public event Action? Changed;               // managed list / mode / overview changed
    public event Action<string>? Notify;        // short user-facing status text
    public event Action<nint>? FocusChanged;    // 0 = nothing focused
    public event Action? FocusRectChanged;      // focused window moved/resized
    public event Action? ViewportChanged;
    public event Action<bool>? DragStateChanged;

    public Environment(WinEventWatcher events, TallonConfig cfg)
    {
        _events = events;
        _cfg = cfg;
        MasterRatio = cfg.Layout.MasterRatio;
        MasterCount = cfg.Layout.MasterCount;
        _events.Event += OnWindowEvent;
    }

    public void ApplyConfig(TallonConfig cfg)
    {
        _cfg = cfg;
        MasterRatio = cfg.Layout.MasterRatio;
        MasterCount = cfg.Layout.MasterCount;
        if (Active && Mode == EnvironmentMode.Tiling) Retile();
    }

    public Win32.RECT Monitor => Win32.GetPrimaryMonitorRect(workArea: false);
    /// <summary>Usable screen area (monitor minus the top bar), in SCREEN coordinates.</summary>
    public Win32.RECT Area { get { var m = Monitor; return new Win32.RECT(m.Left, m.Top + TopBarHeight, m.Right, m.Bottom); } }

    public Win32.RECT ToScreen(Win32.RECT world) => world.Offset(-Viewport.X, -Viewport.Y);
    public Win32.RECT ToWorld(Win32.RECT screen) => screen.Offset(Viewport.X, Viewport.Y);

    // =====================================================================================
    // enter / exit
    // =====================================================================================

    public void Toggle() { if (Active) Exit(); else Enter(); }

    public void Enter()
    {
        if (Active) return;
        Log.Info("ENTER");
        Active = true;
        OverviewActive = false;
        Mode = _cfg.DefaultMode;
        Viewport = new Win32.POINT();
        Managed.Clear();
        _pendingAdopt.Clear();
        Focused = 0;
        _pinnedRecords = PinnedStore.Load();

        var wins = WindowQuery.All();               // z-order, topmost first
        _originalWorkArea = Win32.GetWorkArea();
        _session = new SessionState
        {
            OwnerPid = System.Environment.ProcessId,
            WorkArea = SavedRect.From(_originalWorkArea),
            TaskbarHidden = _cfg.HideTaskbar,
        };
        foreach (var w in wins)
        {
            var wp = Win32.GetPlacement(w.Hwnd);
            _session.Windows.Add(new SavedWindow { Hwnd = w.Hwnd, Title = w.Title, ShowCmd = wp.showCmd, Flags = wp.flags, Normal = SavedRect.From(wp.rcNormalPosition) });
        }
        _session.Save();
        Watchdog.Spawn();

        if (_cfg.HideTaskbar) HideTaskbars();
        Win32.SetWorkArea(Area);

        // Every open program becomes a window on the canvas, cascaded in the middle (bottom-most first
        // so the previously-top window ends up on top of the pile).
        var area = Area;
        int i = 0;
        foreach (var w in Enumerable.Reverse(wins))
        {
            var mw = new ManagedWindow
            {
                Hwnd = w.Hwnd, Pid = w.Pid, ExePath = Win32.GetProcessPath(w.Pid),
                Original = Win32.GetPlacement(w.Hwnd), ExistedAtEnter = true,
            };
            if (w.Minimized || w.Maximized) Win32.ShowNormalNoActivate(w.Hwnd);
            var size = ClampSize(Win32.GetVisibleRect(w.Hwnd), area);
            var pin = FindPinned(mw);
            if (pin != null)
            {
                mw.Pinned = true;
                mw.World = pin.World.ToRect();
            }
            else
            {
                mw.World = ToWorld(Layouts.Cascade(area, size.w, size.h, i++, _cfg.Infinite.CascadeOffset));
            }
            Managed.Add(mw);
        }
        // Managed is bottom-most first; keep "index 0 = top" like dwm's master for tiling.
        Managed.Reverse();

        if (Mode == EnvironmentMode.Tiling) Retile(); else ApplyWorldAll();
        foreach (var mw in Managed.Where(m => m.Pinned)) Hibernate(mw, true);

        var fg = Win32.GetForegroundWindow();
        Focused = Find(fg) != null ? fg : 0;
        Entered?.Invoke();              // the app shows the canvas at the top of the z-order here
        RaiseManagedAboveCanvas();
        Changed?.Invoke();
        FocusChanged?.Invoke(Focused);
    }

    /// <summary>Lift every managed window above the canvas, bottom-most first so their relative order survives.</summary>
    public void RaiseManagedAboveCanvas()
    {
        for (int i = Managed.Count - 1; i >= 0; i--)
        {
            var m = Managed[i];
            if (!m.Alive) continue;
            WithAwake(new[] { m }, () => Win32.SetWindowPos(m.Hwnd, Win32.HWND_TOP, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE));
        }
    }

    public void Exit()
    {
        if (!Active) return;
        Log.Info("EXIT");
        Active = false;
        OverviewActive = false;
        try
        {
            SavePinned();
            foreach (var m in Managed.Where(m => m.Hibernated)) Win32.ResumeProcess(m.Pid);
            var mon = Monitor;
            foreach (var m in Managed)
            {
                if (!m.Alive) continue;
                if (m.ExistedAtEnter)
                {
                    var wp = m.Original;
                    wp.length = System.Runtime.InteropServices.Marshal.SizeOf<Win32.WINDOWPLACEMENT>();
                    if (wp.showCmd == Win32.SW_SHOWMINIMIZED) wp.showCmd = Win32.SW_SHOWMINNOACTIVE;
                    else if (wp.showCmd != Win32.SW_SHOWMAXIMIZED) wp.showCmd = Win32.SW_SHOWNOACTIVATE;
                    Win32.SetWindowPlacement(m.Hwnd, ref wp);
                }
                else
                {
                    // Spawned inside the environment: make sure it is reachable on the normal desktop.
                    var r = Win32.GetVisibleRect(m.Hwnd);
                    if (r.Right < mon.Left + 100 || r.Left > mon.Right - 100 || r.Bottom < mon.Top + 100 || r.Top > mon.Bottom - 100)
                    {
                        var size = ClampSize(r, mon);
                        Win32.SetVisibleRect(m.Hwnd, Win32.RECT.FromSize(mon.CenterX - size.w / 2, mon.CenterY - size.h / 2, size.w, size.h));
                    }
                }
            }
            Managed.Clear();
        }
        catch (Exception ex) { Log.Error("exit: " + ex); }
        finally
        {
            try { Win32.SetWorkArea(_originalWorkArea.Width > 0 ? _originalWorkArea : Monitor); } catch { }
            CrashRestore.ShowTaskbars();
            SessionState.Delete();
            Watchdog.Stop();
            Focused = 0;
            Exited?.Invoke();
            Changed?.Invoke();
            FocusChanged?.Invoke(0);
        }
    }

    /// <summary>Called from process-exit / crash handlers: restore the desktop no matter what.</summary>
    public void EmergencyRestore()
    {
        try { if (Active) Exit(); } catch { }
        try { CrashRestore.Run("emergency"); } catch { }
    }

    private static void HideTaskbars()
    {
        var main = Win32.FindWindow("Shell_TrayWnd", null);
        if (main != 0) Win32.ShowWindow(main, Win32.SW_HIDE);
        nint sec = 0;
        while ((sec = Win32.FindWindowEx(0, sec, "Shell_SecondaryTrayWnd", null)) != 0) Win32.ShowWindow(sec, Win32.SW_HIDE);
    }

    private static (int w, int h) ClampSize(Win32.RECT r, Win32.RECT area)
    {
        int w = Math.Clamp(r.Width, 300, (int)(area.Width * 0.85));
        int h = Math.Clamp(r.Height, 200, (int)(area.Height * 0.85));
        return (w, h);
    }

    // =====================================================================================
    // window events
    // =====================================================================================

    private void OnWindowEvent(WindowEventKind kind, nint hwnd)
    {
        if (!Active) return;
        var m = Find(hwnd);
        switch (kind)
        {
            case WindowEventKind.Shown:
            case WindowEventKind.Uncloaked:
            case WindowEventKind.NameChanged:
                if (m == null) ScheduleAdopt(hwnd);
                break;

            case WindowEventKind.Foreground:
                if (m != null)
                {
                    Focused = hwnd;
                    if (m.Hibernated) Hibernate(m, false);
                    if (OverviewActive) ExitOverview(hwnd);
                    FocusChanged?.Invoke(hwnd);
                }
                else if (hwnd == FocusSink || WindowQuery.IsOwnWindow?.Invoke(hwnd) == true)
                {
                    Focused = 0;
                    FocusChanged?.Invoke(0);
                }
                else ScheduleAdopt(hwnd);
                break;

            case WindowEventKind.Destroyed:
            case WindowEventKind.Hidden:
            case WindowEventKind.Cloaked:
                if (Remove(hwnd)) { if (Mode == EnvironmentMode.Tiling) Retile(); Changed?.Invoke(); FocusChanged?.Invoke(Focused); }
                break;

            case WindowEventKind.MinimizeStart:
                if (m != null) OnMinimize(m);
                break;

            case WindowEventKind.MinimizeEnd:
                if (m != null) { m.HiddenFromTiling = false; if (Mode == EnvironmentMode.Tiling) Retile(); else ApplyWorld(m); Changed?.Invoke(); }
                else ScheduleAdopt(hwnd);
                break;

            case WindowEventKind.MoveSizeStart:
                if (m != null) { _dragging = hwnd; DragStateChanged?.Invoke(true); }
                break;

            case WindowEventKind.MoveSizeEnd:
                if (m != null)
                {
                    _dragging = 0;
                    DragStateChanged?.Invoke(false);
                    if (Mode == EnvironmentMode.Infinite || m.Floating)
                        m.World = ToWorld(Win32.GetVisibleRect(hwnd));
                    else if (Mode == EnvironmentMode.Tiling)
                    {
                        // Dragged a tile with the mouse: it becomes floating where it was dropped (vxwm behaviour).
                        m.Floating = true; m.World = ToWorld(Win32.GetVisibleRect(hwnd));
                        Retile();
                    }
                    Changed?.Invoke();
                    if (hwnd == Focused) FocusRectChanged?.Invoke();
                }
                break;

            case WindowEventKind.LocationChanged:
                if (hwnd == Focused && m != null) FocusRectChanged?.Invoke();
                break;
        }
    }

    private void OnMinimize(ManagedWindow m)
    {
        bool wasMax = Win32.IsRestoreToMaximized(m.Hwnd);
        Delay(40, () =>
        {
            if (!Active || !m.Alive) return;
            if (wasMax)
            {
                // "Minimising a maximised window returns it to its previous size and place."
                Win32.ShowNormalNoActivate(m.Hwnd);
                if (Mode == EnvironmentMode.Tiling && !m.Floating) Retile(); else ApplyWorld(m);
                Win32.ForceForeground(m.Hwnd);
            }
            else if (Mode == EnvironmentMode.Infinite)
            {
                // "Minimising a window that is not maximised does nothing."
                Win32.ShowWindowAsync(m.Hwnd, Win32.SW_RESTORE);
            }
            else
            {
                // Tiling: the window leaves the grid but keeps its infinite-mode position.
                m.HiddenFromTiling = true;
                Retile();
                Changed?.Invoke();
            }
        });
    }

    private void ScheduleAdopt(nint hwnd)
    {
        if (_pendingAdopt.Contains(hwnd)) return;
        _pendingAdopt.Add(hwnd);
        // Many apps show first and size/title themselves a moment later.
        Delay(150, () =>
        {
            _pendingAdopt.Remove(hwnd);
            if (!Active || Find(hwnd) != null) return;
            if (!WindowQuery.IsManageable(hwnd)) return;
            Adopt(hwnd);
        });
    }

    private void Adopt(nint hwnd)
    {
        var area = Area;
        var mw = new ManagedWindow
        {
            Hwnd = hwnd, Pid = Win32.GetProcessId(hwnd), ExePath = "", ExistedAtEnter = false,
            Original = Win32.GetPlacement(hwnd),
        };
        mw.ExePath = Win32.GetProcessPath(mw.Pid);
        if (Win32.IsZoomed(hwnd)) Win32.ShowNormalNoActivate(hwnd);

        // New windows spawn in the centre of the view at 1/12 of the screen area, above everything.
        double side = Math.Sqrt(_cfg.Infinite.NewWindowAreaFraction);
        int w = Math.Max(320, (int)(area.Width * side)), h = Math.Max(240, (int)(area.Height * side));
        var spawn = Win32.RECT.FromSize(area.CenterX - w / 2, area.CenterY - h / 2, w, h);
        mw.World = ToWorld(spawn);

        var pin = FindPinned(mw);
        if (pin != null) { mw.Pinned = true; mw.World = pin.World.ToRect(); }

        Managed.Insert(0, mw);    // index 0 = top / master
        Log.Info("adopt " + mw);
        if (Mode == EnvironmentMode.Tiling) Retile();
        else
        {
            Win32.SetVisibleRect(hwnd, ToScreen(mw.World), activate: false, insertAfter: Win32.HWND_TOP, keepZ: false);
        }
        Focused = hwnd;
        Changed?.Invoke();
        FocusChanged?.Invoke(hwnd);
    }

    public ManagedWindow? Find(nint hwnd) => hwnd == 0 ? null : Managed.Find(m => m.Hwnd == hwnd);

    private bool Remove(nint hwnd)
    {
        int n = Managed.RemoveAll(m => m.Hwnd == hwnd);
        if (n > 0 && Focused == hwnd) Focused = 0;
        return n > 0;
    }

    // =====================================================================================
    // infinite mode
    // =====================================================================================

    /// <summary>Move the viewport by (dx, dy) world pixels: every window slides the opposite way.</summary>
    public void Pan(int dx, int dy)
    {
        if (!Active || Mode != EnvironmentMode.Infinite || OverviewActive || (dx == 0 && dy == 0)) return;
        Viewport = new Win32.POINT { X = Viewport.X + dx, Y = Viewport.Y + dy };
        ApplyWorldAll();
        ViewportChanged?.Invoke();
        if (Focused != 0) FocusRectChanged?.Invoke();
    }

    public void GoHome()
    {
        if (!Active || Mode != EnvironmentMode.Infinite) return;
        Viewport = new Win32.POINT();
        ApplyWorldAll();
        ViewportChanged?.Invoke();
        Notify?.Invoke("(0, 0)");
    }

    /// <summary>Pan so that the window's centre is the centre of the view.</summary>
    public void CenterOn(ManagedWindow m)
    {
        var area = Area;
        Viewport = new Win32.POINT { X = m.World.CenterX - area.CenterX, Y = m.World.CenterY - area.CenterY };
        ApplyWorldAll();
        ViewportChanged?.Invoke();
    }

    private void ApplyWorld(ManagedWindow m)
    {
        if (!m.Alive || m.Hwnd == _dragging) return;
        if (Win32.IsIconic(m.Hwnd) || Win32.IsZoomed(m.Hwnd)) return;
        WithAwake(new[] { m }, () => Win32.SetVisibleRect(m.Hwnd, ToScreen(m.World)));
    }

    private void ApplyWorldAll()
    {
        var items = new List<(nint, Win32.RECT)>();
        var wake = new List<ManagedWindow>();
        foreach (var m in Managed)
        {
            if (!m.Alive || m.Hwnd == _dragging || Win32.IsIconic(m.Hwnd) || Win32.IsZoomed(m.Hwnd)) continue;
            if (Mode == EnvironmentMode.Tiling && !m.Floating) continue;
            if (m.Hibernated) wake.Add(m);
            items.Add((m.Hwnd, ToScreen(m.World)));
        }
        WithAwake(wake, () => Win32.SetVisibleRects(items));
    }

    /// <summary>A suspended process cannot answer window messages; wake it around a move, then re-suspend.</summary>
    private static void WithAwake(IEnumerable<ManagedWindow> windows, Action act)
    {
        var slept = windows.Where(m => m.Hibernated).ToList();
        foreach (var m in slept) Win32.ResumeProcess(m.Pid);
        try { act(); }
        finally { foreach (var m in slept) Win32.SuspendProcess(m.Pid); }
    }

    // =====================================================================================
    // overview
    // =====================================================================================

    public void ToggleOverview() { if (OverviewActive) ExitOverview(0); else EnterOverview(); }

    public void EnterOverview()
    {
        if (!Active || OverviewActive) return;
        var live = Managed.Where(m => m.Alive && !Win32.IsIconic(m.Hwnd)).ToList();
        if (live.Count == 0) return;
        _overviewSaved = new Dictionary<nint, Win32.RECT>();
        foreach (var m in live)
        {
            if (Win32.IsZoomed(m.Hwnd)) Win32.ShowNormalNoActivate(m.Hwnd);
            _overviewSaved[m.Hwnd] = Mode == EnvironmentMode.Infinite ? m.World : ToWorld(Win32.GetVisibleRect(m.Hwnd));
        }
        var rects = Layouts.Grid(Area, live.Count, Math.Max(16, _cfg.Layout.Gap * 3));
        var items = live.Select((m, i) => (m.Hwnd, rects[i])).ToList();
        WithAwake(live, () => Win32.SetVisibleRects(items));
        OverviewActive = true;
        Notify?.Invoke("Overview");
        Changed?.Invoke();
        FocusRectChanged?.Invoke();
    }

    public void ExitOverview(nint focusHwnd)
    {
        if (!OverviewActive) return;
        OverviewActive = false;
        var saved = _overviewSaved ?? new();
        _overviewSaved = null;
        foreach (var m in Managed)
            if (saved.TryGetValue(m.Hwnd, out var r)) { if (Mode == EnvironmentMode.Infinite) m.World = r; else if (m.Floating) m.World = r; }
        var target = Find(focusHwnd);
        if (Mode == EnvironmentMode.Infinite)
        {
            if (target != null) CenterOn(target); else ApplyWorldAll();
        }
        else Retile();
        if (target != null) Focus(target.Hwnd);
        Changed?.Invoke();
        FocusRectChanged?.Invoke();
    }

    // =====================================================================================
    // modes
    // =====================================================================================

    public void SwitchMode()
    {
        if (!Active) return;
        if (OverviewActive) ExitOverview(0);
        if (Mode == EnvironmentMode.Infinite)
        {
            // World rects are already up to date (kept in sync on every move); just tile.
            Mode = EnvironmentMode.Tiling;
            Retile();
            Notify?.Invoke("Tiling mode");
        }
        else
        {
            Mode = EnvironmentMode.Infinite;
            foreach (var m in Managed) m.HiddenFromTiling = false;
            ApplyWorldAll();
            Notify?.Invoke("Infinite mode");
        }
        Changed?.Invoke();
        FocusRectChanged?.Invoke();
    }

    public void Retile()
    {
        if (!Active || Mode != EnvironmentMode.Tiling || OverviewActive) return;
        Managed.RemoveAll(m => !m.Alive);
        var tiled = Managed.Where(m => !m.Floating && !m.HiddenFromTiling && !Win32.IsIconic(m.Hwnd)).ToList();
        var rects = Layouts.MasterStack(Area, tiled.Count, MasterCount, MasterRatio, _cfg.Layout.Gap);
        var items = new List<(nint, Win32.RECT)>();
        for (int i = 0; i < tiled.Count; i++)
        {
            if (Win32.IsZoomed(tiled[i].Hwnd)) Win32.ShowNormalNoActivate(tiled[i].Hwnd);
            items.Add((tiled[i].Hwnd, rects[i]));
        }
        WithAwake(tiled, () => Win32.SetVisibleRects(items));
        FocusRectChanged?.Invoke();
    }

    public void ToggleFloatFocused()
    {
        var m = Find(Focused); if (m == null || Mode != EnvironmentMode.Tiling) return;
        m.Floating = !m.Floating;
        if (m.Floating) ApplyWorld(m);
        Retile();
        Notify?.Invoke(m.Floating ? "Floating" : "Tiled");
        Changed?.Invoke();
    }

    public void AdjustMaster(double delta)
    {
        MasterRatio = Math.Clamp(MasterRatio + delta, 0.1, 0.9);
        Retile();
    }

    public void ZoomToMaster()
    {
        var m = Find(Focused); if (m == null) return;
        Managed.Remove(m); Managed.Insert(0, m);
        Retile(); Changed?.Invoke();
    }

    // =====================================================================================
    // focus / navigation
    // =====================================================================================

    private List<ManagedWindow> Visible() =>
        Managed.Where(m => m.Alive && !Win32.IsIconic(m.Hwnd) && (Mode == EnvironmentMode.Infinite || !m.HiddenFromTiling)).ToList();

    public void FocusDirection(Direction dir)
    {
        var vis = Visible();
        var from = Find(Focused);
        if (vis.Count == 0) return;
        if (from == null) { Focus(vis[0].Hwnd); return; }
        var rects = vis.Select(m => Win32.GetVisibleRect(m.Hwnd)).ToList();
        int i = Layouts.Neighbour(Win32.GetVisibleRect(from.Hwnd), rects, dir);
        if (i >= 0) Focus(vis[i].Hwnd);
    }

    public void SwapDirection(Direction dir)
    {
        var vis = Visible();
        var from = Find(Focused);
        if (from == null || vis.Count < 2) return;
        var rects = vis.Select(m => Win32.GetVisibleRect(m.Hwnd)).ToList();
        int i = Layouts.Neighbour(Win32.GetVisibleRect(from.Hwnd), rects, dir);
        if (i < 0) return;
        var other = vis[i];
        if (Mode == EnvironmentMode.Tiling && !from.Floating && !other.Floating)
        {
            int a = Managed.IndexOf(from), b = Managed.IndexOf(other);
            (Managed[a], Managed[b]) = (Managed[b], Managed[a]);
            Retile();
        }
        else
        {
            (from.World, other.World) = (other.World, from.World);
            ApplyWorld(from); ApplyWorld(other);
        }
        Changed?.Invoke();
        FocusRectChanged?.Invoke();
    }

    public void FocusNext(int dir)
    {
        var vis = Visible();
        if (vis.Count == 0) return;
        int i = vis.FindIndex(m => m.Hwnd == Focused);
        i = i < 0 ? 0 : (i + dir + vis.Count) % vis.Count;
        Focus(vis[i].Hwnd);
    }

    public void Focus(nint hwnd)
    {
        var m = Find(hwnd);
        if (m == null) return;
        if (m.Hibernated) Hibernate(m, false);
        if (OverviewActive) { ExitOverview(hwnd); return; }
        Focused = hwnd;
        Win32.ForceForeground(hwnd);
        FocusChanged?.Invoke(hwnd);
        Changed?.Invoke();
    }

    /// <summary>Clicking the canvas: no window is focused.</summary>
    public void Unfocus()
    {
        if (!Active) return;
        Focused = 0;
        if (FocusSink != 0) Win32.ForceForeground(FocusSink);
        FocusChanged?.Invoke(0);
    }

    public void CloseFocused()
    {
        var h = Focused != 0 ? Focused : Win32.GetForegroundWindow();
        var m = Find(h);
        if (m == null) return;
        if (m.Hibernated) Hibernate(m, false);
        Win32.PostMessage(h, Win32.WM_CLOSE, 0, 0);
    }

    // =====================================================================================
    // pin / hibernate
    // =====================================================================================

    public void TogglePin(nint hwnd)
    {
        var m = Find(hwnd); if (m == null) return;
        m.Pinned = !m.Pinned;
        if (Mode == EnvironmentMode.Infinite) m.World = ToWorld(Win32.GetVisibleRect(hwnd));
        SavePinned();
        Notify?.Invoke(m.Pinned ? "Pinned" : "Unpinned");
        Changed?.Invoke();
    }

    public void ToggleHibernate(nint hwnd)
    {
        var m = Find(hwnd); if (m == null) return;
        Hibernate(m, !m.Hibernated);
        Notify?.Invoke(m.Hibernated ? "Hibernated" : "Awake");
        Changed?.Invoke();
    }

    private void Hibernate(ManagedWindow m, bool on)
    {
        if (m.Hibernated == on) return;
        var exe = Path.GetFileName(m.ExePath).ToLowerInvariant();
        if (on && (exe is "explorer.exe" or "tallon.exe" || m.Pid == (uint)System.Environment.ProcessId)) { Notify?.Invoke("Can't hibernate " + exe); return; }
        bool ok = on ? Win32.SuspendProcess(m.Pid) : Win32.ResumeProcess(m.Pid);
        if (!ok) { Log.Warn($"{(on ? "suspend" : "resume")} pid {m.Pid} failed"); return; }
        m.Hibernated = on;
        if (_session != null)
        {
            if (on) _session.SuspendedPids.Add(m.Pid); else _session.SuspendedPids.Remove(m.Pid);
            _session.Save();
        }
        Log.Info($"{(on ? "hibernate" : "wake")} {m}");
    }

    private PinnedWindowRecord? FindPinned(ManagedWindow m)
    {
        if (string.IsNullOrEmpty(m.ExePath)) return null;
        var title = m.Title;
        return _pinnedRecords.FirstOrDefault(p => p.ExePath.Equals(m.ExePath, StringComparison.OrdinalIgnoreCase)
                                                  && (p.Title == title || title.Contains(p.Title, StringComparison.OrdinalIgnoreCase)));
    }

    private void SavePinned()
    {
        var list = Managed.Where(m => m.Pinned && m.Alive)
            .Select(m => new PinnedWindowRecord { ExePath = m.ExePath, Title = m.Title, World = SavedRect.From(m.World) })
            .ToList();
        // keep records for pinned windows that are not running right now
        foreach (var p in _pinnedRecords)
            if (!list.Any(l => l.ExePath.Equals(p.ExePath, StringComparison.OrdinalIgnoreCase) && l.Title == p.Title)
                && !Managed.Any(m => m.ExePath.Equals(p.ExePath, StringComparison.OrdinalIgnoreCase) && !m.Pinned))
                list.Add(p);
        _pinnedRecords = list;
        PinnedStore.Save(list);
    }

    /// <summary>Windows alive right now, for the menu's "focus window" list.</summary>
    public IEnumerable<ManagedWindow> LiveWindows() => Managed.Where(m => m.Alive);
}
