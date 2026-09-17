using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

/// <summary>Small pill under the top bar: "Tiling mode", "Pinned", "Special+9 — not bound", …</summary>
public partial class OsdWindow : Window
{
    private nint _hwnd;
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromMilliseconds(1300) };

    public OsdWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetExStyle(_hwnd);
            Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, (nint)(ex | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TRANSPARENT));
        };
        _hide.Tick += (_, _) => { _hide.Stop(); Hide(); };
    }

    public bool IsHandle(nint h) => h != 0 && h == _hwnd;

    public void Flash(string text)
    {
        Text.Text = text;
        Show();
        UpdateLayout();
        var area = Win32.GetPrimaryMonitorRect(workArea: false);
        var src = PresentationSource.FromVisual(this);
        double scale = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int w = (int)(ActualWidth * scale), h = (int)(ActualHeight * scale);
        int top = area.Top + App.Current.Env.TopBarHeight + (int)(12 * scale);
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, area.Left + (area.Width - w) / 2, top, w, h,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
        _hide.Stop(); _hide.Start();
    }
}
