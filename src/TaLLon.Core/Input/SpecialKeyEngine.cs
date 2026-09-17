using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.Core.Input;

/// <summary>Shortcuts Windows normally owns that TaLLon takes over while the environment is active.</summary>
public enum SystemShortcut { AltTab, WinTab, WinD, WinTap }

/// <summary>
/// The input brain. Sits on the low-level hooks and:
///  * recognises the special key (Copilot chord LWin+LShift+F23, or any single key),
///  * "tap" (press + release with nothing in between) → <see cref="SpecialTapped"/> (enter/exit),
///  * "hold + key" → <see cref="ChordPressed"/> with a <see cref="KeyChord"/>,
///  * while the environment is active, swallows Alt+Tab / Win+Tab / Win+D / lone Win and raises <see cref="SystemShortcutPressed"/>,
///  * forwards right-clicks on window captions as <see cref="CaptionRightClick"/> (for the window context menu).
/// All callbacks arrive on the hook thread; the app marshals to its UI thread.
/// </summary>
public sealed class SpecialKeyEngine : IDisposable
{
    private readonly HookThread _hooks;
    private readonly object _gate = new();

    private SpecialKeyConfig _special = new();
    private int _specialVk = Win32.VK_F23;

    // Copilot chord deferral (D4, revised): LWin is held back until we know whether F23 follows.
    private readonly List<KeyEvent> _pendingWin = new();
    // keys whose DOWN we swallowed; swallow their UP too
    private readonly HashSet<int> _chordKeys = new();
    // physical modifier keys currently down that the OS also saw (for chord modifiers)
    private readonly HashSet<int> _physMods = new();
    private bool _swallowNextRUp;

    private bool _specialDown;
    private bool _usedWhileHeld;

    /// <summary>Master switch: when false the hooks pass everything through untouched.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Set by the app; enables the Alt+Tab / Win+Tab takeover and the caption right-click.</summary>
    public volatile bool EnvironmentActive;
    /// <summary>App-provided: is this hwnd a managed window (for the caption right-click)?</summary>
    public Func<nint, bool>? IsManagedWindow { get; set; }

    public bool SpecialHeld { get { lock (_gate) return _specialDown; } }

    public event Action<KeyChord>? ChordPressed;
    public event Action? SpecialTapped;
    public event Action<bool>? SpecialHeldChanged;
    public event Action<SystemShortcut>? SystemShortcutPressed;
    public event Action<nint, int, int>? CaptionRightClick;
    /// <summary>Diagnostics: every raw key event (only when <see cref="Trace"/> is on).</summary>
    public event Action<KeyEvent>? RawKey;
    public volatile bool Trace;

    public SpecialKeyEngine()
    {
        _hooks = new HookThread(OnKey, OnMouse);
    }

    public void Apply(TallonConfig cfg)
    {
        lock (_gate)
        {
            _special = cfg.SpecialKey;
            _specialVk = _special.Kind == SpecialKeyKind.Copilot
                ? Win32.VK_F23
                : (KeyNames.TryVk(_special.Key, out var vk) ? vk : 0x14);
        }
    }

    public void Start() => _hooks.Start();
    public void Reinstall() => _hooks.Reinstall();
    public void SetMouseHook(bool on) => _hooks.SetMouseHook(on);

    // -------------------------------------------------------------------------------------

    private bool OnKey(KeyEvent e)
    {
        if (Trace) RawKey?.Invoke(e);
        if (!Enabled) return false;
        if (e.FromTallon) return false;               // our own replays / focus trick
        lock (_gate)
        {
            return e.Up ? OnKeyUp(e) : OnKeyDown(e);
        }
    }

    private bool OnKeyDown(KeyEvent e)
    {
        bool copilot = _special.Kind == SpecialKeyKind.Copilot;

        // Auto-repeat of keys we already swallowed (special chord, chord keys): swallow silently.
        if (_chordKeys.Contains(e.Vk)) return true;

        if (copilot)
        {
            if (_pendingWin.Count > 0)
            {
                if (e.Vk == Win32.VK_LSHIFT) { _pendingWin.Add(e); return true; }
                if (e.Vk == Win32.VK_F23)
                {
                    _pendingWin.Clear();
                    _chordKeys.Add(Win32.VK_LWIN); _chordKeys.Add(Win32.VK_LSHIFT); _chordKeys.Add(Win32.VK_F23);
                    SpecialPressed();
                    return true;
                }
                if (EnvironmentActive && (e.Vk == Win32.VK_TAB || e.Vk == Win32.VK_D))
                {
                    // Win+Tab / Win+D inside the environment: ours.
                    foreach (var p in _pendingWin) _chordKeys.Add(p.Vk);
                    _pendingWin.Clear();
                    _chordKeys.Add(e.Vk);
                    Raise(e.Vk == Win32.VK_TAB ? SystemShortcut.WinTab : SystemShortcut.WinD);
                    return true;
                }
                // A real Win+something: release what we held back, then let this key through.
                ReplayPending();
            }
            else if (e.Vk == Win32.VK_LWIN && !_specialDown)
            {
                _pendingWin.Add(e);
                return true;
            }
            else if (e.Vk == Win32.VK_F23)
            {
                // Bare F23 (keyboard without the Win/Shift prefix, or after a Settings remap): still special.
                _chordKeys.Add(Win32.VK_F23);
                SpecialPressed();
                return true;
            }
        }
        else if (e.Vk == _specialVk)
        {
            _chordKeys.Add(e.Vk);
            SpecialPressed();
            return true;
        }

        if (KeyNames.IsModifierVk(e.Vk))
        {
            _physMods.Add(e.Vk);
            return false;
        }

        if (_specialDown)
        {
            var mods = Mods.Special;
            if (IsMod(Win32.VK_LCONTROL, Win32.VK_RCONTROL, Win32.VK_CONTROL)) mods |= Mods.Ctrl;
            if (IsMod(Win32.VK_LSHIFT, Win32.VK_RSHIFT, Win32.VK_SHIFT)) mods |= Mods.Shift;
            if (IsMod(Win32.VK_LMENU, Win32.VK_RMENU, Win32.VK_MENU)) mods |= Mods.Alt;
            if (IsMod(Win32.VK_LWIN, Win32.VK_RWIN, -1)) mods |= Mods.Win;
            _usedWhileHeld = true;
            _chordKeys.Add(e.Vk);
            var chord = new KeyChord(mods, e.Vk);
            Log.Info("chord " + chord);
            ThreadPool.QueueUserWorkItem(_ => ChordPressed?.Invoke(chord));
            return true;
        }

        if (EnvironmentActive && e.Vk == Win32.VK_TAB && IsMod(Win32.VK_LMENU, Win32.VK_RMENU, Win32.VK_MENU))
        {
            // Alt+Tab inside the environment → overview. Mask the lone-Alt so menu bars don't activate.
            _chordKeys.Add(e.Vk);
            Win32.SendKey(Win32.VK_CONTROL, false); Win32.SendKey(Win32.VK_CONTROL, true);
            Raise(SystemShortcut.AltTab);
            return true;
        }
        return false;
    }

    private bool OnKeyUp(KeyEvent e)
    {
        if (_pendingWin.Count > 0 && (e.Vk == Win32.VK_LWIN || e.Vk == Win32.VK_LSHIFT))
        {
            // Win (or Win+Shift) tapped with nothing else: normally that's the Start menu.
            if (EnvironmentActive)
            {
                _pendingWin.RemoveAll(p => p.Vk == e.Vk);
                if (e.Vk == Win32.VK_LWIN) { _pendingWin.Clear(); Raise(SystemShortcut.WinTap); }
                return true;
            }
            ReplayPending();
            return false;
        }
        if (_chordKeys.Remove(e.Vk))
        {
            bool isSpecial = _special.Kind == SpecialKeyKind.Copilot ? e.Vk == Win32.VK_F23 : e.Vk == _specialVk;
            if (isSpecial) SpecialReleased();
            return true;
        }
        _physMods.Remove(e.Vk);
        return false;
    }

    private bool IsMod(int l, int r, int generic) =>
        _physMods.Contains(l) || _physMods.Contains(r) || (generic >= 0 && _physMods.Contains(generic));

    private void SpecialPressed()
    {
        if (_specialDown) return;                      // auto-repeat
        _specialDown = true;
        _usedWhileHeld = false;
        SpecialHeldChanged?.Invoke(true);
    }

    private void SpecialReleased()
    {
        _specialDown = false;
        SpecialHeldChanged?.Invoke(false);
        if (!_usedWhileHeld) ThreadPool.QueueUserWorkItem(_ => SpecialTapped?.Invoke());
    }

    private void Raise(SystemShortcut s) => ThreadPool.QueueUserWorkItem(_ => SystemShortcutPressed?.Invoke(s));

    private void ReplayPending()
    {
        if (_pendingWin.Count == 0) return;
        var copy = _pendingWin.ToArray();
        _pendingWin.Clear();
        foreach (var k in copy) { Win32.SendKey(k.Vk, false, k.ScanCode); _physMods.Add(k.Vk); }
    }

    // ---- mouse ---------------------------------------------------------------------------

    private bool OnMouse(MouseEvent m)
    {
        if (!Enabled || !EnvironmentActive || m.Injected) return false;
        if (m.Msg == Win32.WM_RBUTTONUP && _swallowNextRUp) { _swallowNextRUp = false; return true; }
        if (m.Msg != Win32.WM_RBUTTONDOWN) return false;

        var hwnd = Win32.WindowFromPoint(new Win32.POINT { X = m.X, Y = m.Y });
        if (hwnd == 0) return false;
        var root = Win32.GetAncestor(hwnd, Win32.GA_ROOT);
        if (root == 0 || IsManagedWindow?.Invoke(root) != true) return false;
        if (Win32.HitTest(root, m.X, m.Y) != Win32.HTCAPTION) return false;

        _swallowNextRUp = true;
        var (x, y) = (m.X, m.Y);
        ThreadPool.QueueUserWorkItem(_ => CaptionRightClick?.Invoke(root, x, y));
        return true;
    }

    // ---- scripting -----------------------------------------------------------------------

    /// <summary>
    /// Injects a chord as if typed: the special key as the Copilot sequence (or the configured key),
    /// then the command key. Used by `TaLLon.exe --send Special+W` so scripts can drive TaLLon.
    /// A chord with no key ("Special") is a tap. Untagged on purpose, so a running instance's hook
    /// treats it like real input.
    /// </summary>
    public static void InjectChord(KeyChord chord, SpecialKeyConfig special)
    {
        var specialSeq = special.Kind == SpecialKeyKind.Copilot
            ? new[] { Win32.VK_LWIN, Win32.VK_LSHIFT, Win32.VK_F23 }
            : new[] { KeyNames.TryVk(special.Key, out var v) ? v : 0x14 };
        var mods = new List<int>();
        if (chord.Mods.HasFlag(Mods.Ctrl)) mods.Add(Win32.VK_CONTROL);
        if (chord.Mods.HasFlag(Mods.Shift)) mods.Add(Win32.VK_SHIFT);
        if (chord.Mods.HasFlag(Mods.Alt)) mods.Add(Win32.VK_MENU);
        if (chord.Mods.HasFlag(Mods.Win)) mods.Add(Win32.VK_LWIN);

        if (chord.Mods.HasFlag(Mods.Special)) foreach (var k in specialSeq) Win32.SendKey(k, false, tagged: false);
        Thread.Sleep(60);
        if (chord.Vk != 0)
        {
            foreach (var m in mods) Win32.SendKey(m, false, tagged: false);
            Win32.SendKey(chord.Vk, false, tagged: false);
            Thread.Sleep(30);
            Win32.SendKey(chord.Vk, true, tagged: false);
            foreach (var m in Enumerable.Reverse(mods)) Win32.SendKey(m, true, tagged: false);
            Thread.Sleep(30);
        }
        if (chord.Mods.HasFlag(Mods.Special)) foreach (var k in Enumerable.Reverse(specialSeq)) Win32.SendKey(k, true, tagged: false);
    }

    public void Dispose() => _hooks.Dispose();
}
