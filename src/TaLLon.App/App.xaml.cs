using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TaLLon.App.Windows;
using TaLLon.Core.Apps;
using TaLLon.Core.Config;
using TaLLon.Core.Input;
using TaLLon.Core.Session;
using TaLLon.Core.Windows;
using Env = TaLLon.Core.Session.Environment;

namespace TaLLon.App;

public partial class App : Application
{
    private static Mutex? _mutex;
    private const string ShowEventName = "Local\\TaLLon.ShowMainWindow";

    public ConfigStore Store { get; } = new();
    public TallonConfig Config => Store.Current;
    public SpecialKeyEngine Engine { get; } = new();
    public WinEventWatcher Events { get; } = new();
    public Env Env { get; private set; } = null!;
    public ActionRouter Router { get; private set; } = null!;
    public IconCache Icons { get; } = new();

    public CanvasWindow Canvas { get; private set; } = null!;
    public TopBarWindow TopBar { get; private set; } = null!;
    public FocusBorderWindow FocusBorder { get; private set; } = null!;
    public FocusSinkWindow FocusSink { get; private set; } = null!;
    public MenuWindow Menu { get; private set; } = null!;
    public OsdWindow Osd { get; private set; } = null!;
    public WindowContextMenu ContextMenu { get; private set; } = null!;
    public MainWindow? MainWin { get; private set; }
    public TrayIcon? Tray { get; private set; }
    private DispatcherTimer? _edgePan, _rehook;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Store.Load();

        // ---- helper modes (no UI) --------------------------------------------------------
        int wi = Array.FindIndex(e.Args, a => a.Equals("--watchdog", StringComparison.OrdinalIgnoreCase));
        if (wi >= 0 && wi + 1 < e.Args.Length && int.TryParse(e.Args[wi + 1], out int ownerPid))
        {
            Watchdog.RunAsWatchdog(ownerPid);
            Shutdown();
            return;
        }
        if (e.Args.Any(a => a.Equals("--restore", StringComparison.OrdinalIgnoreCase)))
        {
            CrashRestore.Run("manual --restore");
            Shutdown();
            return;
        }
        int si = Array.FindIndex(e.Args, a => a.Equals("--send", StringComparison.OrdinalIgnoreCase));
        if (si >= 0)
        {
            if (si + 1 < e.Args.Length)
            {
                var text = e.Args[si + 1];
                if (text.Equals("Special", StringComparison.OrdinalIgnoreCase))
                    SpecialKeyEngine.InjectChord(new KeyChord(Mods.Special, 0), Config.SpecialKey);
                else if (KeyChord.TryParse(text, out var chord))
                    SpecialKeyEngine.InjectChord(chord, Config.SpecialKey);
                else Log.Warn("--send: invalid chord " + text);
            }
            Shutdown();
            return;
        }

        // ---- single instance --------------------------------------------------------------
        _mutex = new Mutex(true, "Local\\TaLLon.App", out bool createdNew);
        if (!createdNew)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("Unhandled: " + ex.Exception);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => { Log.Error("Fatal: " + ex.ExceptionObject); Env?.EmergencyRestore(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Env?.EmergencyRestore();
        SessionEnding += (_, _) => Env?.EmergencyRestore();

        Log.Info("=== TaLLon starting ===");
        if (CrashRestore.Run("startup")) Log.Warn("restored a desktop left behind by a previous crash");
        StartupSetup.EnsureStartWithWindows(Config.StartWithWindows);
        StartupSetup.EnsureStartMenuShortcut();

        // ---- windows ----------------------------------------------------------------------
        FocusSink = new FocusSinkWindow();
        Canvas = new CanvasWindow();
        TopBar = new TopBarWindow();
        FocusBorder = new FocusBorderWindow();
        Osd = new OsdWindow();
        Menu = new MenuWindow();
        ContextMenu = new WindowContextMenu();
        foreach (var w in new Window[] { FocusSink, Canvas, TopBar, FocusBorder, Osd, Menu, ContextMenu })
            new WindowInteropHelper(w).EnsureHandle();
        WindowQuery.IsOwnWindow = h => Canvas.IsHandle(h) || TopBar.IsHandle(h) || FocusBorder.IsHandle(h)
                                       || FocusSink.IsHandle(h) || Osd.IsHandle(h) || Menu.IsHandle(h) || ContextMenu.IsHandle(h);

        Env = new Env(Events, Config)
        {
            Delay = (ms, a) =>
            {
                var t = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(ms) };
                t.Tick += (_, _) => { t.Stop(); a(); };
                t.Start();
            },
            FocusSink = FocusSink.Handle,
            TopBarHeight = Config.Appearance.ShowTopBar ? TopBar.PhysicalHeight : 0,
        };
        Router = new ActionRouter(this);
        Router.Rebuild(Config);

        Env.Entered += OnEntered;
        Env.Exited += OnExited;
        Env.Notify += s => Osd.Flash(s);
        Env.FocusChanged += h => FocusBorder.Track(h);
        Env.FocusRectChanged += () => FocusBorder.Refresh();
        Env.ViewportChanged += () => TopBar.UpdateCoordinates();
        Env.Changed += () => { TopBar.UpdateState(); MainWin?.UpdateEnvironmentState(); };
        Env.DragStateChanged += dragging => FocusBorder.SetDragging(dragging);

        Engine.Apply(Config);
        Engine.IsManagedWindow = h => Env.Find(h) != null;
        Engine.ChordPressed += chord => Dispatcher.BeginInvoke(() => Router.Handle(chord));
        Engine.SpecialTapped += () => Dispatcher.BeginInvoke(() => { if (Config.TapTogglesEnvironment) Env.Toggle(); });
        Engine.SystemShortcutPressed += s => Dispatcher.BeginInvoke(() => OnSystemShortcut(s));
        Engine.CaptionRightClick += (h, x, y) => Dispatcher.BeginInvoke(() => ContextMenu.ShowFor(h, x, y));
        Events.Start();
        Engine.Start();

        SystemEvents.PowerModeChanged += (_, pm) => { if (pm.Mode == PowerModes.Resume) { Log.Info("resume: rehook"); Engine.Reinstall(); } };
        SystemEvents.SessionSwitch += (_, _) => Engine.Reinstall();
        _rehook = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _rehook.Tick += (_, _) => Engine.Reinstall();
        _rehook.Start();

        _edgePan = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _edgePan.Tick += (_, _) => EdgePanTick();

        Store.Changed += cfg => Dispatcher.BeginInvoke(() => ApplyConfig(cfg));
        Store.Watch();
        _ = AppCatalog.RefreshAsync();

        Tray = new TrayIcon(this);
        StartShowListener();

        if (!e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase))) ShowMain();
        Log.Info("ready; special key = " + (Config.SpecialKey.Kind == SpecialKeyKind.Copilot ? "Copilot" : Config.SpecialKey.Key));
    }

    private void StartShowListener()
    {
        var ev = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var t = new Thread(() =>
        {
            while (true)
            {
                ev.WaitOne();
                Dispatcher.BeginInvoke(() => ShowMain());
            }
        }) { IsBackground = true, Name = "TaLLon.ShowListener" };
        t.Start();
    }

    // ---- environment glue ------------------------------------------------------------------

    private void OnEntered()
    {
        Engine.EnvironmentActive = true;
        Engine.SetMouseHook(true);
        Canvas.ShowCanvas(Config);
        if (Config.Appearance.ShowTopBar) TopBar.ShowBar(Config);
        FocusBorder.Apply(Config);
        _edgePan?.Start();
        MainWin?.Hide();
        MainWin?.UpdateEnvironmentState();
    }

    private void OnExited()
    {
        Engine.EnvironmentActive = false;
        Engine.SetMouseHook(false);
        _edgePan?.Stop();
        FocusBorder.Track(0);
        TopBar.HideBar();
        Canvas.HideCanvas();
        Menu.Hide();
        ContextMenu.Hide();
        MainWin?.UpdateEnvironmentState();
    }

    private void OnSystemShortcut(SystemShortcut s)
    {
        if (!Env.Active) return;
        switch (s)
        {
            case SystemShortcut.AltTab:
            case SystemShortcut.WinTab:
                Env.ToggleOverview();
                break;
            case SystemShortcut.WinTap:
                Menu.Open();
                break;
            case SystemShortcut.WinD:
                Env.Unfocus();
                break;
        }
    }

    private void EdgePanTick()
    {
        if (!Env.Active || Env.Mode != EnvironmentMode.Tiling && Env.OverviewActive) return;
        if (Env.Mode != EnvironmentMode.Infinite || Menu.IsVisible || Config.Infinite.EdgePanMargin <= 0) return;
        if (!Core.Native.Win32.GetCursorPos(out var p)) return;
        var mon = Env.Monitor;
        int margin = Config.Infinite.EdgePanMargin, speed = Config.Infinite.EdgePanSpeed;
        int dx = 0, dy = 0;
        if (p.X <= mon.Left + margin - 1) dx = -speed;
        else if (p.X >= mon.Right - margin) dx = speed;
        if (p.Y >= mon.Bottom - margin) dy = speed;
        else if (p.Y <= mon.Top + margin - 1 && !(TopBar.IsVisible && TopBar.IsInteractiveAt(p.X))) dy = -speed;
        if (dx != 0 || dy != 0) Env.Pan(dx, dy);
    }

    public void ApplyConfig(TallonConfig cfg)
    {
        Engine.Apply(cfg);
        Router.Rebuild(cfg);
        Env.TopBarHeight = cfg.Appearance.ShowTopBar ? TopBar.PhysicalHeight : 0;
        Env.ApplyConfig(cfg);
        StartupSetup.EnsureStartWithWindows(cfg.StartWithWindows);
        if (Env.Active)
        {
            Canvas.ShowCanvas(cfg);
            if (cfg.Appearance.ShowTopBar) TopBar.ShowBar(cfg); else TopBar.HideBar();
            FocusBorder.Apply(cfg);
        }
        Log.Info("config applied");
    }

    public void ShowMain(string? page = null)
    {
        if (MainWin == null)
        {
            MainWin = new MainWindow(Store);
            MainWin.Closed += (_, _) => MainWin = null;
        }
        if (page != null) MainWin.SelectPage(page);
        MainWin.Show();
        if (MainWin.WindowState == WindowState.Minimized) MainWin.WindowState = WindowState.Normal;
        MainWin.Activate();
        Core.Native.Win32.ForceForeground(new WindowInteropHelper(MainWin).Handle);
    }

    public void Quit()
    {
        Log.Info("quit");
        try { Env?.EmergencyRestore(); } catch { }
        _edgePan?.Stop();
        _rehook?.Stop();
        Tray?.Dispose();
        Engine.Dispose();
        Events.Dispose();
        Store.Dispose();
        Shutdown();
    }
}
