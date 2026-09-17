using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.Core.Session;

public sealed class SavedRect
{
    public int L { get; set; } public int T { get; set; } public int R { get; set; } public int B { get; set; }
    public static SavedRect From(Win32.RECT r) => new() { L = r.Left, T = r.Top, R = r.Right, B = r.Bottom };
    public Win32.RECT ToRect() => new(L, T, R, B);
}

public sealed class SavedWindow
{
    public long Hwnd { get; set; }
    public string Title { get; set; } = "";
    public int ShowCmd { get; set; }
    public int Flags { get; set; }
    public SavedRect Normal { get; set; } = new();
}

/// <summary>
/// Everything needed to put the desktop back if TaLLon dies while the environment is active.
/// Written to session.json at Enter, deleted at Exit; the watchdog and the next start read it.
/// </summary>
public sealed class SessionState
{
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public int OwnerPid { get; set; }
    public SavedRect WorkArea { get; set; } = new();
    public bool TaskbarHidden { get; set; }
    public List<SavedWindow> Windows { get; set; } = new();
    public List<uint> SuspendedPids { get; set; } = new();

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.Directory);
            File.WriteAllText(ConfigStore.SessionPath, JsonSerializer.Serialize(this, ConfigStore.JsonOptions));
        }
        catch (Exception ex) { Log.Warn("session.json write failed: " + ex.Message); }
    }

    public static SessionState? Load()
    {
        try
        {
            if (!File.Exists(ConfigStore.SessionPath)) return null;
            return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(ConfigStore.SessionPath), ConfigStore.JsonOptions);
        }
        catch { return null; }
    }

    public static void Delete()
    {
        try { File.Delete(ConfigStore.SessionPath); } catch { }
    }
}

/// <summary>A window the user pinned in infinite mode; restored to the same grid position next session.</summary>
public sealed class PinnedWindowRecord
{
    public string ExePath { get; set; } = "";
    public string Title { get; set; } = "";
    public SavedRect World { get; set; } = new();
}

public static class PinnedStore
{
    public static List<PinnedWindowRecord> Load()
    {
        try
        {
            if (!File.Exists(ConfigStore.PinnedPath)) return new();
            return JsonSerializer.Deserialize<List<PinnedWindowRecord>>(File.ReadAllText(ConfigStore.PinnedPath), ConfigStore.JsonOptions) ?? new();
        }
        catch { return new(); }
    }

    public static void Save(List<PinnedWindowRecord> list)
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.Directory);
            File.WriteAllText(ConfigStore.PinnedPath, JsonSerializer.Serialize(list, ConfigStore.JsonOptions));
        }
        catch (Exception ex) { Log.Warn("pinned-windows.json write failed: " + ex.Message); }
    }
}

/// <summary>Puts the desktop back from session.json. Safe to run any time; no-op if there is nothing to restore.</summary>
public static class CrashRestore
{
    public static bool Run(string reason)
    {
        var s = SessionState.Load();
        // Always make sure the taskbar is visible, even without a session file.
        ShowTaskbars();
        if (s == null) return false;
        Log.Warn($"crash restore ({reason}): session from {s.StartedAt:HH:mm:ss}, {s.Windows.Count} windows");
        try
        {
            foreach (var pid in s.SuspendedPids) Win32.ResumeProcess(pid);
            if (s.WorkArea.R > s.WorkArea.L) Win32.SetWorkArea(s.WorkArea.ToRect());
            foreach (var w in s.Windows)
            {
                var h = (nint)w.Hwnd;
                if (!Win32.IsWindow(h)) continue;
                var wp = new Win32.WINDOWPLACEMENT
                {
                    length = Marshal.SizeOf<Win32.WINDOWPLACEMENT>(),
                    flags = w.Flags,
                    showCmd = w.ShowCmd == Win32.SW_SHOWMINIMIZED ? Win32.SW_SHOWMINNOACTIVE : (w.ShowCmd == Win32.SW_SHOWMAXIMIZED ? Win32.SW_SHOWMAXIMIZED : Win32.SW_SHOWNOACTIVATE),
                    rcNormalPosition = w.Normal.ToRect(),
                };
                Win32.SetWindowPlacement(h, ref wp);
            }
        }
        catch (Exception ex) { Log.Error("crash restore failed: " + ex); }
        finally { SessionState.Delete(); ShowTaskbars(); }
        return true;
    }

    public static void ShowTaskbars()
    {
        var main = Win32.FindWindow("Shell_TrayWnd", null);
        if (main != 0 && !Win32.IsWindowVisible(main)) Win32.ShowWindow(main, Win32.SW_SHOW);
        nint sec = 0;
        while ((sec = Win32.FindWindowEx(0, sec, "Shell_SecondaryTrayWnd", null)) != 0)
            if (!Win32.IsWindowVisible(sec)) Win32.ShowWindow(sec, Win32.SW_SHOW);
    }
}

/// <summary>
/// A second copy of TaLLon.exe (`--watchdog &lt;pid&gt;`) that waits for the main process to die and, if
/// the environment was still active (session.json exists), restores the desktop.
/// </summary>
public static class Watchdog
{
    private static Process? _proc;

    public static void Spawn()
    {
        if (_proc is { HasExited: false }) return;
        try
        {
            string exe = global::System.Environment.ProcessPath!;
            int pid = global::System.Environment.ProcessId;
            _proc = Process.Start(new ProcessStartInfo(exe, "--watchdog " + pid)
            {
                UseShellExecute = false, CreateNoWindow = true,
            });
            Log.Info($"watchdog pid {_proc?.Id}");
        }
        catch (Exception ex) { Log.Warn("watchdog spawn failed: " + ex.Message); }
    }

    public static void Stop()
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(); } catch { }
        _proc = null;
    }

    /// <summary>Entry point for the `--watchdog` process.</summary>
    public static void RunAsWatchdog(int ownerPid)
    {
        try
        {
            using var owner = Process.GetProcessById(ownerPid);
            owner.WaitForExit();
        }
        catch { /* owner already gone */ }
        Thread.Sleep(300);
        CrashRestore.Run("owner process exited");
    }
}
