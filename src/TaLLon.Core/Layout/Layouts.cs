using TaLLon.Core.Native;

namespace TaLLon.Core.Layout;

public enum Direction { Left, Right, Up, Down }

/// <summary>Pure geometry. dwm/vxwm master-stack, monocle, overview grid, directional neighbour search.</summary>
public static class Layouts
{
    public static Win32.RECT[] MasterStack(Win32.RECT area, int count, int masterCount, double masterRatio, int gap)
    {
        var result = new Win32.RECT[count];
        if (count == 0) return result;

        int x = area.Left + gap, y = area.Top + gap;
        int w = area.Width - 2 * gap, h = area.Height - 2 * gap;

        int nMaster = Math.Min(Math.Max(1, masterCount), count);
        int nStack = count - nMaster;
        int masterW = nStack == 0 ? w : (int)Math.Round((w - gap) * masterRatio);
        int stackW = nStack == 0 ? 0 : w - gap - masterW;

        int mh = (h - gap * (nMaster - 1)) / nMaster;
        for (int i = 0; i < nMaster; i++)
        {
            int top = y + i * (mh + gap);
            int bottom = i == nMaster - 1 ? y + h : top + mh;
            result[i] = new Win32.RECT(x, top, x + masterW, bottom);
        }
        if (nStack > 0)
        {
            int sx = x + masterW + gap;
            int sh = (h - gap * (nStack - 1)) / nStack;
            for (int i = 0; i < nStack; i++)
            {
                int top = y + i * (sh + gap);
                int bottom = i == nStack - 1 ? y + h : top + sh;
                result[nMaster + i] = new Win32.RECT(sx, top, sx + stackW, bottom);
            }
        }
        return result;
    }

    /// <summary>Win+Tab-like overview: every window gets a cell in a near-square grid with breathing room.</summary>
    public static Win32.RECT[] Grid(Win32.RECT area, int count, int margin)
    {
        var result = new Win32.RECT[count];
        if (count == 0) return result;
        int cols = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling(count / (double)cols);
        int cellW = (area.Width - margin * (cols + 1)) / cols;
        int cellH = (area.Height - margin * (rows + 1)) / rows;
        for (int i = 0; i < count; i++)
        {
            int c = i % cols, r = i / cols;
            int x = area.Left + margin + c * (cellW + margin);
            int y = area.Top + margin + r * (cellH + margin);
            result[i] = new Win32.RECT(x, y, x + cellW, y + cellH);
        }
        return result;
    }

    /// <summary>Cascade like Windows does for repeated instances: each next window offset down-right.</summary>
    public static Win32.RECT Cascade(Win32.RECT area, int w, int h, int index, int offset)
    {
        int cx = area.CenterX - w / 2, cy = area.CenterY - h / 2;
        int wrap = Math.Max(1, Math.Min(area.Width - w, area.Height - h) / Math.Max(1, offset));
        int k = index % (wrap + 1);
        int x = cx + (k - wrap / 2) * offset, y = cy + (k - wrap / 2) * offset;
        return Win32.RECT.FromSize(x, y, w, h);
    }

    /// <summary>Nearest rect in the given direction from `from` (45° cone, then by distance).</summary>
    public static int Neighbour(Win32.RECT from, IReadOnlyList<Win32.RECT> candidates, Direction dir)
    {
        int best = -1; double bestD = double.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            int dx = c.CenterX - from.CenterX, dy = c.CenterY - from.CenterY;
            if (dx == 0 && dy == 0) continue;
            int along = dir switch { Direction.Left => -dx, Direction.Right => dx, Direction.Up => -dy, _ => dy };
            int across = dir is Direction.Left or Direction.Right ? Math.Abs(dy) : Math.Abs(dx);
            if (along <= 0) continue;
            double d = along + across * 2.0;          // prefer windows straight ahead
            if (across > along * 1.5) d += 100000;      // outside the cone: last resort
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }
}
