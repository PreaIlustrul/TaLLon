using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TaLLon.Core.Config;
using TaLLon.Core.Native;
using TaLLon.Core.Status;

namespace TaLLon.App.Windows;

/// <summary>
/// Omarchy-style bar across the top of the environment: coordinates + mode on the left, actions and
/// the clock in the centre, Wi-Fi / Bluetooth / volume / battery on the right. Never takes focus.
/// </summary>
public partial class TopBarWindow : Window
{
    private nint _hwnd;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _status = new() { Interval = TimeSpan.FromSeconds(10) };
    private double _scale = 1.0;

    public const double LogicalHeight = 36;
    public int PhysicalHeight => (int)Math.Round(LogicalHeight * _scale);

    public TopBarWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetExStyle(_hwnd);
            Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, (nint)(ex | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TOPMOST));
            _scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        };
        _clock.Tick += (_, _) => Clock.Text = DateTime.Now.ToString("ddd d MMM  HH:mm");
        _status.Tick += (_, _) => RefreshStatus();
    }

    public bool IsHandle(nint h) => h != 0 && h == _hwnd;

    public void ShowBar(TallonConfig cfg)
    {
        var mon = Win32.GetPrimaryMonitorRect(workArea: false);
        Show();
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, mon.Left, mon.Top, mon.Width, PhysicalHeight, Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
        Clock.Text = DateTime.Now.ToString("ddd d MMM  HH:mm");
        _clock.Start();
        RefreshStatus();
        _status.Start();
        UpdateCoordinates();
        UpdateState();
    }

    public void HideBar()
    {
        _clock.Stop(); _status.Stop();
        Hide();
    }

    public void UpdateCoordinates()
    {
        var env = App.Current.Env;
        var area = env.Area;
        // Show the world coordinate of the centre of the view.
        Coords.Text = $"({env.Viewport.X + area.Width / 2}, {env.Viewport.Y + area.Height / 2})";
    }

    public void UpdateState()
    {
        var env = App.Current.Env;
        ModeText.Text = env.OverviewActive ? "overview" : env.Mode == EnvironmentMode.Infinite ? "infinite" : "tiling";
        OverviewBtn.Foreground = env.OverviewActive ? new SolidColorBrush(Color.FromRgb(0x4C, 0x9B, 0xE8)) : new SolidColorBrush(Color.FromRgb(0xD6, 0xDC, 0xE2));
    }

    /// <summary>True when the screen x lies over the button cluster or the status cluster (edge-pan avoidance).</summary>
    public bool IsInteractiveAt(int screenX)
    {
        try
        {
            double x = screenX / _scale;
            var c = Center.TransformToAncestor(this).Transform(new Point(0, 0));
            if (x >= c.X - 4 && x <= c.X + Center.ActualWidth + 4) return true;
            var r = Right.TransformToAncestor(this).Transform(new Point(0, 0));
            if (x >= r.X - 4) return true;
        }
        catch { }
        return false;
    }

    private void RefreshStatus()
    {
        var bat = SystemStatus.Battery();
        BatText.Text = bat.Present ? $"{bat.Percent}%" : "AC";
        BatIcon.Text = !bat.Present ? "" : bat.Charging ? "" : bat.Percent switch
        {
            > 90 => "", > 70 => "", > 50 => "", > 30 => "", > 10 => "", _ => "",
        };
        var vol = SystemStatus.Volume();
        VolText.Text = vol.Muted ? "mute" : $"{vol.Percent}%";
        VolIcon.Text = vol.Muted ? "" : vol.Percent switch { 0 => "", < 34 => "", < 67 => "", _ => "" };

        Task.Run(() =>
        {
            var wifi = WifiStatus.Read();
            var bt = BluetoothStatus.IsOn();
            Dispatcher.BeginInvoke(() =>
            {
                WifiText.Text = wifi.Connected ? (wifi.Ssid.Length > 18 ? wifi.Ssid[..18] + "…" : wifi.Ssid) : "off";
                WifiIcon.Text = wifi.Connected ? (wifi.SignalPercent > 75 ? "" : wifi.SignalPercent > 50 ? "" : wifi.SignalPercent > 25 ? "" : "") : "";
                BtText.Text = bt switch { true => "on", false => "off", null => "—" };
                BtIcon.Foreground = new SolidColorBrush(bt == true ? Color.FromRgb(0x4C, 0x9B, 0xE8) : Color.FromRgb(0x9A, 0xA6, 0xB2));
            });
        });
    }

    private void Menu_Click(object sender, RoutedEventArgs e) => App.Current.Menu.Open();
    private void Home_Click(object sender, RoutedEventArgs e) => App.Current.Env.GoHome();
    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.ShowMain();
    private void Overview_Click(object sender, RoutedEventArgs e) => App.Current.Env.ToggleOverview();
    private void Mode_Click(object sender, RoutedEventArgs e) => App.Current.Env.SwitchMode();
    private void Exit_Click(object sender, RoutedEventArgs e) => App.Current.Env.Exit();
}
