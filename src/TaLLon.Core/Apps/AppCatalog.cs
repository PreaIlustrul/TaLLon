using System.Diagnostics;
using TaLLon.Core.Config;

namespace TaLLon.Core.Apps;

public sealed record AppEntry(string Name, string LaunchTarget, string Source)
{
    public override string ToString() => Name;
}

/// <summary>
/// Everything the Start menu can launch: Start-menu shortcuts (.lnk) plus shell:AppsFolder
/// (which also covers Store/UWP apps). Enumeration is slow-ish (~1 s) so it runs in the background.
/// </summary>
public static class AppCatalog
{
    private static volatile List<AppEntry> _cache = new();
    public static IReadOnlyList<AppEntry> Apps => _cache;
    public static event Action? Refreshed;

    public static Task RefreshAsync() => Task.Run(() =>
    {
        var map = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
        try { foreach (var e in FromAppsFolder()) map.TryAdd(e.Name, e); }
        catch (Exception ex) { Log.Warn("AppsFolder enumeration failed: " + ex.Message); }
        try { foreach (var e in FromStartMenu()) map.TryAdd(e.Name, e); }
        catch (Exception ex) { Log.Warn("Start menu enumeration failed: " + ex.Message); }
        _cache = map.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Log.Info($"app catalog: {_cache.Count} entries");
        Refreshed?.Invoke();
    });

    private static IEnumerable<AppEntry> FromStartMenu()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
        };
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(lnk);
                if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                yield return new AppEntry(name, lnk, "startmenu");
            }
        }
    }

    private static IEnumerable<AppEntry> FromAppsFolder()
    {
        var list = new List<AppEntry>();
        var t = Type.GetTypeFromProgID("Shell.Application");
        if (t == null) return list;
        dynamic shell = Activator.CreateInstance(t)!;
        dynamic folder = shell.NameSpace("shell:AppsFolder");
        if (folder == null) return list;
        dynamic items = folder.Items();
        int count = items.Count;
        for (int i = 0; i < count; i++)
        {
            dynamic item = items.Item(i);
            string name = item.Name;
            string path = item.Path;   // AUMID or exe path
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path)) continue;
            list.Add(new AppEntry(name, "shell:AppsFolder\\" + path, "appsfolder"));
        }
        return list;
    }

    public static void Launch(AppEntry app)
    {
        try
        {
            if (app.LaunchTarget.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                Process.Start(new ProcessStartInfo("explorer.exe", app.LaunchTarget) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(app.LaunchTarget) { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Warn($"launch {app.Name} failed: {ex.Message}"); }
    }

    /// <summary>Launch an arbitrary command/URL/file as the shell would.</summary>
    public static void LaunchCommand(string command, string args = "")
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        try
        {
            if (command.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                Process.Start(new ProcessStartInfo("explorer.exe", command) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(Environment.ExpandEnvironmentVariables(command), args ?? "") { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Warn($"launch '{command}' failed: {ex.Message}"); }
    }

    public static void LaunchTerminal(TallonConfig cfg)
    {
        var cmd = string.IsNullOrWhiteSpace(cfg.TerminalCommand) ? "wt.exe" : cfg.TerminalCommand;
        try
        {
            Process.Start(new ProcessStartInfo(cmd, cfg.TerminalArgs ?? "") { UseShellExecute = true });
        }
        catch
        {
            // Windows Terminal not installed: fall back to PowerShell.
            Process.Start(new ProcessStartInfo("powershell.exe") { UseShellExecute = true });
        }
    }

    /// <summary>Simple ranking: prefix match beats word-start match beats substring.</summary>
    public static IEnumerable<AppEntry> Search(string query, int max = 40)
    {
        if (string.IsNullOrWhiteSpace(query)) return _cache.Take(max);
        var q = query.Trim();
        return _cache
            .Select(a => (a, score: Score(a.Name, q)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score).ThenBy(x => x.a.Name.Length)
            .Take(max).Select(x => x.a);
    }

    public static int Score(string name, string q)
    {
        if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 3;
        foreach (var word in name.Split(' ', '-', '_', '.'))
            if (word.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 2;
        return name.Contains(q, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }
}
