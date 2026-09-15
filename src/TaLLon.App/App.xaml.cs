using System.Windows;
using System.Windows.Threading;
using TaLLon.App.Windows;
using TaLLon.Core.Apps;
using TaLLon.Core.Config;
using TaLLon.Core.Input;
using TaLLon.Core.Session;
using TaLLon.Core.Windows;

namespace TaLLon.App;

public partial class App : Application
{
    private static Mutex? _mutex;

    public ConfigStore Store { get; } = new();
    public TallonConfig Config => Store.Current;
    public SpecialKeyEngine Engine { get; } = new();
    public WinEventWatcher Events { get; } = new();
    public WindowManager Wm { get; private set; } = null!;
    public ActionRouter Router { get; private set; } = null!;

    public CanvasWindow? Canvas { get; private set; }
    public MenuWindow? Menu { get; private set; }
    public OsdWindow? Osd { get; private set; }
    public SettingsWindow? Settings { get; private set; }
    private TrayIcon? _tray;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("Unhandled: " + ex.Exception);
            Wm?.EmergencyRestore();
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => { Log.Error("Fatal: " + ex.ExceptionObject); Wm?.EmergencyRestore(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Wm?.EmergencyRestore();

        Store.Load();
        bool settingsOnly = e.Args.Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase));

        // `TaLLon.exe --send Special+W` : type a chord into the running daemon (scripting / testing).
        int si = Array.FindIndex(e.Args, a => a.Equals("--send", StringComparison.OrdinalIgnoreCase));
        if (si >= 0)
        {
            if (si + 1 < e.Args.Length && KeyChord.TryParse(e.Args[si + 1], out var chord))
                SpecialKeyEngine.InjectChord(chord, Config.SpecialKey);
            else
                Log.Warn("--send: missing or invalid chord");
            Shutdown();
            return;
        }

        if (settingsOnly)
        {
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            OpenSettings();
            return;
        }

        _mutex = new Mutex(true, "Global\\TaLLon.Daemon", out bool createdNew);
        if (!createdNew)
        {
            // Another daemon is running: just open its settings and exit.
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            OpenSettings();
            return;
        }

        Log.Info("=== TaLLon daemon starting ===");
        Wm = new WindowManager(Events, Config)
        {
            Delay = (ms, a) =>
            {
                var t = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(ms) };
                t.Tick += (_, _) => { t.Stop(); a(); };
                t.Start();
            },
        };
        Canvas = new CanvasWindow();
        Menu = new MenuWindow();
        Osd = new OsdWindow();
        WindowQuery.IsOwnWindow = h => Canvas.IsHandle(h) || Menu.IsHandle(h) || Osd.IsHandle(h);

        Router = new ActionRouter(this);
        Router.Rebuild(Config);

        Wm.Entered += () => Canvas.ShowCanvas(Config);
        Wm.Exited += () => { Canvas.HideCanvas(); Menu.Hide(); };
        Wm.Notify += s => Osd.Flash(s);

        Engine.Apply(Config);
        Engine.ChordPressed += chord => Dispatcher.BeginInvoke(() => Router.Handle(chord));
        Engine.StateChanged += st => Dispatcher.BeginInvoke(() => Osd.ShowState(st, Config));
        Events.Start();
        Engine.Start();

        Store.Changed += cfg => Dispatcher.BeginInvoke(() => ApplyConfig(cfg));
        Store.Watch();
        _ = AppCatalog.RefreshAsync();

        _tray = new TrayIcon(this);
        Log.Info("daemon ready; special key = " + (Config.SpecialKey.Kind == SpecialKeyKind.Copilot ? "Copilot" : Config.SpecialKey.Key));
    }

    public void ApplyConfig(TallonConfig cfg)
    {
        Engine.Apply(cfg);
        Router.Rebuild(cfg);
        Wm.ApplyConfig(cfg);
        if (Wm.Active) Canvas?.ShowCanvas(cfg);
        Log.Info("config applied");
    }

    public void OpenSettings()
    {
        if (Settings == null || !Settings.IsLoaded)
        {
            Settings = new SettingsWindow(Store);
            Settings.Closed += (_, _) => Settings = null;
        }
        Settings.Show();
        Settings.Activate();
    }

    public void Quit()
    {
        Log.Info("quit");
        try { Wm?.EmergencyRestore(); } catch { }
        _tray?.Dispose();
        Engine.Dispose();
        Events.Dispose();
        Store.Dispose();
        Shutdown();
    }
}
