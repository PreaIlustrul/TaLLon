using TaLLon.Core.Native;

namespace TaLLon.Core.Windows;

public sealed record WindowInfo(nint Hwnd, string Title, string Class, uint Pid, Win32.RECT Rect, bool Minimized, bool Maximized)
{
    public override string ToString() => $"0x{Hwnd:X} [{Class}] \"{Title}\" {Rect}{(Minimized ? " (min)" : "")}{(Maximized ? " (max)" : "")}";
}

/// <summary>Enumerates the top-level windows a window manager should care about.</summary>
public static class WindowQuery
{
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow", "Windows.Internal.Shell.TabProxyWindow", "ForegroundStaging",
        "MultitaskingViewFrame", "TaskListThumbnailWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
        "Shell_InputSwitchTopLevelWindow", "SysShadow", "tooltips_class32", "#32770",
    };

    /// <summary>Windows that belong to TaLLon itself and must never be managed.</summary>
    public static Func<nint, bool>? IsOwnWindow { get; set; }

    public static bool IsManageable(nint hwnd)
    {
        if (hwnd == 0 || !Win32.IsWindow(hwnd) || !Win32.IsWindowVisible(hwnd)) return false;
        if (IsOwnWindow?.Invoke(hwnd) == true) return false;
        if (Win32.GetProcessId(hwnd) == (uint)Environment.ProcessId) return false;

        long style = Win32.GetStyle(hwnd);
        long ex = Win32.GetExStyle(hwnd);
        if ((style & Win32.WS_CHILD) != 0) return false;
        if ((ex & Win32.WS_EX_TOOLWINDOW) != 0 && (ex & Win32.WS_EX_APPWINDOW) == 0) return false;
        if ((ex & Win32.WS_EX_NOACTIVATE) != 0) return false;
        if (Win32.GetWindow(hwnd, Win32.GW_OWNER) != 0 && (ex & Win32.WS_EX_APPWINDOW) == 0) return false;
        if (Win32.IsCloaked(hwnd)) return false;

        var cls = Win32.GetWindowClass(hwnd);
        if (IgnoredClasses.Contains(cls)) return false;
        if (string.IsNullOrWhiteSpace(Win32.GetWindowTitle(hwnd))) return false;

        Win32.GetWindowRect(hwnd, out var r);
        if (!Win32.IsIconic(hwnd) && (r.Width < 40 || r.Height < 40)) return false;
        return true;
    }

    public static WindowInfo Describe(nint hwnd)
    {
        var rect = Win32.GetVisibleRect(hwnd);
        return new WindowInfo(hwnd, Win32.GetWindowTitle(hwnd), Win32.GetWindowClass(hwnd),
            Win32.GetProcessId(hwnd), rect, Win32.IsIconic(hwnd), Win32.IsZoomed(hwnd));
    }

    /// <summary>All manageable windows in z-order (topmost first).</summary>
    public static List<WindowInfo> All()
    {
        var list = new List<WindowInfo>();
        Win32.EnumWindows((h, _) =>
        {
            if (IsManageable(h)) list.Add(Describe(h));
            return true;
        }, 0);
        return list;
    }
}
