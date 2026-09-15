using TaLLon.Core.Native;

namespace TaLLon.Core.Layout;

public enum LayoutMode { Tiled, Monocle, Floating }

/// <summary>Pure geometry. dwm/vxwm master-stack: master column left, stack column right.</summary>
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

        // master column
        int mh = (h - gap * (nMaster - 1)) / nMaster;
        for (int i = 0; i < nMaster; i++)
        {
            int top = y + i * (mh + gap);
            int bottom = i == nMaster - 1 ? y + h : top + mh;
            result[i] = new Win32.RECT(x, top, x + masterW, bottom);
        }
        // stack column
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

    public static Win32.RECT[] Monocle(Win32.RECT area, int count, int gap)
    {
        var r = new Win32.RECT(area.Left + gap, area.Top + gap, area.Right - gap, area.Bottom - gap);
        var result = new Win32.RECT[count];
        Array.Fill(result, r);
        return result;
    }
}
