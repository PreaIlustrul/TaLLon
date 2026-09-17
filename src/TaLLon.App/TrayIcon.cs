using System.Drawing;
using System.Drawing.Drawing2D;
using WF = System.Windows.Forms;

namespace TaLLon.App;

/// <summary>Notification-area icon. Double-click opens the TaLLon window.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly App _app;
    private readonly WF.ToolStripMenuItem _toggle;
    private bool _hintShown;

    public TrayIcon(App app)
    {
        _app = app;
        var menu = new WF.ContextMenuStrip();
        _toggle = new WF.ToolStripMenuItem("Launch TaLLon", null, (_, _) => _app.Env.Toggle());
        menu.Items.Add(_toggle);
        menu.Items.Add("Open TaLLon", null, (_, _) => _app.ShowMain());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Restore desktop now", null, (_, _) => { _app.Env.Exit(); Core.Session.CrashRestore.Run("tray"); });
        menu.Items.Add("Open log folder", null, (_, _) =>
            System.Diagnostics.Process.Start("explorer.exe", Core.Config.ConfigStore.Directory));
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => _app.Quit());

        _icon = new WF.NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "TaLLon",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => _app.ShowMain();
        _app.Env.Entered += () => { _toggle.Text = "Exit TaLLon"; _icon.Text = "TaLLon — active"; };
        _app.Env.Exited += () => { _toggle.Text = "Launch TaLLon"; _icon.Text = "TaLLon"; };
    }

    /// <summary>One-time hint when the window is closed to the tray.</summary>
    public void ShowMinimizedHint()
    {
        if (_hintShown) return;
        _hintShown = true;
        _icon.ShowBalloonTip(3000, "TaLLon", "Still running here. Tap the special key to open the environment, double-click to open the window.", WF.ToolTipIcon.None);
    }

    private static Icon MakeIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/TaLLon.ico");
            var s = System.Windows.Application.GetResourceStream(uri);
            if (s != null) return new Icon(s.Stream);
        }
        catch { }
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(new SolidBrush(Color.FromArgb(0x2b, 0x2b, 0x2b)), 0, 0, 32, 32);
            using var f = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString("T", f, new SolidBrush(Color.FromArgb(0x4c, 0x9b, 0xe8)), 8, 5);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
