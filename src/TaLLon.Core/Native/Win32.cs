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
        public int CenterX => (Left + Right) / 2;
        public int CenterY => (Top + Bottom) / 2;
        public RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
        public static RECT FromSize(int x, int y, int w, int h) => new(x, y, x + w, y + h);
        public RECT Offset(int dx, int dy) => new(Left + dx, Top + dy, Right + dx, Bottom + dy);
        public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
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
    public const int WPF_RESTORETOMAXIMIZED = 2;

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
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd; public uint message; public nuint wParam; public nint lParam; public uint time; public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    // ---- constants -----------------------------------------------------------------------

    public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    public const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    public const int WM_CLOSE = 0x0010, WM_QUIT = 0x0012, WM_APP = 0x8000, WM_NCHITTEST = 0x0084;
    public const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205, WM_MOUSEWHEEL = 0x020A, WM_MOUSEHWHEEL = 0x020E;
    public const int HTCAPTION = 2, HTCLIENT = 1;
    public const uint LLKHF_EXTENDED = 0x01, LLKHF_INJECTED = 0x10, LLKHF_ALTDOWN = 0x20, LLKHF_UP = 0x80;
    public const uint LLMHF_INJECTED = 0x01;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_SCANCODE = 0x0008;

    public const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_TAB = 0x09, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12,
        VK_ESCAPE = 0x1B, VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1,
        VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5, VK_F23 = 0x86, VK_D = 0x44;

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
        SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040, SWP_HIDEWINDOW = 0x0080, SWP_NOOWNERZORDER = 0x0200,
        SWP_ASYNCWINDOWPOS = 0x4000, SWP_NOSENDCHANGING = 0x0400;
    public static readonly nint HWND_TOP = 0, HWND_BOTTOM = 1, HWND_TOPMOST = -1, HWND_NOTOPMOST = -2;

    public const uint GW_HWNDFIRST = 0, GW_HWNDLAST = 1, GW_HWNDNEXT = 2, GW_HWNDPREV = 3, GW_OWNER = 4;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1, MONITOR_DEFAULTTONEAREST = 2;

    public const int DWMWA_CLOAKED = 14, DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    public const uint SPI_GETWORKAREA = 0x0030, SPI_SETWORKAREA = 0x002F, SPIF_SENDCHANGE = 0x0002;

    // WinEvents
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_SYSTEM_MOVESIZESTART = 0x000A, EVENT_SYSTEM_MOVESIZEEND = 0x000B,
        EVENT_SYSTEM_MINIMIZESTART = 0x0016, EVENT_SYSTEM_MINIMIZEEND = 0x0017,
        EVENT_OBJECT_CREATE = 0x8000, EVENT_OBJECT_DESTROY = 0x8001, EVENT_OBJECT_SHOW = 0x8002,
        EVENT_OBJECT_HIDE = 0x8003, EVENT_OBJECT_LOCATIONCHANGE = 0x800B, EVENT_OBJECT_NAMECHANGE = 0x800C,
        EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000, WINEVENT_SKIPOWNPROCESS = 0x0002;
    public const int OBJID_WINDOW = 0;

    public const uint PROCESS_SUSPEND_RESUME = 0x0800, PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, SYNCHRONIZE = 0x00100000;

    // ---- delegates -----------------------------------------------------------------------

    public delegate nint HookProc(int nCode, nint wParam, nint lParam);
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);
    public delegate void WinEventDelegate(nint hWinEventHook, uint eventType, nint hwnd, int idObject,
        int idChild, uint dwEventThread, uint dwmsEventTime);

    // ---- user32 --------------------------------------------------------------------------

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(nint hhk);
    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern bool GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")]
    public static extern nint DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")]
    public static extern bool PostThreadMessage(uint idThread, uint Msg, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")]
    public static extern nint WindowFromPoint(POINT p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern nint SendMessageTimeout(nint hWnd, uint Msg, nuint wParam, nint lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

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
    public static extern nint BeginDeferWindowPos(int nNumWindows);
    [DllImport("user32.dll")]
    public static extern nint DeferWindowPos(nint hWinPosInfo, nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")]
    public static extern bool EndDeferWindowPos(nint hWinPosInfo);
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
    public static extern nint GetAncestor(nint hwnd, uint gaFlags);
    public const uint GA_ROOT = 2, GA_ROOTOWNER = 3;
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    // ---- kernel32 / ntdll ----------------------------------------------------------------

    [DllImport("kernel32.dll")]
    public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(nint hObject);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageName(nint hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);
    [DllImport("ntdll.dll")]
    public static extern int NtSuspendProcess(nint hProcess);
    [DllImport("ntdll.dll")]
    public static extern int NtResumeProcess(nint hProcess);

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

    public static string GetProcessPath(uint pid)
    {
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == 0) return "";
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : "";
        }
        finally { CloseHandle(h); }
    }

    public static bool SuspendProcess(uint pid)
    {
        var h = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
        if (h == 0) return false;
        try { return NtSuspendProcess(h) == 0; } finally { CloseHandle(h); }
    }

    public static bool ResumeProcess(uint pid)
    {
        var h = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
        if (h == 0) return false;
        try { return NtResumeProcess(h) == 0; } finally { CloseHandle(h); }
    }

    public static RECT GetPrimaryMonitorRect(bool workArea)
    {
        var mon = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        GetMonitorInfo(mon, ref info);
        return workArea ? info.rcWork : info.rcMonitor;
    }

    public static RECT GetWorkArea()
    {
        var r = new RECT();
        SystemParametersInfo(SPI_GETWORKAREA, 0, ref r, 0);
        return r;
    }

    public static bool SetWorkArea(RECT r) => SystemParametersInfo(SPI_SETWORKAREA, 0, ref r, SPIF_SENDCHANGE);

    /// <summary>Visible frame bounds (what the user sees), excluding DWM's invisible resize borders.</summary>
    public static RECT GetVisibleRect(nint hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT ext, Marshal.SizeOf<RECT>()) == 0
            && ext.Width > 0 && ext.Height > 0)
            return ext;
        GetWindowRect(hwnd, out RECT r);
        return r;
    }

    /// <summary>Offsets between GetWindowRect and the visible frame (left, top, right, bottom), usually ~7px on resizable windows.</summary>
    public static (int l, int t, int r, int b) FrameDeltas(nint hwnd)
    {
        GetWindowRect(hwnd, out RECT win);
        RECT vis = GetVisibleRect(hwnd);
        return (vis.Left - win.Left, vis.Top - win.Top, win.Right - vis.Right, win.Bottom - vis.Bottom);
    }

    /// <summary>Places the *visible* frame of hwnd exactly on target, compensating for invisible borders.</summary>
    public static void SetVisibleRect(nint hwnd, RECT target, bool activate = false, nint insertAfter = 0, bool keepZ = true)
    {
        var (dl, dt, dr, db) = FrameDeltas(hwnd);
        uint flags = (keepZ ? SWP_NOZORDER : 0u) | (activate ? 0u : SWP_NOACTIVATE);
        SetWindowPos(hwnd, insertAfter, target.Left - dl, target.Top - dt,
            target.Width + dl + dr, target.Height + dt + db, flags);
    }

    /// <summary>Batched version of <see cref="SetVisibleRect"/> for many windows at once (one repaint pass).</summary>
    public static void SetVisibleRects(IReadOnlyList<(nint hwnd, RECT target)> items)
    {
        if (items.Count == 0) return;
        var hdwp = BeginDeferWindowPos(items.Count);
        foreach (var (hwnd, target) in items)
        {
            if (!IsWindow(hwnd)) continue;
            var (dl, dt, dr, db) = FrameDeltas(hwnd);
            hdwp = DeferWindowPos(hdwp, hwnd, 0, target.Left - dl, target.Top - dt,
                target.Width + dl + dr, target.Height + dt + db, SWP_NOZORDER | SWP_NOACTIVATE);
            if (hdwp == 0) break;
        }
        if (hdwp != 0) EndDeferWindowPos(hdwp);
    }

    /// <summary>Un-maximises / un-minimises a window to its normal placement without activating it.</summary>
    public static void ShowNormalNoActivate(nint hwnd)
    {
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref wp)) return;
        wp.flags &= ~WPF_RESTORETOMAXIMIZED;
        wp.showCmd = SW_SHOWNOACTIVATE;
        SetWindowPlacement(hwnd, ref wp);
    }

    public static WINDOWPLACEMENT GetPlacement(nint hwnd)
    {
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        GetWindowPlacement(hwnd, ref wp);
        return wp;
    }

    public static bool IsRestoreToMaximized(nint hwnd) => (GetPlacement(hwnd).flags & WPF_RESTORETOMAXIMIZED) != 0;

    /// <summary>
    /// SetForegroundWindow has strict rules; the classic workaround is to inject a harmless
    /// key event (so our thread "received the last input") then call it, falling back to
    /// AttachThreadInput. We use Ctrl rather than Alt: a lone Alt press activates menu bars.
    /// </summary>
    public static void ForceForeground(nint hwnd)
    {
        if (hwnd == 0 || !IsWindow(hwnd)) return;
        if (IsIconic(hwnd)) ShowWindowAsync(hwnd, SW_RESTORE);

        SendKey(VK_CONTROL, false);
        SendKey(VK_CONTROL, true);
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

    /// <summary>Non-client hit test of a screen point against a window (HTCAPTION etc.), with a timeout so a hung app can't stall us.</summary>
    public static int HitTest(nint hwnd, int x, int y)
    {
        nint lparam = (nint)((y << 16) | (x & 0xFFFF));
        if (SendMessageTimeout(hwnd, WM_NCHITTEST, 0, lparam, 0x0002 /*SMTO_ABORTIFHUNG*/, 60, out var result) == 0) return -1;
        return (int)result;
    }

    /// <summary>Top-level windows in z-order, topmost first.</summary>
    public static IEnumerable<nint> ZOrder()
    {
        var h = GetWindow(FindWindow("Progman", null), GW_HWNDFIRST);
        if (h == 0) h = GetWindow(GetForegroundWindow(), GW_HWNDFIRST);
        while (h != 0) { yield return h; h = GetWindow(h, GW_HWNDNEXT); }
    }
}
