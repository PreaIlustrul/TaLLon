using System.Drawing;
using System.Drawing.Drawing2D;
using WF = System.Windows.Forms;

namespace TaLLon.App;

/// <summary>Notification-area icon: the only always-visible piece of the dormant daemon.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly App _app;

    public TrayIcon(App app)
    {
        _app = app;
        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("Open / close TaLLon", null, (_, _) => _app.Wm.Toggle());
        menu.Items.Add("Settings…", null, (_, _) => _app.OpenSettings());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Open log folder", null, (_, _) =>
            System.Diagnostics.Process.Start("explorer.exe", Core.Config.ConfigStore.Directory));
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Quit TaLLon", null, (_, _) => _app.Quit());

        _icon = new WF.NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "TaLLon — dormant. Press the special key.",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => _app.OpenSettings();
        _app.Wm.Entered += () => _icon.Text = "TaLLon — active";
        _app.Wm.Exited += () => _icon.Text = "TaLLon — dormant. Press the special key.";
    }

    private static Icon MakeIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/tallon.ico");
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
