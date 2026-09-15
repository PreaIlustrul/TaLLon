namespace TaLLon.Core.Input;

[Flags]
public enum Mods
{
    None = 0,
    Special = 1,
    Ctrl = 2,
    Shift = 4,
    Alt = 8,
    Win = 16,
}

/// <summary>A key combination such as "Special+Shift+Enter". Immutable, comparable, string round-trips.</summary>
public readonly record struct KeyChord(Mods Mods, int Vk)
{
    public bool IsEmpty => Vk == 0 && Mods == Mods.None;

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Mods.HasFlag(Mods.Special)) parts.Add("Special");
        if (Mods.HasFlag(Mods.Ctrl)) parts.Add("Ctrl");
        if (Mods.HasFlag(Mods.Shift)) parts.Add("Shift");
        if (Mods.HasFlag(Mods.Alt)) parts.Add("Alt");
        if (Mods.HasFlag(Mods.Win)) parts.Add("Win");
        if (Vk != 0) parts.Add(KeyNames.NameOf(Vk));
        return string.Join("+", parts);
    }

    public static bool TryParse(string? text, out KeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var mods = Mods.None;
        int vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "special": case "mod": case "copilot": mods |= Mods.Special; break;
                case "ctrl": case "control": mods |= Mods.Ctrl; break;
                case "shift": mods |= Mods.Shift; break;
                case "alt": mods |= Mods.Alt; break;
                case "win": case "super": case "meta": mods |= Mods.Win; break;
                default:
                    if (vk != 0) return false; // two non-modifier keys
                    if (!KeyNames.TryVk(raw, out vk)) return false;
                    break;
            }
        }
        if (vk == 0) return false;
        chord = new KeyChord(mods, vk);
        return true;
    }

    public static KeyChord Parse(string text) =>
        TryParse(text, out var c) ? c : throw new FormatException($"Bad key chord: '{text}'");
}

/// <summary>Human names for virtual-key codes (WinForms-free so Core stays UI-agnostic).</summary>
public static class KeyNames
{
    private static readonly Dictionary<string, int> ByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, string> ByVk = new();

    static KeyNames()
    {
        void Add(string name, int vk, params string[] aliases)
        {
            ByName[name] = vk;
            if (!ByVk.ContainsKey(vk)) ByVk[vk] = name;
            foreach (var a in aliases) ByName[a] = vk;
        }

        for (int i = 0; i < 26; i++) Add(((char)('A' + i)).ToString(), 0x41 + i);
        for (int i = 0; i < 10; i++) Add(((char)('0' + i)).ToString(), 0x30 + i, "D" + i);
        for (int i = 1; i <= 24; i++) Add("F" + i, 0x6F + i);
        for (int i = 0; i < 10; i++) Add("Numpad" + i, 0x60 + i, "NumPad" + i);

        Add("Space", 0x20); Add("Enter", 0x0D, "Return"); Add("Tab", 0x09); Add("Escape", 0x1B, "Esc");
        Add("Backspace", 0x08, "Back"); Add("Delete", 0x2E, "Del"); Add("Insert", 0x2D, "Ins");
        Add("Home", 0x24); Add("End", 0x23); Add("PageUp", 0x21, "Prior"); Add("PageDown", 0x22, "Next");
        Add("Left", 0x25); Add("Up", 0x26); Add("Right", 0x27); Add("Down", 0x28);
        Add("CapsLock", 0x14, "Capital"); Add("NumLock", 0x90); Add("ScrollLock", 0x91, "Scroll");
        Add("Pause", 0x13); Add("PrintScreen", 0x2C, "Snapshot"); Add("Apps", 0x5D, "Menu", "ContextMenu");
        Add("LWin", 0x5B); Add("RWin", 0x5C);
        Add("LShift", 0xA0); Add("RShift", 0xA1); Add("LCtrl", 0xA2); Add("RCtrl", 0xA3);
        Add("LAlt", 0xA4); Add("RAlt", 0xA5, "AltGr");
        Add("Comma", 0xBC, "Oem,", "OemComma", ","); Add("Period", 0xBE, "OemPeriod", ".");
        Add("Minus", 0xBD, "OemMinus", "-"); Add("Plus", 0xBB, "Oemplus", "Equals", "=");
        Add("Semicolon", 0xBA, "Oem1", ";"); Add("Slash", 0xBF, "Oem2", "/", "Question");
        Add("Backtick", 0xC0, "Oem3", "Tilde", "`"); Add("LBracket", 0xDB, "Oem4", "[");
        Add("Backslash", 0xDC, "Oem5", "\\"); Add("RBracket", 0xDD, "Oem6", "]");
        Add("Quote", 0xDE, "Oem7", "'"); Add("Oem102", 0xE2, "OemBackslash");
        Add("Multiply", 0x6A); Add("Add", 0x6B); Add("Subtract", 0x6D); Add("Decimal", 0x6E); Add("Divide", 0x6F);
        Add("VolumeMute", 0xAD); Add("VolumeDown", 0xAE); Add("VolumeUp", 0xAF);
        Add("MediaNext", 0xB0); Add("MediaPrev", 0xB1); Add("MediaStop", 0xB2); Add("MediaPlayPause", 0xB3);
        Add("BrowserHome", 0xAC); Add("BrowserSearch", 0xAA);
    }

    public static bool TryVk(string name, out int vk)
    {
        if (ByName.TryGetValue(name, out vk)) return true;
        if (name.StartsWith("VK_0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(name.AsSpan(5), System.Globalization.NumberStyles.HexNumber, null, out vk)) return true;
        if (name.StartsWith("VK", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(2), out vk)) return true;
        vk = 0;
        return false;
    }

    public static string NameOf(int vk) => ByVk.TryGetValue(vk, out var n) ? n : $"VK_0x{vk:X2}";

    public static bool IsModifierVk(int vk) => vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1
        or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    /// <summary>Keys that make sense as a stand-alone special/leader key.</summary>
    public static readonly string[] SpecialKeyChoices =
    {
        "CapsLock", "RAlt", "RCtrl", "RWin", "Apps", "ScrollLock", "Pause", "Backtick",
        "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21", "F22", "F23", "F24",
    };
}
