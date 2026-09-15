using TaLLon.Core.Config;
using TaLLon.Core.Layout;
using TaLLon.Core.Native;
using TaLLon.Core.Windows;

namespace TaLLon.Core.Session;

public sealed class ManagedWindow
{
    public nint Hwnd;
    public Win32.RECT PreTileRect;   // where it was before we tiled it ("restore" target)
    public bool Floating;
    public bool WasMaximized;
    public string Title => Win32.GetWindowTitle(Hwnd);
    public override string ToString() => $"0x{Hwnd:X} \"{Title}\"{(Floating ? " float" : "")}";
}

/// <summary>
/// The session state machine (D5/D6): Enter() takes over the screen, Exit() gives it back,
/// and in between windows that appear are tiled vxwm-style on the primary monitor.
/// Single-threaded: call only from the UI thread (WinEvent callbacks already arrive there).
/// </summary>
public sealed class WindowManager
{
    private readonly WinEventWatcher _events;
    private TallonConfig _cfg;
    private readonly List<nint> _minimizedByUs = new();
    private readonly List<nint> _hiddenTaskbars = new();
    private readonly Dictionary<nint, Win32.RECT> _snapshot = new();   // rects of everything at Enter()
    private readonly HashSet<nint> _pendingAdopt = new();

    public bool Active { get; private set; }
    public LayoutMode Mode { get; private set; } = LayoutMode.Tiled;
    public List<ManagedWindow> Managed { get; } = new();
    public nint Focused { get; private set; }
    public double MasterRatio { get; private set; }
    public int MasterCount { get; private set; }

    /// <summary>The app plugs in its "do this later on the UI thread" primitive (WPF Dispatcher).</summary>
    public Action<int, Action> Delay { get; set; } = (ms, a) => Task.Delay(ms).ContinueWith(_ => a());

    public event Action? Entered;
    public event Action? Exited;
    public event Action? Changed;         // managed list / layout changed (for menus & OSD)
    public event Action<string>? Notify;  // short user-facing status text

    public WindowManager(WinEventWatcher events, TallonConfig cfg)
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
        if (Active) Retile();
    }

    public Win32.RECT Area => Win32.GetPrimaryMonitorRect(workArea: !_cfg.HideTaskbar);

    // ---- enter / exit --------------------------------------------------------------------

    public void Toggle() { if (Active) Exit(); else Enter(); }

    public void Enter()
    {
        if (Active) return;
        Log.Info("ENTER");
        Active = true;
        Managed.Clear();
        _minimizedByUs.Clear();
        _snapshot.Clear();
        Mode = _cfg.Layout.StartTiled ? LayoutMode.Tiled : LayoutMode.Floating;

        foreach (var w in WindowQuery.All())
        {
            _snapshot[w.Hwnd] = w.Rect;
            if (_cfg.MinimizeOthersOnEnter && !w.Minimized)
            {
                Win32.ShowWindowAsync(w.Hwnd, Win32.SW_SHOWMINNOACTIVE);
                _minimizedByUs.Add(w.Hwnd);
            }
        }
        if (_cfg.HideTaskbar) HideTaskbars();
        Entered?.Invoke();
        Changed?.Invoke();
    }

    public void Exit()
    {
        if (!Active) return;
        Log.Info("EXIT");
        Active = false;
        try
        {
            // Put tiled windows back where they were.
            foreach (var m in Managed)
            {
                if (!Win32.IsWindow(m.Hwnd)) continue;
                if (!m.Floating && Mode != LayoutMode.Floating) RestoreRect(m);
            }
            Managed.Clear();
            foreach (var h in _minimizedByUs)
                if (Win32.IsWindow(h) && Win32.IsIconic(h)) Win32.ShowWindowAsync(h, Win32.SW_SHOWNOACTIVATE);
            _minimizedByUs.Clear();
        }
        finally
        {
            ShowTaskbars();
            Exited?.Invoke();
            Changed?.Invoke();
        }
    }

    /// <summary>Called from process-exit / crash handlers: restore the desktop no matter what.</summary>
    public void EmergencyRestore()
    {
        try { if (Active) Exit(); } catch { /* best effort */ }
        try { ShowTaskbars(); } catch { }
    }

    private void HideTaskbars()
    {
        _hiddenTaskbars.Clear();
        var main = Win32.FindWindow("Shell_TrayWnd", null);
        if (main != 0) { Win32.ShowWindow(main, Win32.SW_HIDE); _hiddenTaskbars.Add(main); }
        nint sec = 0;
        while ((sec = Win32.FindWindowEx(0, sec, "Shell_SecondaryTrayWnd", null)) != 0)
        { Win32.ShowWindow(sec, Win32.SW_HIDE); _hiddenTaskbars.Add(sec); }
    }

    private void ShowTaskbars()
    {
        foreach (var h in _hiddenTaskbars) if (Win32.IsWindow(h)) Win32.ShowWindow(h, Win32.SW_SHOW);
        _hiddenTaskbars.Clear();
        // belt and braces: if we crashed earlier and lost the list
        var main = Win32.FindWindow("Shell_TrayWnd", null);
        if (main != 0 && !Win32.IsWindowVisible(main)) Win32.ShowWindow(main, Win32.SW_SHOW);
    }

    // ---- window events -------------------------------------------------------------------

    private void OnWindowEvent(WindowEventKind kind, nint hwnd)
    {
        if (!Active) return;
        switch (kind)
        {
            case WindowEventKind.Shown:
            case WindowEventKind.Uncloaked:
            case WindowEventKind.NameChanged:
            case WindowEventKind.MinimizeEnd:
                if (Find(hwnd) == null) ScheduleAdopt(hwnd);
                break;
            case WindowEventKind.Foreground:
                if (Find(hwnd) != null) { Focused = hwnd; Changed?.Invoke(); }
                else ScheduleAdopt(hwnd);
                break;
            case WindowEventKind.Destroyed:
            case WindowEventKind.Hidden:
            case WindowEventKind.Cloaked:
            case WindowEventKind.MinimizeStart:
                if (Remove(hwnd)) { Retile(); Changed?.Invoke(); }
                break;
            case WindowEventKind.MoveSizeEnd:
                // The user dragged/resized a tiled window with the mouse: treat it as floating (vxwm does the same).
                var m = Find(hwnd);
                if (m != null && !m.Floating && Mode == LayoutMode.Tiled)
                {
                    m.Floating = true; m.PreTileRect = Win32.GetVisibleRect(hwnd);
                    Retile(); Changed?.Invoke();
                }
                break;
        }
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
        var mw = new ManagedWindow
        {
            Hwnd = hwnd,
            PreTileRect = _snapshot.TryGetValue(hwnd, out var r) ? r : Win32.GetVisibleRect(hwnd),
            WasMaximized = Win32.IsZoomed(hwnd),
        };
        Managed.Insert(0, mw);    // new windows become master (dwm attach-at-head)
        Focused = hwnd;
        Log.Info("adopt " + mw);
        Retile();
        Changed?.Invoke();
    }

    private ManagedWindow? Find(nint hwnd) => Managed.Find(m => m.Hwnd == hwnd);
    private bool Remove(nint hwnd)
    {
        int n = Managed.RemoveAll(m => m.Hwnd == hwnd);
        if (n > 0 && Focused == hwnd) Focused = Managed.Count > 0 ? Managed[0].Hwnd : 0;
        return n > 0;
    }

    // ---- layout --------------------------------------------------------------------------

    public void Retile()
    {
        if (!Active) return;
        Managed.RemoveAll(m => !Win32.IsWindow(m.Hwnd));
        if (Mode == LayoutMode.Floating) return;

        var tiled = Managed.Where(m => !m.Floating).ToList();
        var area = Area;
        var rects = Mode == LayoutMode.Monocle
            ? Layouts.Monocle(area, tiled.Count, _cfg.Layout.Gap)
            : Layouts.MasterStack(area, tiled.Count, MasterCount, MasterRatio, _cfg.Layout.Gap);

        for (int i = 0; i < tiled.Count; i++)
        {
            var m = tiled[i];
            if (Win32.IsIconic(m.Hwnd)) continue;
            if (Win32.IsZoomed(m.Hwnd)) Win32.RestoreNoActivate(m.Hwnd);
            Win32.SetVisibleRect(m.Hwnd, rects[i]);
            Log.Info($"tile[{i}] {m} -> {rects[i]} (got {Win32.GetVisibleRect(m.Hwnd)})");
        }
    }

    private static void RestoreRect(ManagedWindow m)
    {
        if (m.WasMaximized) { Win32.ShowWindowAsync(m.Hwnd, Win32.SW_SHOWMAXIMIZED); return; }
        if (m.PreTileRect.Width > 0 && m.PreTileRect.Height > 0) Win32.SetVisibleRect(m.Hwnd, m.PreTileRect);
    }

    /// <summary>Special+T: tiled ⇄ "back where they were".</summary>
    public void ToggleTiling()
    {
        if (!Active) return;
        if (Mode == LayoutMode.Floating)
        {
            foreach (var m in Managed) { m.PreTileRect = Win32.GetVisibleRect(m.Hwnd); m.WasMaximized = Win32.IsZoomed(m.Hwnd); }
            Mode = LayoutMode.Tiled;
            Retile();
            Notify?.Invoke("Tiled");
        }
        else
        {
            Mode = LayoutMode.Floating;
            foreach (var m in Managed) if (!m.Floating) RestoreRect(m);
            Notify?.Invoke("Restored");
        }
        Changed?.Invoke();
    }

    public void ToggleMonocle()
    {
        if (!Active) return;
        Mode = Mode == LayoutMode.Monocle ? LayoutMode.Tiled : LayoutMode.Monocle;
        Retile();
        Notify?.Invoke(Mode == LayoutMode.Monocle ? "Monocle" : "Tiled");
        Changed?.Invoke();
    }

    public void ToggleFloatFocused()
    {
        var m = Find(Focused); if (m == null) return;
        m.Floating = !m.Floating;
        if (m.Floating) RestoreRect(m); else m.PreTileRect = Win32.GetVisibleRect(m.Hwnd);
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

    // ---- focus ---------------------------------------------------------------------------

    public void FocusNext(int dir)
    {
        var live = Managed.Where(m => Win32.IsWindow(m.Hwnd) && !Win32.IsIconic(m.Hwnd)).ToList();
        if (live.Count == 0) return;
        int i = live.FindIndex(m => m.Hwnd == Focused);
        i = i < 0 ? 0 : (i + dir + live.Count) % live.Count;
        Focus(live[i].Hwnd);
    }

    public void Focus(nint hwnd)
    {
        Focused = hwnd;
        Win32.ForceForeground(hwnd);
        Changed?.Invoke();
    }

    public void CloseFocused()
    {
        var h = Focused != 0 ? Focused : Win32.GetForegroundWindow();
        if (Find(h) != null) Win32.PostMessage(h, Win32.WM_CLOSE, 0, 0);
    }

    /// <summary>Windows alive right now, for the menu's "focus window" list.</summary>
    public IEnumerable<ManagedWindow> LiveWindows() => Managed.Where(m => Win32.IsWindow(m.Hwnd));
}
