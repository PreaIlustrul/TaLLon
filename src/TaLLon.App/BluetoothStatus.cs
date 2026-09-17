using TaLLon.Core.Config;
using TaLLon.Core.Status;

namespace TaLLon.App;

/// <summary>Bluetooth radio state through WinRT (Windows.Devices.Radios). null = unknown / no radio.</summary>
public static class BluetoothStatus
{
    private static bool _broken;

    public static bool? IsOn()
    {
        if (_broken) return null;
        try
        {
            var radios = global::Windows.Devices.Radios.Radio.GetRadiosAsync().AsTask().GetAwaiter().GetResult();
            var bt = radios.FirstOrDefault(r => r.Kind == global::Windows.Devices.Radios.RadioKind.Bluetooth);
            if (bt == null) return null;
            return bt.State == global::Windows.Devices.Radios.RadioState.On;
        }
        catch (Exception ex)
        {
            _broken = true;
            Log.Warn("bluetooth status unavailable: " + ex.Message);
            return null;
        }
    }
}

/// <summary>
/// Wi-Fi through WinRT's connection profile: works without the Location permission that
/// `netsh wlan` now requires on Windows 11. Falls back to netsh if WinRT is unavailable.
/// </summary>
public static class WifiStatus
{
    private static bool _broken;

    public static WifiInfo Read()
    {
        if (!_broken)
        {
            try
            {
                var profile = global::Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
                if (profile == null) return new WifiInfo(false, "", 0);
                if (!profile.IsWlanConnectionProfile) return new WifiInfo(true, "wired", 100);
                byte? bars = profile.GetSignalBars();
                int pct = bars.HasValue ? bars.Value * 20 : 100;
                return new WifiInfo(true, profile.ProfileName ?? "", pct);
            }
            catch (Exception ex)
            {
                _broken = true;
                Log.Warn("WinRT network status unavailable, using netsh: " + ex.Message);
            }
        }
        return SystemStatus.Wifi();
    }
}
