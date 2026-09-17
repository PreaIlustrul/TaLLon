using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaLLon.Core.Config;

namespace TaLLon.App;

/// <summary>
/// App / file / window icons through the shell (IShellItemImageFactory), which understands both
/// classic paths and "shell:AppsFolder\AUMID" Store apps. Loads in the background; results are
/// frozen BitmapSources safe to bind from any thread.
/// </summary>
public sealed class IconCache
{
    private readonly Dictionary<string, BitmapSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loading = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public BitmapSource? TryGet(string key)
    {
        lock (_gate) return _cache.TryGetValue(key, out var v) ? v : null;
    }

    /// <summary>Returns the cached icon immediately, or null and calls back on the UI thread once loaded.</summary>
    public BitmapSource? Get(string key, Action<BitmapSource?> onLoaded)
    {
        if (string.IsNullOrEmpty(key)) return null;
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var v)) return v;
            if (!_loading.Add(key)) { return null; }
        }
        Task.Run(() =>
        {
            BitmapSource? bmp = null;
            try { bmp = Load(key, 32); } catch (Exception ex) { Log.Warn($"icon {key}: {ex.Message}"); }
            lock (_gate) { _cache[key] = bmp; _loading.Remove(key); }
            Application.Current?.Dispatcher.BeginInvoke(() => onLoaded(bmp));
        });
        return null;
    }

    public static BitmapSource? Load(string parsingName, int size)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        int hr = SHCreateItemFromParsingName(parsingName, 0, ref iid, out var obj);
        if (hr != 0 || obj == null) return null;
        var factory = (IShellItemImageFactory)obj;
        hr = factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out var hbm);
        if (hr != 0 || hbm == 0) return null;
        try { return FromHBitmap(hbm); }
        finally { DeleteObject(hbm); }
    }

    /// <summary>Preserves the alpha channel (the WPF Imaging helper would flatten it to black).</summary>
    private static BitmapSource? FromHBitmap(nint hbm)
    {
        var bm = new BITMAP();
        if (GetObject(hbm, Marshal.SizeOf<BITMAP>(), ref bm) == 0) return null;
        if (bm.bmBits == 0 || bm.bmBitsPixel != 32)
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hbm, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        int stride = bm.bmWidthBytes;
        var bytes = new byte[stride * bm.bmHeight];
        Marshal.Copy(bm.bmBits, bytes, 0, bytes.Length);
        // DIB sections from the shell are top-down already when bmHeight > 0 via GetObject? They are bottom-up;
        // flip rows so the icon is not upside down.
        var flipped = new byte[bytes.Length];
        for (int y = 0; y < bm.bmHeight; y++)
            Buffer.BlockCopy(bytes, y * stride, flipped, (bm.bmHeight - 1 - y) * stride, stride);
        var bs = BitmapSource.Create(bm.bmWidth, bm.bmHeight, 96, 96, PixelFormats.Pbgra32, null, flipped, stride);
        bs.Freeze();
        return bs;
    }

    // ---- interop -------------------------------------------------------------------------

    private const uint SIIGBF_BIGGERSIZEOK = 0x1, SIIGBF_ICONONLY = 0x4;

    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public nint bmBits;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, uint flags, out nint phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string pszPath, nint pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? ppv);

    [DllImport("gdi32.dll")] private static extern int GetObject(nint h, int c, ref BITMAP pv);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint h);
}
