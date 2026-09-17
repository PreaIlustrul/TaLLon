using System.IO;
using Microsoft.Win32;
using TaLLon.Core.Config;

namespace TaLLon.App;

/// <summary>Run-at-login registration and a Start-menu entry so TaLLon is always startable from somewhere.</summary>
public static class StartupSetup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "TaLLon";

    public static string ExePath => Environment.ProcessPath ?? "";

    public static bool IsStartWithWindows()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(RunValue) != null; }
        catch { return false; }
    }

    /// <summary>Keeps the Run entry in sync with the setting (and with where the exe currently lives).</summary>
    public static void EnsureStartWithWindows(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (k == null) return;
            if (on)
            {
                var want = $"\"{ExePath}\" --tray";
                if ((k.GetValue(RunValue) as string) != want) { k.SetValue(RunValue, want); Log.Info("run-at-login: " + want); }
            }
            else if (k.GetValue(RunValue) != null) { k.DeleteValue(RunValue, false); Log.Info("run-at-login removed"); }
        }
        catch (Exception ex) { Log.Warn("run-at-login failed: " + ex.Message); }
    }

    public static string StartMenuShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\TaLLon.lnk");

    /// <summary>Creates the Start-menu entry, or repairs it if the exe has moved since it was made.</summary>
    public static void EnsureStartMenuShortcut()
    {
        try
        {
            if (File.Exists(StartMenuShortcutPath) && ShortcutTarget(StartMenuShortcutPath).Equals(ExePath, StringComparison.OrdinalIgnoreCase)) return;
            CreateShortcut(StartMenuShortcutPath, "", "TaLLon");
            Log.Info("start menu shortcut written -> " + ExePath);
        }
        catch (Exception ex) { Log.Warn("start menu shortcut failed: " + ex.Message); }
    }

    public static string ShortcutTarget(string lnkPath)
    {
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return "";
            dynamic shell = Activator.CreateInstance(t)!;
            dynamic sc = shell.CreateShortcut(lnkPath);
            return (string)sc.TargetPath;
        }
        catch { return ""; }
    }

    public static void CreateShortcut(string lnkPath, string args, string description)
    {
        var t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) return;
        dynamic shell = Activator.CreateInstance(t)!;
        dynamic sc = shell.CreateShortcut(lnkPath);
        sc.TargetPath = ExePath;
        sc.Arguments = args;
        sc.WorkingDirectory = Path.GetDirectoryName(ExePath);
        sc.Description = description;
        sc.IconLocation = ExePath + ",0";
        sc.Save();
    }
}
