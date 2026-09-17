using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

/// <summary>A click-through ring drawn around the focused window so it is obvious where typing goes.</summary>
public partial class FocusBorderWindow : Window
{
    private nint _hwnd;
    private nint _target;
    private int _thickness = 3;
    private double _scale = 1.0;
    private readonly DispatcherTimer _follow = new() { Interval = TimeSpan.FromMilliseconds(30) };

    public FocusBorderWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetExStyle(_hwnd);
            Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE,
                (nint)(ex | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOPMOST | Win32.WS_EX_LAYERED));
            _scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        };
        _follow.Tick += (_, _) => Refresh();
    }

    public bool IsHandle(nint h) => h != 0 && h == _hwnd;

    public void Apply(TallonConfig cfg)
    {
        _thickness = cfg.Appearance.FocusBorderThickness;
        Ring.BorderThickness = new Thickness(_thickness);
        try { Ring.BorderBrush = (Brush)new BrushConverter().ConvertFromString(cfg.Appearance.FocusBorderColor)!; }
        catch { Ring.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0x9B, 0xE8)); }
        Refresh();
    }

    public void Track(nint hwnd)
    {
        _target = hwnd;
        Refresh();
    }

    public void SetDragging(bool dragging) { if (dragging) _follow.Start(); else { _follow.Stop(); Refresh(); } }

    public void Refresh()
    {
        var app = App.Current;
        if (_target == 0 || !app.Env.Active || !Win32.IsWindow(_target) || Win32.IsIconic(_target) || app.Env.Find(_target) == null)
        {
            if (IsVisible) Hide();
            return;
        }
        var r = Win32.GetVisibleRect(_target);
        int t = (int)Math.Round(_thickness * _scale);
        if (!IsVisible) Show();
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, r.Left - t, r.Top - t, r.Width + 2 * t, r.Height + 2 * t,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
    }
}
