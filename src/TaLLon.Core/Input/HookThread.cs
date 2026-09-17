using System.Runtime.InteropServices;
using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.Core.Input;

public sealed class KeyEvent
{
    public int Vk;
    public uint ScanCode;
    public bool Up;
    public bool Injected;
    public bool FromTallon;
    public uint Time;
    public override string ToString() => $"{(Up ? "UP  " : "DOWN")} {KeyNames.NameOf(Vk)} (0x{Vk:X2}) sc=0x{ScanCode:X}{(Injected ? " inj" : "")}";
}

public sealed class MouseEvent
{
    public int Msg;      // WM_LBUTTONDOWN etc.
    public int X, Y;
    public int WheelDelta;
    public bool Injected;
}

/// <summary>
/// Dedicated thread that owns the low-level keyboard and mouse hooks and pumps their messages.
/// Keeping this off the UI thread means a busy WPF dispatcher can never stall the hook (Windows
/// silently removes low-level hooks whose callbacks time out). Handlers return true to swallow.
/// </summary>
public sealed class HookThread : IDisposable
{
    private readonly Func<KeyEvent, bool> _onKey;
    private readonly Func<MouseEvent, bool> _onMouse;
    private readonly Win32.HookProc _kbProc, _msProc;   // keep delegates alive
    private Thread? _thread;
    private uint _threadId;
    private nint _kbHook, _msHook;
    private volatile bool _mouseWanted;
    private readonly ManualResetEventSlim _ready = new();
    private const uint MSG_REHOOK = Win32.WM_APP + 1, MSG_MOUSE = Win32.WM_APP + 2;

    public HookThread(Func<KeyEvent, bool> onKey, Func<MouseEvent, bool> onMouse)
    {
        _onKey = onKey; _onMouse = onMouse;
        _kbProc = KeyboardProc; _msProc = MouseProc;
    }

    public void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(Loop) { IsBackground = true, Name = "TaLLon.Hooks", Priority = ThreadPriority.Highest };
        _thread.Start();
        _ready.Wait(3000);
    }

    /// <summary>Unhook + rehook (after sleep/resume, or periodically as insurance).</summary>
    public void Reinstall() { if (_threadId != 0) Win32.PostThreadMessage(_threadId, MSG_REHOOK, 0, 0); }

    /// <summary>The mouse hook is only installed while the environment is active (cheaper when idle).</summary>
    public void SetMouseHook(bool on) { _mouseWanted = on; if (_threadId != 0) Win32.PostThreadMessage(_threadId, MSG_MOUSE, 0, 0); }

    private void Loop()
    {
        _threadId = Win32.GetCurrentThreadId();
        Install();
        _ready.Set();
        while (Win32.GetMessage(out var msg, 0, 0, 0))
        {
            if (msg.message == MSG_REHOOK) { Uninstall(); Install(); continue; }
            if (msg.message == MSG_MOUSE) { UpdateMouse(); continue; }
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }
        Uninstall();
    }

    private void Install()
    {
        var mod = Win32.GetModuleHandle(null);
        _kbHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _kbProc, mod, 0);
        if (_kbHook == 0) Log.Error("keyboard hook failed: " + Marshal.GetLastWin32Error());
        UpdateMouse();
    }

    private void UpdateMouse()
    {
        if (_mouseWanted && _msHook == 0)
        {
            _msHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _msProc, Win32.GetModuleHandle(null), 0);
            if (_msHook == 0) Log.Error("mouse hook failed: " + Marshal.GetLastWin32Error());
        }
        else if (!_mouseWanted && _msHook != 0)
        {
            Win32.UnhookWindowsHookEx(_msHook); _msHook = 0;
        }
    }

    private void Uninstall()
    {
        if (_kbHook != 0) { Win32.UnhookWindowsHookEx(_kbHook); _kbHook = 0; }
        if (_msHook != 0) { Win32.UnhookWindowsHookEx(_msHook); _msHook = 0; }
    }

    private nint KeyboardProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var k = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
            var ev = new KeyEvent
            {
                Vk = (int)k.vkCode,
                ScanCode = k.scanCode,
                Up = (k.flags & Win32.LLKHF_UP) != 0,
                Injected = (k.flags & Win32.LLKHF_INJECTED) != 0,
                FromTallon = k.dwExtraInfo == Win32.TALLON_INJECT_TAG,
                Time = k.time,
            };
            bool swallow;
            try { swallow = _onKey(ev); }
            catch (Exception ex) { Log.Error("key handler threw: " + ex); swallow = false; }
            if (swallow) return 1;
        }
        return Win32.CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private nint MouseProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var m = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;
            if (msg is Win32.WM_LBUTTONDOWN or Win32.WM_LBUTTONUP or Win32.WM_RBUTTONDOWN or Win32.WM_RBUTTONUP
                or Win32.WM_MOUSEWHEEL or Win32.WM_MOUSEHWHEEL)
            {
                var ev = new MouseEvent
                {
                    Msg = msg, X = m.pt.X, Y = m.pt.Y,
                    WheelDelta = (short)((m.mouseData >> 16) & 0xFFFF),
                    Injected = (m.flags & Win32.LLMHF_INJECTED) != 0,
                };
                bool swallow;
                try { swallow = _onMouse(ev); }
                catch (Exception ex) { Log.Error("mouse handler threw: " + ex); swallow = false; }
                if (swallow) return 1;
            }
        }
        return Win32.CallNextHookEx(_msHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_threadId != 0) Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, 0, 0);
        _thread?.Join(1000);
    }
}
