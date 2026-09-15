using TaLLon.Core.Native;

namespace TaLLon.Core.Windows;

public enum WindowEventKind { Shown, Hidden, Destroyed, Foreground, MinimizeStart, MinimizeEnd, MoveSizeEnd, NameChanged, Uncloaked, Cloaked }

/// <summary>
/// SetWinEventHook (out-of-context) for window lifecycle. Callbacks arrive on the message loop of
/// the thread that called <see cref="Start"/> — create it on the UI thread and everything is single-threaded.
/// </summary>
public sealed class WinEventWatcher : IDisposable
{
    private readonly List<nint> _hooks = new();
    private readonly Win32.WinEventDelegate _proc; // keep alive

    public event Action<WindowEventKind, nint>? Event;

    public WinEventWatcher() { _proc = Callback; }

    public void Start()
    {
        if (_hooks.Count > 0) return;
        Hook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND);
        Hook(Win32.EVENT_SYSTEM_MOVESIZEEND, Win32.EVENT_SYSTEM_MOVESIZEEND);
        Hook(Win32.EVENT_SYSTEM_MINIMIZESTART, Win32.EVENT_SYSTEM_MINIMIZEEND);
        Hook(Win32.EVENT_OBJECT_DESTROY, Win32.EVENT_OBJECT_HIDE);
        Hook(Win32.EVENT_OBJECT_NAMECHANGE, Win32.EVENT_OBJECT_NAMECHANGE);
        Hook(Win32.EVENT_OBJECT_CLOAKED, Win32.EVENT_OBJECT_UNCLOAKED);
    }

    private void Hook(uint min, uint max)
    {
        var h = Win32.SetWinEventHook(min, max, 0, _proc, 0, 0, Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
        if (h != 0) _hooks.Add(h);
    }

    private void Callback(nint hook, uint type, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != Win32.OBJID_WINDOW || idChild != 0 || hwnd == 0) return;
        WindowEventKind kind = type switch
        {
            Win32.EVENT_OBJECT_SHOW => WindowEventKind.Shown,
            Win32.EVENT_OBJECT_HIDE => WindowEventKind.Hidden,
            Win32.EVENT_OBJECT_DESTROY => WindowEventKind.Destroyed,
            Win32.EVENT_SYSTEM_FOREGROUND => WindowEventKind.Foreground,
            Win32.EVENT_SYSTEM_MINIMIZESTART => WindowEventKind.MinimizeStart,
            Win32.EVENT_SYSTEM_MINIMIZEEND => WindowEventKind.MinimizeEnd,
            Win32.EVENT_SYSTEM_MOVESIZEEND => WindowEventKind.MoveSizeEnd,
            Win32.EVENT_OBJECT_NAMECHANGE => WindowEventKind.NameChanged,
            Win32.EVENT_OBJECT_CLOAKED => WindowEventKind.Cloaked,
            Win32.EVENT_OBJECT_UNCLOAKED => WindowEventKind.Uncloaked,
            _ => (WindowEventKind)(-1),
        };
        if ((int)kind < 0) return;
        try { Event?.Invoke(kind, hwnd); }
        catch (Exception ex) { Config.Log.Error($"WinEvent handler threw ({kind}): {ex}"); }
    }

    public void Dispose()
    {
        foreach (var h in _hooks) Win32.UnhookWinEvent(h);
        _hooks.Clear();
    }
}
