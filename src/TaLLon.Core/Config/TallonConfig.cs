using System.Text.Json.Serialization;

namespace TaLLon.Core.Config;

/// <summary>Names of built-in actions. Keys of <see cref="TallonConfig.Bindings"/>.</summary>
public static class Actions
{
    public const string ToggleManager = "toggleManager";
    public const string OpenMenu = "openMenu";
    public const string ToggleTiling = "toggleTiling";
    public const string OpenTerminal = "openTerminal";
    public const string CloseWindow = "closeWindow";
    public const string FocusNext = "focusNext";
    public const string FocusPrev = "focusPrev";
    public const string ShrinkMaster = "shrinkMaster";
    public const string GrowMaster = "growMaster";
    public const string ToggleMonocle = "toggleMonocle";
    public const string ToggleFloat = "toggleFloat";
    public const string ZoomToMaster = "zoomToMaster";
    public const string OpenSettings = "openSettings";
    public const string QuitDaemon = "quitDaemon";

    /// <summary>Display labels in a stable order for the settings UI.</summary>
    public static readonly (string Key, string Label, string Help)[] All =
    {
        (ToggleManager, "Open / close TaLLon", "Enter or leave the TaLLon canvas"),
        (OpenMenu, "Command menu", "Centered menu: exit, launch apps, focus windows"),
        (ToggleTiling, "Tile / restore", "Arrange windows side by side or put them back"),
        (OpenTerminal, "Open terminal", "Runs the terminal command below"),
        (CloseWindow, "Close focused window", ""),
        (FocusNext, "Focus next window", ""),
        (FocusPrev, "Focus previous window", ""),
        (ShrinkMaster, "Shrink master area", ""),
        (GrowMaster, "Grow master area", ""),
        (ToggleMonocle, "Toggle monocle", "All windows full-screen, one at a time"),
        (ToggleFloat, "Toggle floating", "Detach focused window from tiling"),
        (ZoomToMaster, "Zoom to master", "Swap focused window into the master slot"),
        (OpenSettings, "Open settings app", ""),
        (QuitDaemon, "Quit TaLLon completely", "Stops the background listener"),
    };
}

public enum SpecialKeyKind { Copilot, Key }

public sealed class SpecialKeyConfig
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SpecialKeyKind Kind { get; set; } = SpecialKeyKind.Copilot;
    /// <summary>Key name (see <see cref="Input.KeyNames"/>) used when Kind == Key.</summary>
    public string Key { get; set; } = "CapsLock";
}

public sealed class LauncherConfig
{
    public string Name { get; set; } = "";
    public string Chord { get; set; } = "";
    public string Command { get; set; } = "";
    public string Args { get; set; } = "";
}

public sealed class AppearanceConfig
{
    /// <summary>Absolute path to an image; empty = built-in default.</summary>
    public string BackgroundImage { get; set; } = "";
    public string BackgroundColor { get; set; } = "#161a1f";
    public bool ShowHint { get; set; } = true;
}

public sealed class LayoutConfig
{
    public double MasterRatio { get; set; } = 0.55;
    public int MasterCount { get; set; } = 1;
    public int Gap { get; set; } = 8;
    public bool StartTiled { get; set; } = true;
}

public sealed class TallonConfig
{
    public int Version { get; set; } = 1;
    public SpecialKeyConfig SpecialKey { get; set; } = new();
    /// <summary>After a tap of the special key, how long the next key may follow (leader mode).</summary>
    public int LeaderTimeoutMs { get; set; } = 2500;
    /// <summary>A press shorter than this with no chord key counts as a "tap" that arms leader mode.</summary>
    public int TapThresholdMs { get; set; } = 400;
    public Dictionary<string, string> Bindings { get; set; } = DefaultBindings();
    public List<LauncherConfig> Launchers { get; set; } = new()
    {
        new LauncherConfig { Name = "File Explorer", Chord = "Special+E", Command = "explorer.exe" },
        new LauncherConfig { Name = "Browser", Chord = "Special+B", Command = "https://www.google.com" },
    };
    public string TerminalCommand { get; set; } = "wt.exe";
    public string TerminalArgs { get; set; } = "";
    public AppearanceConfig Appearance { get; set; } = new();
    public LayoutConfig Layout { get; set; } = new();
    public bool HideTaskbar { get; set; } = true;
    public bool MinimizeOthersOnEnter { get; set; } = true;

    public static Dictionary<string, string> DefaultBindings() => new()
    {
        [Actions.ToggleManager] = "Special+W",
        [Actions.OpenMenu] = "Special+Space",
        [Actions.ToggleTiling] = "Special+T",
        [Actions.OpenTerminal] = "Special+Enter",
        [Actions.CloseWindow] = "Special+Q",
        [Actions.FocusNext] = "Special+J",
        [Actions.FocusPrev] = "Special+K",
        [Actions.ShrinkMaster] = "Special+H",
        [Actions.GrowMaster] = "Special+L",
        [Actions.ToggleMonocle] = "Special+M",
        [Actions.ToggleFloat] = "Special+F",
        [Actions.ZoomToMaster] = "Special+Z",
        [Actions.OpenSettings] = "Special+Comma",
        [Actions.QuitDaemon] = "Special+Shift+Q",
    };

    /// <summary>Fills in any binding missing from an older config file.</summary>
    public void Normalize()
    {
        foreach (var (k, v) in DefaultBindings())
            Bindings.TryAdd(k, v);
        Layout.MasterRatio = Math.Clamp(Layout.MasterRatio, 0.1, 0.9);
        Layout.MasterCount = Math.Max(1, Layout.MasterCount);
        Layout.Gap = Math.Clamp(Layout.Gap, 0, 100);
    }

    public TallonConfig Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, ConfigStore.JsonOptions);
        return System.Text.Json.JsonSerializer.Deserialize<TallonConfig>(json, ConfigStore.JsonOptions)!;
    }
}
