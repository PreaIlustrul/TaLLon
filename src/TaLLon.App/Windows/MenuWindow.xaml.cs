using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TaLLon.Core.Apps;
using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

public sealed record MenuEntry(string Kind, string Title, string Hint, Action Run);

/// <summary>Special+Space: the centred command palette.</summary>
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
        if (_hwnd == 0) new WindowInteropHelper(this).EnsureHandle();
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
        App.Current.Engine.CancelLeader();
    }

    private void BuildCommands(App app)
    {
        _commands.Clear();
        var cfg = app.Config;
        string B(string k) => cfg.Bindings.TryGetValue(k, out var v) ? v : "";
        if (app.Wm.Active)
            _commands.Add(new("command", "Exit TaLLon (back to desktop)", B(Actions.ToggleManager), () => app.Wm.Exit()));
        else
            _commands.Add(new("command", "Open TaLLon canvas", B(Actions.ToggleManager), () => app.Wm.Enter()));
        _commands.Add(new("command", "Tile / restore windows", B(Actions.ToggleTiling), () => app.Wm.ToggleTiling()));
        _commands.Add(new("command", "Toggle monocle layout", B(Actions.ToggleMonocle), () => app.Wm.ToggleMonocle()));
        _commands.Add(new("command", "Open terminal", B(Actions.OpenTerminal), () => AppCatalog.LaunchTerminal(cfg)));
        _commands.Add(new("command", "TaLLon settings", B(Actions.OpenSettings), () => app.OpenSettings()));
        _commands.Add(new("command", "Refresh app list", "", () => { _ = AppCatalog.RefreshAsync().ContinueWith(_ => Dispatcher.BeginInvoke(Refresh)); }));
        _commands.Add(new("command", "Quit TaLLon completely", B(Actions.QuitDaemon), () => app.Quit()));
        foreach (var l in cfg.Launchers)
            _commands.Add(new("launcher", l.Name, l.Chord, () => AppCatalog.LaunchCommand(l.Command, l.Args)));
    }

    private void Refresh()
    {
        var app = App.Current;
        var q = Search.Text.Trim();
        var items = new List<MenuEntry>();

        IEnumerable<MenuEntry> cmds = string.IsNullOrEmpty(q)
            ? _commands
            : _commands.Where(c => AppCatalog.Score(c.Title, q) > 0);
        items.AddRange(cmds);

        foreach (var w in app.Wm.LiveWindows())
        {
            var title = w.Title;
            if (string.IsNullOrEmpty(q) || AppCatalog.Score(title, q) > 0)
            {
                var h = w.Hwnd;
                items.Add(new("window", title, w.Floating ? "floating" : "", () => app.Wm.Focus(h)));
            }
        }

        foreach (var a in AppCatalog.Search(q, string.IsNullOrEmpty(q) ? 30 : 40))
            items.Add(new("app", a.Name, "", () => AppCatalog.Launch(a)));

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
