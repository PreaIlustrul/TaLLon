using System.Runtime.InteropServices;
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

/// <summary>
/// WH_KEYBOARD_LL wrapper. Must be created on a thread that pumps messages (the WPF UI thread).
/// The handler returns true to swallow the event.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private nint _hook;
    private readonly Win32.LowLevelKeyboardProc _proc; // keep delegate alive for the GC
    private readonly Func<KeyEvent, bool> _handler;

    public KeyboardHook(Func<KeyEvent, bool> handler)
    {
        _handler = handler;
        _proc = Callback;
    }

    public void Install()
    {
        if (_hook != 0) return;
        _hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _proc, Win32.GetModuleHandle(null), 0);
        if (_hook == 0) throw new InvalidOperationException("SetWindowsHookEx failed: " + Marshal.GetLastWin32Error());
    }

    private nint Callback(int nCode, nint wParam, nint lParam)
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
            try { swallow = _handler(ev); }
            catch (Exception ex) { Config.Log.Error("hook handler threw: " + ex); swallow = false; }
            if (swallow) return 1;
        }
        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != 0) { Win32.UnhookWindowsHookEx(_hook); _hook = 0; }
    }
}
