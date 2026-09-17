using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TaLLon.Core.Apps;
using TaLLon.Core.Config;
using TaLLon.Core.Input;
using TaLLon.Core.Session;

namespace TaLLon.App.Windows;

/// <summary>The TaLLon window: launch button + settings. Closing hides to the tray (configurable).</summary>
public partial class MainWindow : Window
{
    private readonly ConfigStore _store;
    private TallonConfig _cfg;
    private readonly Dictionary<string, KeyChordBox> _keyBoxes = new();
    private readonly List<LauncherRow> _launcherRows = new();
    private readonly List<PinnedAppConfig> _pinned = new();
    private readonly Action<KeyEvent> _rawKeyHandler;

    public MainWindow(ConfigStore store)
    {
        InitializeComponent();
        _store = store;
        _cfg = store.Current.Clone();
        var choices = KeyNames.SpecialKeyChoices.ToList();
        if (!choices.Contains(_cfg.SpecialKey.Key)) choices.Insert(0, _cfg.SpecialKey.Key);
        SpecialKeyCombo.ItemsSource = choices;
        DefaultMode.ItemsSource = new[] { "Infinite", "Tiling" };
        MasterRatio.ValueChanged += (_, _) => MasterRatioText.Text = $"{MasterRatio.Value:P0}";
        BgImage.TextChanged += (_, _) => UpdatePreview();
        BuildKeysPanel();
        Populate();
        ConfigPathText.Text = ConfigStore.FilePath;
        VersionText.Text = "TaLLon " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.2");
        UpdateEnvironmentState();

        _rawKeyHandler = ev => Dispatcher.BeginInvoke(() =>
        {
            KeyLog.AppendText(ev + System.Environment.NewLine);
            if (KeyLog.LineCount > 400) KeyLog.Text = string.Join(System.Environment.NewLine, KeyLog.Text.Split(System.Environment.NewLine).Skip(100));
            KeyLog.ScrollToEnd();
        });
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        App.Current.Engine.RawKey -= _rawKeyHandler;
        App.Current.Engine.Trace = false;
        if (App.Current.Config.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            App.Current.Tray?.ShowMinimizedHint();
        }
        else App.Current.Quit();
    }

    public void SelectPage(string page)
    {
        switch (page.ToLowerInvariant())
        {
            case "keys": NavKeys.IsChecked = true; break;
            case "launchers": NavLaunchers.IsChecked = true; break;
            case "menu": NavMenu.IsChecked = true; break;
            case "appearance": NavAppearance.IsChecked = true; break;
            case "diagnostics": NavDiagnostics.IsChecked = true; break;
            case "about": NavAbout.IsChecked = true; break;
            default: NavGeneral.IsChecked = true; break;
        }
    }

    public void UpdateEnvironmentState()
    {
        var env = App.Current.Env;
        if (env == null) return;
        LaunchBtn.Content = env.Active ? "Exit TaLLon" : "Launch TaLLon";
        EnvState.Text = env.Active
            ? $"Environment active — {(env.Mode == EnvironmentMode.Infinite ? "infinite" : "tiling")} mode, {env.Managed.Count} windows"
            : "Environment closed. Tap the special key or press Launch.";
    }

    private void Launch_Click(object sender, RoutedEventArgs e) => App.Current.Env.Toggle();

    // ---- build ---------------------------------------------------------------------------

    private void BuildKeysPanel()
    {
        foreach (var (key, label, help) in Actions.All)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            lbl.Children.Add(new TextBlock { Text = label });
            if (help != "") lbl.Children.Add(new TextBlock { Text = help, Style = (Style)FindResource("T.Dim") });
            var box = new KeyChordBox { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(box, 1);
            g.Children.Add(lbl); g.Children.Add(box);
            KeysPanel.Children.Add(g);
            _keyBoxes[key] = box;
        }
    }

    private void Populate()
    {
        SpecialCopilot.IsChecked = _cfg.SpecialKey.Kind == SpecialKeyKind.Copilot;
        SpecialOther.IsChecked = _cfg.SpecialKey.Kind == SpecialKeyKind.Key;
        SpecialKeyCombo.SelectedItem = _cfg.SpecialKey.Key;
        TapToggles.IsChecked = _cfg.TapTogglesEnvironment;
        DefaultMode.SelectedIndex = _cfg.DefaultMode == EnvironmentMode.Infinite ? 0 : 1;
        HideTaskbar.IsChecked = _cfg.HideTaskbar;
        CloseToTray.IsChecked = _cfg.CloseToTray;
        StartWithWindows.IsChecked = _cfg.StartWithWindows;
        TerminalCommand.Text = _cfg.TerminalCommand;
        TerminalArgs.Text = _cfg.TerminalArgs;

        foreach (var (k, box) in _keyBoxes) box.Chord = _cfg.Bindings.TryGetValue(k, out var v) ? v : "";

        LaunchersPanel.Children.Clear(); _launcherRows.Clear();
        foreach (var l in _cfg.Launchers) AddLauncherRow(l);

        _pinned.Clear(); _pinned.AddRange(_cfg.PinnedApps.Select(p => new PinnedAppConfig { Name = p.Name, LaunchTarget = p.LaunchTarget }));
        RenderPinned();

        BgImage.Text = _cfg.Appearance.BackgroundImage;
        BgColor.Text = _cfg.Appearance.BackgroundColor;
        ShowTopBar.IsChecked = _cfg.Appearance.ShowTopBar;
        FocusColor.Text = _cfg.Appearance.FocusBorderColor;
        FocusThickness.Text = _cfg.Appearance.FocusBorderThickness.ToString();
        NewWindowFraction.Text = Math.Round(1.0 / _cfg.Infinite.NewWindowAreaFraction).ToString();
        CascadeOffset.Text = _cfg.Infinite.CascadeOffset.ToString();
        EdgePanSpeed.Text = _cfg.Infinite.EdgePanSpeed.ToString();
        EdgePanMargin.Text = _cfg.Infinite.EdgePanMargin.ToString();
        ScrollPanStep.Text = _cfg.Infinite.ScrollPanStep.ToString();
        MasterRatio.Value = _cfg.Layout.MasterRatio;
        MasterRatioText.Text = $"{MasterRatio.Value:P0}";
        Gap.Text = _cfg.Layout.Gap.ToString();
        MasterCount.Text = _cfg.Layout.MasterCount.ToString();
        UpdatePreview();
    }

    private bool Collect(out string error)
    {
        error = "";
        _cfg.SpecialKey.Kind = SpecialCopilot.IsChecked == true ? SpecialKeyKind.Copilot : SpecialKeyKind.Key;
        var keyName = (SpecialKeyCombo.SelectedItem as string ?? "").Trim();
        if (_cfg.SpecialKey.Kind == SpecialKeyKind.Key)
        {
            if (!KeyNames.TryVk(keyName, out _)) { error = $"Unknown special key '{keyName}'."; return false; }
            _cfg.SpecialKey.Key = keyName;
        }
        _cfg.TapTogglesEnvironment = TapToggles.IsChecked == true;
        _cfg.DefaultMode = DefaultMode.SelectedIndex == 1 ? EnvironmentMode.Tiling : EnvironmentMode.Infinite;
        _cfg.HideTaskbar = HideTaskbar.IsChecked == true;
        _cfg.CloseToTray = CloseToTray.IsChecked == true;
        _cfg.StartWithWindows = StartWithWindows.IsChecked == true;
        _cfg.TerminalCommand = TerminalCommand.Text.Trim();
        _cfg.TerminalArgs = TerminalArgs.Text;

        var seen = new Dictionary<string, string>();
        foreach (var (k, box) in _keyBoxes)
        {
            var c = box.Chord;
            if (c == "") { error = $"'{Actions.All.First(a => a.Key == k).Label}' has no key."; return false; }
            if (seen.TryGetValue(c, out var other)) { error = $"{c} is used twice ({other} and {k})."; return false; }
            seen[c] = k;
            _cfg.Bindings[k] = c;
        }
        _cfg.Launchers = new List<LauncherConfig>();
        foreach (var r in _launcherRows)
        {
            var l = r.ToConfig();
            if (string.IsNullOrWhiteSpace(l.Command)) continue;
            if (l.Chord != "" && seen.TryGetValue(l.Chord, out var other)) { error = $"{l.Chord} is used twice ({other} and launcher '{l.Name}')."; return false; }
            if (l.Chord != "") seen[l.Chord] = "launcher:" + l.Name;
            _cfg.Launchers.Add(l);
        }
        _cfg.PinnedApps = _pinned.Select(p => new PinnedAppConfig { Name = p.Name, LaunchTarget = p.LaunchTarget }).ToList();

        _cfg.Appearance.BackgroundImage = BgImage.Text.Trim();
        _cfg.Appearance.BackgroundColor = BgColor.Text.Trim();
        _cfg.Appearance.ShowTopBar = ShowTopBar.IsChecked == true;
        _cfg.Appearance.FocusBorderColor = FocusColor.Text.Trim();
        if (!int.TryParse(FocusThickness.Text, out var ft) || ft < 1 || ft > 12) { error = "Border thickness must be 1–12."; return false; }
        _cfg.Appearance.FocusBorderThickness = ft;
        if (!double.TryParse(NewWindowFraction.Text, out var nf) || nf < 1 || nf > 50) { error = "New window size must be 1–50 (1/N of the screen)."; return false; }
        _cfg.Infinite.NewWindowAreaFraction = 1.0 / nf;
        if (!int.TryParse(CascadeOffset.Text, out var co) || co < 0 || co > 400) { error = "Cascade offset must be 0–400."; return false; }
        if (!int.TryParse(EdgePanSpeed.Text, out var ps) || ps < 1 || ps > 200) { error = "Edge pan speed must be 1–200."; return false; }
        if (!int.TryParse(EdgePanMargin.Text, out var pm) || pm < 0 || pm > 50) { error = "Edge pan margin must be 0–50."; return false; }
        if (!int.TryParse(ScrollPanStep.Text, out var ss) || ss < 5 || ss > 500) { error = "Scroll pan step must be 5–500."; return false; }
        _cfg.Infinite.CascadeOffset = co; _cfg.Infinite.EdgePanSpeed = ps; _cfg.Infinite.EdgePanMargin = pm; _cfg.Infinite.ScrollPanStep = ss;
        _cfg.Layout.MasterRatio = MasterRatio.Value;
        if (!int.TryParse(Gap.Text, out var gap) || gap < 0 || gap > 100) { error = "Gap must be 0–100."; return false; }
        if (!int.TryParse(MasterCount.Text, out var mc) || mc < 1 || mc > 5) { error = "Master windows must be 1–5."; return false; }
        _cfg.Layout.Gap = gap; _cfg.Layout.MasterCount = mc;
        return true;
    }

    // ---- launchers -----------------------------------------------------------------------

    private sealed class LauncherRow
    {
        public Grid Root = new() { Margin = new Thickness(0, 0, 0, 8) };
        public TextBox Name = new() { Width = 130, Margin = new Thickness(0, 0, 6, 0) };
        public KeyChordBox Chord = new() { Width = 240, Margin = new Thickness(0, 0, 6, 0) };
        public TextBox Command = new() { MinWidth = 170, Margin = new Thickness(0, 0, 6, 0) };
        public TextBox Args = new() { Width = 80, Margin = new Thickness(0, 0, 6, 0) };
        public LauncherConfig ToConfig() => new() { Name = Name.Text.Trim(), Chord = Chord.Chord, Command = Command.Text.Trim(), Args = Args.Text };
    }

    private void AddLauncherRow(LauncherConfig l)
    {
        var r = new LauncherRow();
        r.Name.Text = l.Name; r.Chord.Chord = l.Chord; r.Command.Text = l.Command; r.Args.Text = l.Args;
        var browse = new Button { Content = "Browse…", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 6, 0) };
        browse.Click += (_, _) =>
        {
            var d = new AppPickerDialog { Owner = this };
            if (d.ShowDialog() == true && d.Chosen != null)
            {
                r.Command.Text = d.Chosen.LaunchTarget;
                if (r.Name.Text.Trim() == "") r.Name.Text = d.Chosen.Name;
            }
        };
        var remove = new Button { Content = "✕", Width = 30, Padding = new Thickness(0), Style = (Style)FindResource("Btn.Danger") };
        remove.Click += (_, _) => { LaunchersPanel.Children.Remove(r.Root); _launcherRows.Remove(r); };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var e in new UIElement[] { r.Name, r.Chord, r.Command, browse, r.Args, remove }) sp.Children.Add(e);
        r.Root.Children.Add(sp);
        if (_launcherRows.Count == 0)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            foreach (var (t, w) in new[] { ("Name", 136), ("Keys", 246), ("Command / app / URL", 250), ("Args", 86) })
                header.Children.Add(new TextBlock { Text = t, Width = w, Style = (Style)FindResource("T.Dim") });
            LaunchersPanel.Children.Add(header);
        }
        LaunchersPanel.Children.Add(r.Root);
        _launcherRows.Add(r);
    }

    private void AddLauncher_Click(object sender, RoutedEventArgs e) => AddLauncherRow(new LauncherConfig());

    // ---- pinned apps ---------------------------------------------------------------------

    private void RenderPinned()
    {
        PinnedPanel.Children.Clear();
        if (_pinned.Count == 0)
        {
            PinnedPanel.Children.Add(new TextBlock { Text = "Nothing pinned yet.", Style = (Style)FindResource("T.Dim") });
            return;
        }
        foreach (var p in _pinned)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var img = new Image { Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            var cached = App.Current.Icons.Get(p.LaunchTarget, bmp => img.Source = bmp);
            if (cached != null) img.Source = cached;
            var name = new TextBlock { Text = p.Name, Width = 260, VerticalAlignment = VerticalAlignment.Center };
            var remove = new Button { Content = "✕", Width = 30, Padding = new Thickness(0), Style = (Style)FindResource("Btn.Danger") };
            var captured = p;
            remove.Click += (_, _) => { _pinned.Remove(captured); RenderPinned(); };
            row.Children.Add(img); row.Children.Add(name); row.Children.Add(remove);
            PinnedPanel.Children.Add(row);
        }
    }

    private void AddPinned_Click(object sender, RoutedEventArgs e)
    {
        var d = new AppPickerDialog { Owner = this };
        if (d.ShowDialog() == true && d.Chosen != null)
        {
            if (!_pinned.Any(p => p.LaunchTarget.Equals(d.Chosen.LaunchTarget, StringComparison.OrdinalIgnoreCase)))
                _pinned.Add(new PinnedAppConfig { Name = d.Chosen.Name, LaunchTarget = d.Chosen.LaunchTarget });
            RenderPinned();
        }
    }

    // ---- appearance ----------------------------------------------------------------------

    private void UpdatePreview()
    {
        try
        {
            var p = BgImage.Text.Trim();
            BgPreview.Source = !string.IsNullOrEmpty(p) && File.Exists(p)
                ? new BitmapImage(new Uri(p))
                : new BitmapImage(new Uri("pack://application:,,,/Assets/default-bg.png"));
        }
        catch { BgPreview.Source = null; }
    }

    private void BrowseBg_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*" };
        if (d.ShowDialog() == true) BgImage.Text = d.FileName;
    }

    private void DefaultBg_Click(object sender, RoutedEventArgs e) => BgImage.Text = "";

    // ---- diagnostics ---------------------------------------------------------------------

    private void Trace_Changed(object sender, RoutedEventArgs e)
    {
        var engine = App.Current.Engine;
        engine.RawKey -= _rawKeyHandler;
        if (TraceKeys.IsChecked == true) { engine.RawKey += _rawKeyHandler; engine.Trace = true; }
        else engine.Trace = false;
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => KeyLog.Clear();

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        App.Current.Env.Exit();
        CrashRestore.Run("diagnostics button");
        Status.Text = "Desktop restored.";
    }

    // ---- nav / save ----------------------------------------------------------------------

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageGeneral == null) return;
        PageGeneral.Visibility = NavGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageKeys.Visibility = NavKeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageLaunchers.Visibility = NavLaunchers.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageMenu.Visibility = NavMenu.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageAppearance.Visibility = NavAppearance.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageDiagnostics.Visibility = NavDiagnostics.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = NavAbout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Collect(out var err)) { Status.Text = "⚠ " + err; return; }
        _store.Save(_cfg);
        App.Current.ApplyConfig(_cfg.Clone());
        Status.Text = $"Saved {DateTime.Now:HH:mm:ss}";
    }

    private void Revert_Click(object sender, RoutedEventArgs e)
    {
        _cfg = _store.Load().Clone();
        Populate();
        Status.Text = "Reverted to saved config";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", ConfigStore.Directory) { UseShellExecute = true });
}
