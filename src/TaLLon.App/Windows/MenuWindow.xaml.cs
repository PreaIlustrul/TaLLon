using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TaLLon.Core.Apps;
using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

public sealed class MenuEntry : INotifyPropertyChanged
{
    private BitmapSource? _icon;
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Hint { get; init; } = "";
    public string IconKey { get; init; } = "";
    public Action Run { get; init; } = () => { };
    public BitmapSource? Icon { get => _icon; set { _icon = value; PropertyChanged?.Invoke(this, new(nameof(Icon))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>Special+Space: the centred command palette (pinned apps, commands, windows, all apps).</summary>
public partial class MenuWindow : Window
{
    private nint _hwnd;
    private readonly List<MenuEntry> _commands = new();

    public MenuWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetExStyle(_hwnd);
            Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, (nint)(ex | Win32.WS_EX_TOOLWINDOW));
        };
        Search.TextChanged += (_, _) => Refresh();
        Deactivated += (_, _) => Hide();
        PreviewKeyDown += OnKey;
        List.MouseDoubleClick += (_, _) => RunSelected();
    }

    public bool IsHandle(nint h) => h != 0 && h == _hwnd;

    public void Open()
    {
        var app = App.Current;
        BuildCommands(app);
        Search.Text = "";
        Refresh();
        Show();
        var area = Win32.GetPrimaryMonitorRect(workArea: false);
        var src = PresentationSource.FromVisual(this);
        double scale = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int w = (int)(Width * scale), h = (int)(Height * scale);
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2, w, h, Win32.SWP_SHOWWINDOW);
        Win32.ForceForeground(_hwnd);
        Activate();
        Search.Focus();
        Keyboard.Focus(Search);
    }

    private void BuildCommands(App app)
    {
        _commands.Clear();
        var cfg = app.Config;
        var env = app.Env;
        string B(string k) => cfg.Bindings.TryGetValue(k, out var v) ? v : "";
        if (env.Active)
        {
            _commands.Add(new() { Kind = "command", Title = "Exit TaLLon (back to the desktop)", Hint = "tap Special / " + B(Actions.ExitEnvironment), Run = () => env.Exit() });
            _commands.Add(new() { Kind = "command", Title = env.Mode == EnvironmentMode.Infinite ? "Switch to tiling mode" : "Switch to infinite mode", Hint = B(Actions.SwitchMode), Run = () => env.SwitchMode() });
            _commands.Add(new() { Kind = "command", Title = env.OverviewActive ? "Close overview" : "Overview of all windows", Hint = B(Actions.Overview), Run = () => env.ToggleOverview() });
            if (env.Mode == EnvironmentMode.Infinite)
                _commands.Add(new() { Kind = "command", Title = "Go to (0, 0)", Hint = B(Actions.GoHome), Run = () => env.GoHome() });
        }
        else
            _commands.Add(new() { Kind = "command", Title = "Launch TaLLon", Hint = "tap Special", Run = () => env.Enter() });
        _commands.Add(new() { Kind = "command", Title = "Open terminal", Hint = B(Actions.OpenTerminal), Run = () => AppCatalog.LaunchTerminal(cfg) });
        _commands.Add(new() { Kind = "command", Title = "TaLLon settings", Hint = B(Actions.OpenSettings), Run = () => app.ShowMain() });
        _commands.Add(new() { Kind = "command", Title = "Refresh app list", Run = () => { _ = AppCatalog.RefreshAsync().ContinueWith(_ => Dispatcher.BeginInvoke(Refresh)); } });
        _commands.Add(new() { Kind = "command", Title = "Quit TaLLon completely", Hint = B(Actions.QuitApp), Run = () => app.Quit() });
        foreach (var l in cfg.Launchers)
            _commands.Add(new() { Kind = "launcher", Title = l.Name, Hint = l.Chord, IconKey = IconKeyFor(l.Command), Run = () => AppCatalog.LaunchCommand(l.Command, l.Args) });
    }

    private static string IconKeyFor(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return "";
        if (command.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return "";
        if (command.Contains('\\') || command.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return command;
        // bare exe name like "explorer.exe": resolve on PATH / system dir
        foreach (var dir in new[] { Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) }
                     .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')))
        {
            try { var p = System.IO.Path.Combine(dir, command); if (System.IO.File.Exists(p)) return p; } catch { }
        }
        return "";
    }

    private void Refresh()
    {
        var app = App.Current;
        var q = Search.Text.Trim();
        var items = new List<MenuEntry>();

        // pinned apps first
        foreach (var p in app.Config.PinnedApps)
        {
            if (!string.IsNullOrEmpty(q) && AppCatalog.Score(p.Name, q) == 0) continue;
            var target = p.LaunchTarget;
            items.Add(new() { Kind = "pinned", Title = p.Name, IconKey = target, Run = () => AppCatalog.Launch(new AppEntry(p.Name, target, "pinned")) });
        }

        items.AddRange(string.IsNullOrEmpty(q) ? _commands : _commands.Where(c => AppCatalog.Score(c.Title, q) > 0));

        foreach (var w in app.Env.LiveWindows())
        {
            var title = w.Title;
            if (!string.IsNullOrEmpty(q) && AppCatalog.Score(title, q) == 0) continue;
            var h = w.Hwnd;
            string hint = (w.Pinned ? "pinned " : "") + (w.Hibernated ? "hibernated " : "") + (w.Floating ? "floating" : "");
            items.Add(new() { Kind = "window", Title = title, Hint = hint.Trim(), IconKey = w.ExePath, Run = () => app.Env.Focus(h) });
        }

        var pinnedTargets = new HashSet<string>(app.Config.PinnedApps.Select(p => p.LaunchTarget), StringComparer.OrdinalIgnoreCase);
        foreach (var a in AppCatalog.Search(q, string.IsNullOrEmpty(q) ? 40 : 40))
        {
            if (pinnedTargets.Contains(a.LaunchTarget)) continue;
            items.Add(new() { Kind = "app", Title = a.Name, IconKey = a.LaunchTarget, Run = () => AppCatalog.Launch(a) });
        }

        foreach (var it in items)
        {
            if (it.IconKey == "") continue;
            var cached = app.Icons.Get(it.IconKey, bmp => it.Icon = bmp);
            if (cached != null) it.Icon = cached;
        }

        List.ItemsSource = items;
        if (items.Count > 0) List.SelectedIndex = 0;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Hide(); e.Handled = true; break;
            case Key.Down: Move(+1); e.Handled = true; break;
            case Key.Up: Move(-1); e.Handled = true; break;
            case Key.PageDown: Move(+8); e.Handled = true; break;
            case Key.PageUp: Move(-8); e.Handled = true; break;
            case Key.Enter: RunSelected(); e.Handled = true; break;
        }
    }

    private void Move(int d)
    {
        if (List.Items.Count == 0) return;
        List.SelectedIndex = Math.Clamp(List.SelectedIndex + d, 0, List.Items.Count - 1);
        List.ScrollIntoView(List.SelectedItem);
    }

    private void RunSelected()
    {
        if (List.SelectedItem is not MenuEntry m) return;
        Hide();
        try { m.Run(); } catch (Exception ex) { Log.Error("menu action failed: " + ex); }
    }
}
