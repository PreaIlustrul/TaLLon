using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TaLLon.Core.Config;
using TaLLon.Core.Input;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

/// <summary>Small top-centre pill: "Special key held", "Leader: waiting for key…", "Tiled", etc.</summary>
public partial class OsdWindow : Window
{
    private nint _hwnd;
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromMilliseconds(1300) };
    private bool _sticky;

    public OsdWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetExStyle(_hwnd);
            Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, (nint)(ex | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TRANSPARENT));
        };
        _hide.Tick += (_, _) => { _hide.Stop(); if (!_sticky) Hide(); };
    }

    public bool IsHandle(nint h) => h != 0 && h == _hwnd;

    public void Flash(string text)
    {
        _sticky = false;
        Present(text);
        _hide.Stop(); _hide.Start();
    }

    public void ShowState(SpecialState st, TallonConfig cfg)
    {
        switch (st)
        {
            case SpecialState.Held:
                _sticky = true; _hide.Stop();
                Present("Special key held — press a command key");
                break;
            case SpecialState.Leader:
                _sticky = true; _hide.Stop();
                Present($"Leader armed — press a command key within {cfg.LeaderTimeoutMs / 1000.0:0.#}s");
                break;
            default:
                _sticky = false;
                _hide.Stop(); _hide.Interval = TimeSpan.FromMilliseconds(150); _hide.Start();
                _hide.Interval = TimeSpan.FromMilliseconds(1300);
                break;
        }
    }

    private void Present(string text)
    {
        Text.Text = text;
        if (_hwnd == 0) new WindowInteropHelper(this).EnsureHandle();
        Show();
        UpdateLayout();
        var area = Win32.GetPrimaryMonitorRect(workArea: false);
        var src = PresentationSource.FromVisual(this);
        double scale = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int w = (int)(ActualWidth * scale), h = (int)(ActualHeight * scale);
        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, area.Left + (area.Width - w) / 2, area.Top + 40, w, h,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
    }
}
