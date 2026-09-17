using System.Text.Json.Serialization;

namespace TaLLon.Core.Config;

/// <summary>Names of built-in actions. Keys of <see cref="TallonConfig.Bindings"/>.</summary>
public static class Actions
{
    public const string ExitEnvironment = "exitEnvironment";
    public const string OpenMenu = "openMenu";
    public const string SwitchMode = "switchMode";
    public const string Overview = "overview";
    public const string GoHome = "goHome";
    public const string OpenTerminal = "openTerminal";
    public const string CloseWindow = "closeWindow";
    public const string FocusLeft = "focusLeft";
    public const string FocusRight = "focusRight";
    public const string FocusUp = "focusUp";
    public const string FocusDown = "focusDown";
    public const string SwapLeft = "swapLeft";
    public const string SwapRight = "swapRight";
    public const string SwapUp = "swapUp";
    public const string SwapDown = "swapDown";
    public const string FocusNext = "focusNext";
    public const string FocusPrev = "focusPrev";
    public const string PinWindow = "pinWindow";
    public const string HibernateWindow = "hibernateWindow";
    public const string ToggleFloat = "toggleFloat";
    public const string ZoomToMaster = "zoomToMaster";
    public const string ShrinkMaster = "shrinkMaster";
    public const string GrowMaster = "growMaster";
    public const string OpenSettings = "openSettings";
    public const string QuitApp = "quitApp";

    /// <summary>Display labels in a stable order for the settings UI.</summary>
    public static readonly (string Key, string Label, string Help)[] All =
    {
        (ExitEnvironment, "Exit the environment", "Tapping the special key alone also enters / exits"),
        (OpenMenu, "Command menu", "Centered menu: pinned apps, all apps, open windows, actions"),
        (SwitchMode, "Switch mode", "Infinite ⇄ tiling"),
        (Overview, "Overview", "All windows in a grid; Alt+Tab / Win+Tab do the same inside the environment"),
        (GoHome, "Go to (0, 0)", "Infinite mode: pan the view back to the origin"),
        (OpenTerminal, "Open terminal", "Runs the terminal command"),
        (CloseWindow, "Close focused window", ""),
        (FocusLeft, "Focus window to the left", ""),
        (FocusRight, "Focus window to the right", ""),
        (FocusUp, "Focus window above", ""),
        (FocusDown, "Focus window below", ""),
        (SwapLeft, "Swap with window to the left", "Tiling mode"),
        (SwapRight, "Swap with window to the right", "Tiling mode"),
        (SwapUp, "Swap with window above", "Tiling mode"),
        (SwapDown, "Swap with window below", "Tiling mode"),
        (FocusNext, "Focus next window", ""),
        (FocusPrev, "Focus previous window", ""),
        (PinWindow, "Pin / unpin focused window", "Infinite mode: remembers its grid position between sessions"),
        (HibernateWindow, "Hibernate / wake focused window", "Suspends the app's process until it is focused again"),
        (ToggleFloat, "Toggle floating", "Tiling mode: detach the focused window from the grid"),
        (ZoomToMaster, "Zoom to master", "Tiling mode: swap focused window into the master slot"),
        (ShrinkMaster, "Shrink master area", "Tiling mode"),
        (GrowMaster, "Grow master area", "Tiling mode"),
        (OpenSettings, "Open TaLLon window", ""),
        (QuitApp, "Quit TaLLon completely", ""),
    };
}

public enum SpecialKeyKind { Copilot, Key }
public enum EnvironmentMode { Infinite, Tiling }

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

/// <summary>An app pinned to the top of the command menu.</summary>
public sealed class PinnedAppConfig
{
    public string Name { get; set; } = "";
    public string LaunchTarget { get; set; } = "";
}

public sealed class AppearanceConfig
{
    /// <summary>Absolute path to an image; empty = built-in default.</summary>
    public string BackgroundImage { get; set; } = "";
    public string BackgroundColor { get; set; } = "#161a1f";
    public bool ShowTopBar { get; set; } = true;
    public string FocusBorderColor { get; set; } = "#4C9BE8";
    public int FocusBorderThickness { get; set; } = 3;
}

public sealed class LayoutConfig
{
    public double MasterRatio { get; set; } = 0.55;
    public int MasterCount { get; set; } = 1;
    public int Gap { get; set; } = 8;
}

public sealed class InfiniteConfig
{
    /// <summary>New windows spawn at this fraction of the screen AREA (1/12 ⇒ each side is 1/√12 of the screen).</summary>
    public double NewWindowAreaFraction { get; set; } = 1.0 / 12.0;
    /// <summary>Pixel offset between cascaded windows when the environment opens.</summary>
    public int CascadeOffset { get; set; } = 48;
    /// <summary>Pan speed (px per tick, 60 ticks/s) when the mouse touches a screen edge.</summary>
    public int EdgePanSpeed { get; set; } = 24;
    /// <summary>How close to the edge (px) the mouse must be to pan. 0 disables edge panning.</summary>
    public int EdgePanMargin { get; set; } = 3;
    /// <summary>Pixels moved per trackpad scroll notch.</summary>
    public int ScrollPanStep { get; set; } = 60;
}

public sealed class TallonConfig
{
    public int Version { get; set; } = 2;
    public SpecialKeyConfig SpecialKey { get; set; } = new();
    /// <summary>Tapping the special key alone (press, release, nothing in between) enters / exits the environment.</summary>
    public bool TapTogglesEnvironment { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnvironmentMode DefaultMode { get; set; } = EnvironmentMode.Infinite;
    public Dictionary<string, string> Bindings { get; set; } = DefaultBindings();
    public List<LauncherConfig> Launchers { get; set; } = new()
    {
        new LauncherConfig { Name = "File Explorer", Chord = "Special+E", Command = "explorer.exe" },
        new LauncherConfig { Name = "Browser", Chord = "Special+B", Command = "https://www.google.com" },
    };
    public List<PinnedAppConfig> PinnedApps { get; set; } = new();
    public string TerminalCommand { get; set; } = "wt.exe";
    public string TerminalArgs { get; set; } = "";
    public AppearanceConfig Appearance { get; set; } = new();
    public LayoutConfig Layout { get; set; } = new();
    public InfiniteConfig Infinite { get; set; } = new();
    public bool HideTaskbar { get; set; } = true;

    public static Dictionary<string, string> DefaultBindings() => new()
    {
        [Actions.ExitEnvironment] = "Special+Escape",
        [Actions.OpenMenu] = "Special+Space",
        [Actions.SwitchMode] = "Special+M",
        [Actions.Overview] = "Special+Tab",
        [Actions.GoHome] = "Special+Home",
        [Actions.OpenTerminal] = "Special+Enter",
        [Actions.CloseWindow] = "Special+Q",
        [Actions.FocusLeft] = "Special+Left",
        [Actions.FocusRight] = "Special+Right",
        [Actions.FocusUp] = "Special+Up",
        [Actions.FocusDown] = "Special+Down",
        [Actions.SwapLeft] = "Special+Shift+Left",
        [Actions.SwapRight] = "Special+Shift+Right",
        [Actions.SwapUp] = "Special+Shift+Up",
        [Actions.SwapDown] = "Special+Shift+Down",
        [Actions.FocusNext] = "Special+J",
        [Actions.FocusPrev] = "Special+K",
        [Actions.PinWindow] = "Special+P",
        [Actions.HibernateWindow] = "Special+H",
        [Actions.ToggleFloat] = "Special+F",
        [Actions.ZoomToMaster] = "Special+Z",
        [Actions.ShrinkMaster] = "Special+Minus",
        [Actions.GrowMaster] = "Special+Plus",
        [Actions.OpenSettings] = "Special+Comma",
        [Actions.QuitApp] = "Special+Shift+Q",
    };

    /// <summary>Fills in any binding missing from an older config file and clamps values.</summary>
    public void Normalize()
    {
        // v1 -> v2: old action names are gone; drop bindings we no longer know.
        var known = new HashSet<string>(Actions.All.Select(a => a.Key));
        foreach (var k in Bindings.Keys.Where(k => !known.Contains(k)).ToList()) Bindings.Remove(k);
        foreach (var (k, v) in DefaultBindings())
            Bindings.TryAdd(k, v);
        Version = 2;
        Layout.MasterRatio = Math.Clamp(Layout.MasterRatio, 0.1, 0.9);
        Layout.MasterCount = Math.Max(1, Layout.MasterCount);
        Layout.Gap = Math.Clamp(Layout.Gap, 0, 100);
        Infinite.NewWindowAreaFraction = Math.Clamp(Infinite.NewWindowAreaFraction, 0.02, 1.0);
        Infinite.CascadeOffset = Math.Clamp(Infinite.CascadeOffset, 0, 400);
        Infinite.EdgePanSpeed = Math.Clamp(Infinite.EdgePanSpeed, 1, 200);
        Infinite.EdgePanMargin = Math.Clamp(Infinite.EdgePanMargin, 0, 50);
        Infinite.ScrollPanStep = Math.Clamp(Infinite.ScrollPanStep, 5, 500);
        Appearance.FocusBorderThickness = Math.Clamp(Appearance.FocusBorderThickness, 1, 12);
    }

    public TallonConfig Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, ConfigStore.JsonOptions);
        return System.Text.Json.JsonSerializer.Deserialize<TallonConfig>(json, ConfigStore.JsonOptions)!;
    }
}
