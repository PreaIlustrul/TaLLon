using System.Diagnostics;
using System.Runtime.InteropServices;
using TaLLon.Core.Native;

namespace TaLLon.Core.Status;

public sealed record BatteryInfo(int Percent, bool Charging, bool Present);
public sealed record VolumeInfo(int Percent, bool Muted);
public sealed record WifiInfo(bool Connected, string Ssid, int SignalPercent);

/// <summary>Cheap readers for the top bar: battery (Win32), master volume (Core Audio COM), Wi-Fi (netsh).</summary>
public static class SystemStatus
{
    public static BatteryInfo Battery()
    {
        if (!Win32.GetSystemPowerStatus(out var s)) return new(0, false, false);
        bool present = s.BatteryFlag != 128 && s.BatteryLifePercent != 255;
        return new(present ? s.BatteryLifePercent : 0, s.ACLineStatus == 1, present);
    }

    public static VolumeInfo Volume()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var device) != 0 || device == null) return new(0, true);
            var iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, 23 /*CLSCTX_ALL*/, 0, out var obj) != 0) return new(0, true);
            var vol = (IAudioEndpointVolume)obj;
            vol.GetMasterVolumeLevelScalar(out float level);
            vol.GetMute(out bool mute);
            return new((int)Math.Round(level * 100), mute);
        }
        catch { return new(0, true); }
    }

    /// <summary>Slow (~100 ms): call from a background thread every ~15 s.</summary>
    public static WifiInfo Wifi()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return new(false, "", 0);
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            bool connected = false; string ssid = ""; int signal = 0;
            foreach (var raw in output.Split('\n'))
            {
                var line = raw.Trim();
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                var key = line[..colon].Trim(); var val = line[(colon + 1)..].Trim();
                if (key.Equals("State", StringComparison.OrdinalIgnoreCase)) connected = val.Contains("connected", StringComparison.OrdinalIgnoreCase) && !val.Contains("disconnected", StringComparison.OrdinalIgnoreCase);
                else if (key.Equals("SSID", StringComparison.OrdinalIgnoreCase)) ssid = val;
                else if (key.Equals("Signal", StringComparison.OrdinalIgnoreCase)) int.TryParse(val.TrimEnd('%'), out signal);
            }
            return new(connected, ssid, signal);
        }
        catch { return new(false, "", 0); }
    }

    // ---- Core Audio COM ------------------------------------------------------------------

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(nint client);
        int UnregisterEndpointNotificationCallback(nint client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        int OpenPropertyStore(int access, out nint properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(nint notify);
        int UnregisterControlChangeNotify(nint notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float level, ref Guid ctx);
        int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float level, ref Guid ctx);
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid ctx);
        int GetChannelVolumeLevel(uint channel, out float level);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
