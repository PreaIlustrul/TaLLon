using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

/// <summary>
/// The desktop replacement shown while the environment is active. Full-screen, never activates
/// (WS_EX_NOACTIVATE) so managed windows always sit above it. Clicking it un-focuses everything;
/// two-finger scrolling over it pans the infinite canvas.
/// </summary>
public partial class CanvasWindow : Window
{
    private nint _hwnd;

    public CanvasWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetExStyle(_hwnd);
            Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, (nint)(ex | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW));
            HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        };
        MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Left) App.Current.Env.Unfocus(); };
        MouseWheel += (_, e) =>
        {
            var app = App.Current;
            int step = app.Config.Infinite.ScrollPanStep;
            int notches = -e.Delta / 120;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) app.Env.Pan(notches * step, 0);
            else app.Env.Pan(0, notches * step);
            e.Handled = true;
        };
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Win32.WM_MOUSEHWHEEL)
        {
            int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            var app = App.Current;
            app.Env.Pan(delta / 120 * app.Config.Infinite.ScrollPanStep, 0);
            handled = true;
        }
        return 0;
    }

    public bool IsHandle(nint h) => h != 0 && h == _hwnd;

    public void ShowCanvas(TallonConfig cfg)
    {
        ApplyAppearance(cfg);
        var area = Win32.GetPrimaryMonitorRect(workArea: false);
        Show();
        // Top of the normal z-order; the environment then lifts every managed window above us
        // (Environment.RaiseManagedAboveCanvas), so the canvas sits under them and over the desktop.
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOP, area.Left, area.Top, area.Width, area.Height, Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
    }

    public void HideCanvas() => Hide();

    private void ApplyAppearance(TallonConfig cfg)
    {
        try { Background = (Brush)new BrushConverter().ConvertFromString(cfg.Appearance.BackgroundColor)!; }
        catch { Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1a, 0x1f)); }

        BitmapImage? img = null;
        var path = cfg.Appearance.BackgroundImage;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(path, UriKind.Absolute);
                img.EndInit();
            }
            catch (Exception ex) { Log.Warn("background image failed: " + ex.Message); img = null; }
        }
        if (img == null)
        {
            try { img = new BitmapImage(new Uri("pack://application:,,,/Assets/default-bg.png")); }
            catch { img = null; }
        }
        Bg.Source = img;

        string B(string k) => cfg.Bindings.TryGetValue(k, out var v) ? v : "?";
        HintText.Text = $"{B(Actions.OpenMenu)}  menu     {B(Actions.SwitchMode)}  mode     {B(Actions.Overview)}  overview     {B(Actions.OpenTerminal)}  terminal     tap Special / {B(Actions.ExitEnvironment)}  exit";
        Hint.Visibility = cfg.Appearance.ShowTopBar ? Visibility.Collapsed : Visibility.Visible;
    }
}
