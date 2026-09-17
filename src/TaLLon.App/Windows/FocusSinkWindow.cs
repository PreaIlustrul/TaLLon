using System.Windows;
using System.Windows.Interop;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

/// <summary>
/// An invisible 1×1 window that can take keyboard focus, so "no window is focused" is a real state
/// (clicking the canvas gives focus to this instead of leaving it on the last app).
/// </summary>
public sealed class FocusSinkWindow : Window
{
    public nint Handle { get; private set; }

    public FocusSinkWindow()
    {
        Title = "TaLLon";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Width = 1; Height = 1;
        Left = -32000; Top = -32000;
        Opacity = 0;
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetExStyle(Handle);
            Win32.SetWindowLongPtr(Handle, Win32.GWL_EXSTYLE, (nint)(ex | Win32.WS_EX_TOOLWINDOW));
            Show();
        };
    }

    public bool IsHandle(nint h) => h != 0 && h == Handle;
}
