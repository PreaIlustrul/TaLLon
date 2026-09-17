using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.App.Windows;

/// <summary>
/// Right-click on a managed window's title bar: pin, hibernate, float, close. A tiny transparent
/// window hosts the WPF ContextMenu at the cursor.
/// </summary>
public sealed class WindowContextMenu : Window
{
    private nint _hwnd;
    private readonly ContextMenu _menu = new();

    public WindowContextMenu()
    {
        Title = "TaLLon";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        Width = 2; Height = 2;
        Visibility = Visibility.Hidden;
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetExStyle(_hwnd);
            Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, (nint)(ex | Win32.WS_EX_TOOLWINDOW));
        };
        _menu.Closed += (_, _) => Hide();
        _menu.Background = new SolidColorBrush(Color.FromRgb(0x2b, 0x2b, 0x2b));
        _menu.Foreground = Brushes.White;
        _menu.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3f, 0x3f, 0x3f));
    }

    public bool IsHandle(nint h) => h != 0 && h == _hwnd;

    public void ShowFor(nint target, int x, int y)
    {
        var app = App.Current;
        var m = app.Env.Find(target);
        if (m == null) return;
        _menu.Items.Clear();
        var title = m.Title;
        _menu.Items.Add(new MenuItem { Header = title.Length > 40 ? title[..40] + "…" : title, IsEnabled = false, FontWeight = FontWeights.SemiBold });
        _menu.Items.Add(new Separator());
        Add(m.Pinned ? "Unpin from grid" : "Pin to grid", () => app.Env.TogglePin(target));
        Add(m.Hibernated ? "Wake up" : "Hibernate (suspend process)", () => app.Env.ToggleHibernate(target));
        if (app.Env.Mode == EnvironmentMode.Tiling)
            Add(m.Floating ? "Tile" : "Float", () => { app.Env.Focus(target); app.Env.ToggleFloatFocused(); });
        Add("Focus", () => app.Env.Focus(target));
        _menu.Items.Add(new Separator());
        Add("Close window", () => Win32.PostMessage(target, Win32.WM_CLOSE, 0, 0));

        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        Left = x / scale; Top = y / scale;
        Show();
        Win32.ForceForeground(_hwnd);
        Activate();
        _menu.PlacementTarget = this;
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        _menu.IsOpen = true;
    }

    private void Add(string header, Action act)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => { try { act(); } catch (Exception ex) { Log.Error("context menu: " + ex); } };
        _menu.Items.Add(item);
    }
}
