using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaLLon.Core.Config;

/// <summary>Loads/saves %APPDATA%\TaLLon\config.json and raises <see cref="Changed"/> on external edits.</summary>
public sealed class ConfigStore : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaLLon");
    public static string FilePath { get; } = Path.Combine(Directory, "config.json");
    public static string LogPath { get; } = Path.Combine(Directory, "TaLLon.log");
    public static string SessionPath { get; } = Path.Combine(Directory, "session.json");
    public static string PinnedPath { get; } = Path.Combine(Directory, "pinned-windows.json");

    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounce;
    private DateTime _lastSelfWrite = DateTime.MinValue;

    public TallonConfig Current { get; private set; } = new();

    /// <summary>Raised on a background thread after the file changes on disk (not by <see cref="Save"/>).</summary>
    public event Action<TallonConfig>? Changed;

    public TallonConfig Load()
    {
        System.IO.Directory.CreateDirectory(Directory);
        if (File.Exists(FilePath))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<TallonConfig>(File.ReadAllText(FilePath), JsonOptions) ?? new();
                cfg.Normalize();
                Current = cfg;
                return cfg;
            }
            catch (Exception ex)
            {
                Log.Warn($"config.json unreadable, using defaults: {ex.Message}");
            }
        }
        Current = new TallonConfig();
        Current.Normalize();
        Save(Current);
        return Current;
    }

    public void Save(TallonConfig cfg)
    {
        System.IO.Directory.CreateDirectory(Directory);
        cfg.Normalize();
        _lastSelfWrite = DateTime.UtcNow;
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(cfg, JsonOptions));
        File.Move(tmp, FilePath, overwrite: true);
        Current = cfg;
    }

    public void Watch()
    {
        if (_watcher != null) return;
        _watcher = new FileSystemWatcher(Directory, "config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        FileSystemEventHandler h = (_, _) => Debounced();
        _watcher.Changed += h;
        _watcher.Created += h;
        _watcher.Renamed += (_, _) => Debounced();
    }

    private void Debounced()
    {
        _debounce?.Dispose();
        _debounce = new System.Threading.Timer(_ =>
        {
            if ((DateTime.UtcNow - _lastSelfWrite).TotalMilliseconds < 800) return;
            try
            {
                var cfg = JsonSerializer.Deserialize<TallonConfig>(File.ReadAllText(FilePath), JsonOptions);
                if (cfg == null) return;
                cfg.Normalize();
                Current = cfg;
                Changed?.Invoke(cfg);
            }
            catch (Exception ex) { Log.Warn($"config reload failed: {ex.Message}"); }
        }, null, 300, Timeout.Infinite);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}

/// <summary>Tiny append-only logger (file + Debug output). Enough for a pilot.</summary>
public static class Log
{
    private static readonly object Gate = new();
    public static event Action<string>? Line;
    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg) => Write("ERR ", msg);

    private static void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {level} {msg}";
        System.Diagnostics.Debug.WriteLine(line);
        try { Line?.Invoke(line); } catch { }
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(ConfigStore.Directory);
                File.AppendAllText(ConfigStore.LogPath, line + Environment.NewLine);
            }
        }
        catch { /* logging must never throw */ }
    }
}
