using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

/// <summary>
/// The "blank desktop" shown while TaLLon is active. Full-screen, never activates (WS_EX_NOACTIVATE),
/// so managed windows always sit above it.
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
        };
    }

    public bool IsHandle(nint h) => h != 0 && h == _hwnd;

    public void ShowCanvas(TallonConfig cfg)
    {
        ApplyAppearance(cfg);
        if (_hwnd == 0) { var _ = new WindowInteropHelper(this).EnsureHandle(); }
        var area = Win32.GetPrimaryMonitorRect(workArea: false);
        Show();
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOP, area.Left, area.Top, area.Width, area.Height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
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

        Hint.Visibility = cfg.Appearance.ShowHint ? Visibility.Visible : Visibility.Collapsed;
        string B(string k) => cfg.Bindings.TryGetValue(k, out var v) ? v : "?";
        HintText.Text = $"{B(Actions.OpenMenu)}  menu     {B(Actions.ToggleTiling)}  tile / restore     {B(Actions.OpenTerminal)}  terminal     {B(Actions.ToggleManager)}  exit";
    }
}
