using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TaLLon.Core.Config;
using TaLLon.Core.Input;

namespace TaLLon.App.Windows;

/// <summary>The "TaLLon Settings" app. Thin editor over config.json (D7).</summary>
public partial class SettingsWindow : Window
{
    private readonly ConfigStore _store;
    private TallonConfig _cfg;
    private readonly Dictionary<string, KeyChordBox> _keyBoxes = new();
    private readonly List<LauncherRow> _launcherRows = new();
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public SettingsWindow(ConfigStore store)
    {
        InitializeComponent();
        _store = store;
        _cfg = store.Current.Clone();
        SpecialKeyCombo.ItemsSource = KeyNames.SpecialKeyChoices;
        MasterRatio.ValueChanged += (_, _) => MasterRatioText.Text = $"{MasterRatio.Value:P0}";
        BgImage.TextChanged += (_, _) => UpdatePreview();
        BuildKeysPanel();
        Populate();
        ConfigPathText.Text = ConfigStore.FilePath;
        VersionText.Text = "TaLLon " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1");
        DaemonStatus.Text = App.Current.Wm != null
            ? "Listener running in this process. " + (App.Current.Wm.Active ? "TaLLon is ACTIVE." : "TaLLon is dormant.")
            : "Settings-only mode. Start TaLLon.exe (without --settings) to run the listener.";
    }

    // ---- build ---------------------------------------------------------------------------

    private void BuildKeysPanel()
    {
        foreach (var (key, label, help) in Actions.All)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
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
        SpecialKeyCombo.Text = _cfg.SpecialKey.Key;
        LeaderTimeout.Text = _cfg.LeaderTimeoutMs.ToString();
        TapThreshold.Text = _cfg.TapThresholdMs.ToString();
        HideTaskbar.IsChecked = _cfg.HideTaskbar;
        MinimizeOthers.IsChecked = _cfg.MinimizeOthersOnEnter;
        TerminalCommand.Text = _cfg.TerminalCommand;
        TerminalArgs.Text = _cfg.TerminalArgs;
        RunAtLogin.IsChecked = IsRunAtLogin();

        foreach (var (k, box) in _keyBoxes) box.Chord = _cfg.Bindings.TryGetValue(k, out var v) ? v : "";

        LaunchersPanel.Children.Clear(); _launcherRows.Clear();
        foreach (var l in _cfg.Launchers) AddLauncherRow(l);

        BgImage.Text = _cfg.Appearance.BackgroundImage;
        BgColor.Text = _cfg.Appearance.BackgroundColor;
        ShowHint.IsChecked = _cfg.Appearance.ShowHint;
        MasterRatio.Value = _cfg.Layout.MasterRatio;
        MasterRatioText.Text = $"{MasterRatio.Value:P0}";
        Gap.Text = _cfg.Layout.Gap.ToString();
        MasterCount.Text = _cfg.Layout.MasterCount.ToString();
        StartTiled.IsChecked = _cfg.Layout.StartTiled;
        UpdatePreview();
    }

    private bool Collect(out string error)
    {
        error = "";
        _cfg.SpecialKey.Kind = SpecialCopilot.IsChecked == true ? SpecialKeyKind.Copilot : SpecialKeyKind.Key;
        var keyName = SpecialKeyCombo.Text.Trim();
        if (_cfg.SpecialKey.Kind == SpecialKeyKind.Key)
        {
            if (!KeyNames.TryVk(keyName, out _)) { error = $"Unknown special key '{keyName}'."; return false; }
            _cfg.SpecialKey.Key = keyName;
        }
        if (!int.TryParse(LeaderTimeout.Text, out var lt) || lt < 300) { error = "Leader timeout must be ≥ 300 ms."; return false; }
        if (!int.TryParse(TapThreshold.Text, out var tt) || tt < 50) { error = "Tap threshold must be ≥ 50 ms."; return false; }
        _cfg.LeaderTimeoutMs = lt; _cfg.TapThresholdMs = tt;
        _cfg.HideTaskbar = HideTaskbar.IsChecked == true;
        _cfg.MinimizeOthersOnEnter = MinimizeOthers.IsChecked == true;
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

        _cfg.Appearance.BackgroundImage = BgImage.Text.Trim();
        _cfg.Appearance.BackgroundColor = BgColor.Text.Trim();
        _cfg.Appearance.ShowHint = ShowHint.IsChecked == true;
        _cfg.Layout.MasterRatio = MasterRatio.Value;
        if (!int.TryParse(Gap.Text, out var gap) || gap < 0 || gap > 100) { error = "Gap must be 0–100."; return false; }
        if (!int.TryParse(MasterCount.Text, out var mc) || mc < 1 || mc > 5) { error = "Master windows must be 1–5."; return false; }
        _cfg.Layout.Gap = gap; _cfg.Layout.MasterCount = mc;
        _cfg.Layout.StartTiled = StartTiled.IsChecked == true;
        return true;
    }

    // ---- launchers -----------------------------------------------------------------------

    private sealed class LauncherRow
    {
        public Grid Root = new() { Margin = new Thickness(0, 0, 0, 8) };
        public TextBox Name = new() { Width = 130, Margin = new Thickness(0, 0, 6, 0) };
        public KeyChordBox Chord = new() { Width = 250, Margin = new Thickness(0, 0, 6, 0) };
        public TextBox Command = new() { MinWidth = 160, Margin = new Thickness(0, 0, 6, 0) };
        public TextBox Args = new() { Width = 90, Margin = new Thickness(0, 0, 6, 0) };
        public LauncherConfig ToConfig() => new() { Name = Name.Text.Trim(), Chord = Chord.Chord, Command = Command.Text.Trim(), Args = Args.Text };
    }

    private void AddLauncherRow(LauncherConfig l)
    {
        var r = new LauncherRow();
        r.Name.Text = l.Name; r.Chord.Chord = l.Chord; r.Command.Text = l.Command; r.Args.Text = l.Args;
        var browse = new Button { Content = "…", Width = 30, Padding = new Thickness(0), Margin = new Thickness(0, 0, 6, 0) };
        browse.Click += (_, _) =>
        {
            var d = new OpenFileDialog { Filter = "Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd|All files|*.*" };
            if (d.ShowDialog() == true) { r.Command.Text = d.FileName; if (r.Name.Text == "") r.Name.Text = Path.GetFileNameWithoutExtension(d.FileName); }
        };
        var remove = new Button { Content = "✕", Width = 30, Padding = new Thickness(0), Style = (Style)FindResource("Btn.Danger") };
        remove.Click += (_, _) => { LaunchersPanel.Children.Remove(r.Root); _launcherRows.Remove(r); };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var e in new UIElement[] { r.Name, r.Chord, r.Command, browse, r.Args, remove }) sp.Children.Add(e);
        r.Root.Children.Add(sp);
        if (_launcherRows.Count == 0)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            foreach (var (t, w) in new[] { ("Name", 136), ("Keys", 256), ("Command / path / URL", 202), ("Args", 96) })
                header.Children.Add(new TextBlock { Text = t, Width = w, Style = (Style)FindResource("T.Dim") });
            LaunchersPanel.Children.Add(header);
        }
        LaunchersPanel.Children.Add(r.Root);
        _launcherRows.Add(r);
    }

    private void AddLauncher_Click(object sender, RoutedEventArgs e) => AddLauncherRow(new LauncherConfig());

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

    // ---- nav / save ----------------------------------------------------------------------

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageGeneral == null) return;
        PageGeneral.Visibility = NavGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageKeys.Visibility = NavKeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageLaunchers.Visibility = NavLaunchers.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageAppearance.Visibility = NavAppearance.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = NavAbout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!Collect(out var err)) { Status.Text = "⚠ " + err; return; }
        _store.Save(_cfg);
        SetRunAtLogin(RunAtLogin.IsChecked == true);
        if (App.Current.Wm != null) App.Current.ApplyConfig(_cfg.Clone());
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

    private static bool IsRunAtLogin()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("TaLLon") != null; }
        catch { return false; }
    }

    private static void SetRunAtLogin(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (k == null) return;
            if (on) k.SetValue("TaLLon", $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue("TaLLon", throwOnMissingValue: false);
        }
        catch (Exception ex) { Log.Warn("run-at-login failed: " + ex.Message); }
    }
}
