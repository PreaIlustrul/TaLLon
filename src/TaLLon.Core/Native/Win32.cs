using System.Runtime.InteropServices;
using System.Text;

namespace TaLLon.Core.Native;

/// <summary>Raw Win32 P/Invoke surface used by TaLLon. Kept in one file on purpose.</summary>
public static class Win32
{
    // ---- structs -------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
        public override string ToString() => $"({Left},{Top})-({Right},{Bottom}) {Width}x{Height}";
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy; public uint mouseData, dwFlags, time; public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk, wScan; public uint dwFlags, time; public nuint dwExtraInfo;
    }

    // ---- constants -----------------------------------------------------------------------

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    public const int WM_CLOSE = 0x0010;
    public const uint LLKHF_EXTENDED = 0x01, LLKHF_INJECTED = 0x10, LLKHF_ALTDOWN = 0x20, LLKHF_UP = 0x80;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_SCANCODE = 0x0008;

    public const int VK_LBUTTON = 0x01, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12,
        VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1,
        VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5, VK_F23 = 0x86;

    public const int SW_HIDE = 0, SW_SHOWNORMAL = 1, SW_SHOWMINIMIZED = 2, SW_SHOWMAXIMIZED = 3,
        SW_SHOWNOACTIVATE = 4, SW_SHOW = 5, SW_MINIMIZE = 6, SW_SHOWMINNOACTIVE = 7,
        SW_SHOWNA = 8, SW_RESTORE = 9;

    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_VISIBLE = 0x10000000L, WS_MINIMIZE = 0x20000000L, WS_MAXIMIZE = 0x01000000L,
        WS_CAPTION = 0x00C00000L, WS_THICKFRAME = 0x00040000L, WS_POPUP = 0x80000000L, WS_CHILD = 0x40000000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L, WS_EX_APPWINDOW = 0x00040000L,
        WS_EX_NOACTIVATE = 0x08000000L, WS_EX_TOPMOST = 0x00000008L, WS_EX_TRANSPARENT = 0x00000020L,
        WS_EX_LAYERED = 0x00080000L;

    public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010,
        SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040, SWP_HIDEWINDOW = 0x0080, SWP_ASYNCWINDOWPOS = 0x4000;
    public static readonly nint HWND_TOP = 0, HWND_BOTTOM = 1, HWND_TOPMOST = -1, HWND_NOTOPMOST = -2;

    public const uint GW_OWNER = 4;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1, MONITOR_DEFAULTTONEAREST = 2;

    public const int DWMWA_CLOAKED = 14, DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // WinEvents
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_SYSTEM_MOVESIZEEND = 0x000B,
        EVENT_SYSTEM_MINIMIZESTART = 0x0016, EVENT_SYSTEM_MINIMIZEEND = 0x0017,
        EVENT_OBJECT_CREATE = 0x8000, EVENT_OBJECT_DESTROY = 0x8001, EVENT_OBJECT_SHOW = 0x8002,
        EVENT_OBJECT_HIDE = 0x8003, EVENT_OBJECT_LOCATIONCHANGE = 0x800B, EVENT_OBJECT_NAMECHANGE = 0x800C,
        EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000, WINEVENT_SKIPOWNPROCESS = 0x0002;
    public const int OBJID_WINDOW = 0;

    // ---- delegates -----------------------------------------------------------------------

    public delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);
    public delegate void WinEventDelegate(nint hWinEventHook, uint eventType, nint hwnd, int idObject,
        int idChild, uint dwEventThread, uint dwmsEventTime);

    // ---- user32 --------------------------------------------------------------------------

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(nint hhk);
    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]
    public static extern short GetKeyState(int vKey);
    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsWindow(nint hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsZoomed(nint hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(nint hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern nint GetWindow(nint hWnd, uint uCmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern nint GetWindowLongPtr(nint hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool GetWindowPlacement(nint hWnd, ref WINDOWPLACEMENT lpwndpl);
    [DllImport("user32.dll")]
    public static extern bool SetWindowPlacement(nint hWnd, ref WINDOWPLACEMENT lpwndpl);
    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(nint hWnd);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]
    public static extern bool PostMessage(nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern nint FindWindow(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern nint FindWindowEx(nint parent, nint after, string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);
    [DllImport("user32.dll")]
    public static extern nint MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);
    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(nint hWinEventHook);
    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(uint dwProcessId);
    [DllImport("user32.dll")]
    public static extern nint GetAncestor(nint hwnd, uint gaFlags);
    public const uint GA_ROOT = 2, GA_ROOTOWNER = 3;

    // ---- dwmapi --------------------------------------------------------------------------

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    // ---- helpers -------------------------------------------------------------------------

    public static string GetWindowTitle(nint hwnd)
    {
        int len = GetWindowTextLength(hwnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetWindowClass(nint hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool IsCloaked(nint hwnd)
    {
        return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;
    }

    public static long GetStyle(nint hwnd) => (long)GetWindowLongPtr(hwnd, GWL_STYLE);
    public static long GetExStyle(nint hwnd) => (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);

    public static uint GetProcessId(nint hwnd) { GetWindowThreadProcessId(hwnd, out uint pid); return pid; }

    public static RECT GetPrimaryMonitorRect(bool workArea)
    {
        var mon = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        GetMonitorInfo(mon, ref info);
        return workArea ? info.rcWork : info.rcMonitor;
    }

    /// <summary>Visible frame bounds (what the user sees), excluding DWM's invisible resize borders.</summary>
    public static RECT GetVisibleRect(nint hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT ext, Marshal.SizeOf<RECT>()) == 0
            && ext.Width > 0 && ext.Height > 0)
            return ext;
        GetWindowRect(hwnd, out RECT r);
        return r;
    }

    /// <summary>Places the *visible* frame of hwnd exactly on target, compensating for invisible borders.</summary>
    public static void SetVisibleRect(nint hwnd, RECT target, bool activate = false)
    {
        GetWindowRect(hwnd, out RECT win);
        RECT vis = GetVisibleRect(hwnd);
        int dl = vis.Left - win.Left, dt = vis.Top - win.Top, dr = win.Right - vis.Right, db = win.Bottom - vis.Bottom;
        uint flags = SWP_NOZORDER | (activate ? 0u : SWP_NOACTIVATE);
        SetWindowPos(hwnd, 0, target.Left - dl, target.Top - dt,
            target.Width + dl + dr, target.Height + dt + db, flags);
    }

    /// <summary>Un-maximises a window without activating it.</summary>
    public static void RestoreNoActivate(nint hwnd)
    {
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref wp)) return;
        wp.showCmd = SW_SHOWNOACTIVATE;
        SetWindowPlacement(hwnd, ref wp);
    }

    /// <summary>
    /// SetForegroundWindow has strict rules; the classic workaround is to inject a harmless
    /// key event (so our thread "received the last input") then call it, falling back to
    /// AttachThreadInput.
    /// </summary>
    public static void ForceForeground(nint hwnd)
    {
        if (hwnd == 0 || !IsWindow(hwnd)) return;
        if (IsIconic(hwnd)) ShowWindowAsync(hwnd, SW_RESTORE);

        SendKey(VK_MENU, false);
        SendKey(VK_MENU, true);
        if (SetForegroundWindow(hwnd)) return;

        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint me = GetCurrentThreadId();
        if (fgThread != me) AttachThreadInput(me, fgThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (fgThread != me) AttachThreadInput(me, fgThread, false);
        }
    }

    /// <summary>Marker placed in dwExtraInfo of every key event TaLLon injects, so the hook skips them.</summary>
    public const nuint TALLON_INJECT_TAG = 0x7A11;

    public static void SendKey(int vk, bool up, uint scan = 0, bool tagged = true)
    {
        var inp = new INPUT { type = INPUT_KEYBOARD };
        inp.U.ki = new KEYBDINPUT
        {
            wVk = (ushort)vk,
            wScan = (ushort)scan,
            dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0),
            dwExtraInfo = tagged ? TALLON_INJECT_TAG : 0,
        };
        SendInput(1, new[] { inp }, Marshal.SizeOf<INPUT>());
    }

    public static bool IsExtendedKey(int vk) => vk is VK_LWIN or VK_RWIN or VK_RCONTROL or VK_RMENU
        or 0x2D or 0x2E or 0x24 or 0x23 or 0x21 or 0x22 or 0x25 or 0x26 or 0x27 or 0x28 or 0x6F or 0x90;
}
