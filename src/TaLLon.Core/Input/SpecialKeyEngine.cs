using TaLLon.Core.Config;
using TaLLon.Core.Native;

namespace TaLLon.Core.Input;

public enum SpecialState { Idle, Held, Leader }

/// <summary>
/// The input brain. Sits on the low-level keyboard hook and:
///  * recognises the special key (Copilot chord LWin+LShift+F23, or any single key),
///  * supports "hold" (modifier style) and "leader" (tap, then next key) usage,
///  * turns key presses while special is active into <see cref="KeyChord"/>s and raises <see cref="ChordPressed"/>.
/// All callbacks arrive on the hook thread; the app marshals to its UI thread.
/// </summary>
public sealed class SpecialKeyEngine : IDisposable
{
    private readonly KeyboardHook _hook;
    private readonly object _gate = new();

    private SpecialKeyConfig _special = new();
    private int _specialVk = Win32.VK_F23;
    private int _leaderTimeoutMs = 2500;
    private int _tapThresholdMs = 400;

    // copilot chord deferral (D4)
    private readonly List<KeyEvent> _pending = new();
    private System.Threading.Timer? _pendingTimer;
    private const int PendingWindowMs = 35;

    // keys whose DOWN we swallowed as part of the special chord; swallow their UP too
    private readonly HashSet<int> _chordKeys = new();

    private bool _specialDown;
    private bool _usedWhileHeld;
    private DateTime _specialDownAt;
    private bool _leader;
    private System.Threading.Timer? _leaderTimer;

    /// <summary>Master switch: when false the hook passes everything through untouched.</summary>
    public bool Enabled { get; set; } = true;

    public SpecialState State
    {
        get { lock (_gate) return _specialDown ? SpecialState.Held : _leader ? SpecialState.Leader : SpecialState.Idle; }
    }

    public event Action<KeyChord>? ChordPressed;
    public event Action<SpecialState>? StateChanged;
    /// <summary>Diagnostics: every raw key event (only when <see cref="Trace"/> is on).</summary>
    public event Action<KeyEvent>? RawKey;
    public bool Trace { get; set; }

    public SpecialKeyEngine()
    {
        _hook = new KeyboardHook(OnKey);
    }

    public void Apply(TallonConfig cfg)
    {
        lock (_gate)
        {
            _special = cfg.SpecialKey;
            _leaderTimeoutMs = Math.Max(300, cfg.LeaderTimeoutMs);
            _tapThresholdMs = Math.Max(50, cfg.TapThresholdMs);
            _specialVk = _special.Kind == SpecialKeyKind.Copilot
                ? Win32.VK_F23
                : (KeyNames.TryVk(_special.Key, out var vk) ? vk : 0x14);
        }
    }

    public void Start() => _hook.Install();

    // -------------------------------------------------------------------------------------

    private bool OnKey(KeyEvent e)
    {
        if (Trace) RawKey?.Invoke(e);
        if (!Enabled) return false;
        if (e.FromTallon) return false;               // our own replays / ALT trick

        lock (_gate)
        {
            return e.Up ? OnKeyUp(e) : OnKeyDown(e);
        }
    }

    private bool OnKeyDown(KeyEvent e)
    {
        bool copilot = _special.Kind == SpecialKeyKind.Copilot;

        // Auto-repeat of the chord keys while holding the Copilot key: swallow silently.
        if (_specialDown && _chordKeys.Contains(e.Vk)) return true;

        if (copilot)
        {
            if (_pending.Count > 0)
            {
                if (e.Vk == Win32.VK_LSHIFT) { _pending.Add(e); return true; }
                if (e.Vk == Win32.VK_F23)
                {
                    // Chord complete: LWin, LShift, F23 -> Special down.
                    CancelPending(replay: false);
                    _chordKeys.Add(Win32.VK_LWIN); _chordKeys.Add(Win32.VK_LSHIFT); _chordKeys.Add(Win32.VK_F23);
                    SpecialPressed();
                    return true;
                }
                // Something else: the user really pressed Win (+key). Replay what we held back.
                CancelPending(replay: true);
                // fall through and treat this key normally
            }
            else if (e.Vk == Win32.VK_LWIN && !_specialDown && !_leader)
            {
                _pending.Add(e);
                _pendingTimer?.Dispose();
                _pendingTimer = new System.Threading.Timer(_ => { lock (_gate) CancelPending(replay: true); },
                    null, PendingWindowMs, Timeout.Infinite);
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

        if (!_specialDown && !_leader) return false;

        // A key while special is active -> chord attempt.
        if (KeyNames.IsModifierVk(e.Vk)) return false;   // let Shift/Ctrl/Alt through; we read them below

        var mods = Mods.Special;
        if (IsDown(Win32.VK_CONTROL)) mods |= Mods.Ctrl;
        if (IsDown(Win32.VK_SHIFT)) mods |= Mods.Shift;
        if (IsDown(Win32.VK_MENU)) mods |= Mods.Alt;
        if (IsDown(Win32.VK_LWIN) || IsDown(Win32.VK_RWIN)) mods |= Mods.Win;

        _usedWhileHeld = true;
        if (_leader) SetLeader(false);
        var chord = new KeyChord(mods, e.Vk);
        _chordKeys.Add(e.Vk);   // swallow its key-up as well
        Log.Info("chord " + chord);
        ThreadPool.QueueUserWorkItem(_ => ChordPressed?.Invoke(chord));
        return true;
    }

    private bool OnKeyUp(KeyEvent e)
    {
        if (_pending.Count > 0)
        {
            // Win tapped faster than our window: replay down, let the up through.
            CancelPending(replay: true);
            return false;
        }
        if (_chordKeys.Remove(e.Vk))
        {
            bool isSpecial = _special.Kind == SpecialKeyKind.Copilot ? e.Vk == Win32.VK_F23 : e.Vk == _specialVk;
            if (isSpecial) SpecialReleased();
            return true;
        }
        return false;
    }

    private void SpecialPressed()
    {
        if (_specialDown) return;                      // auto-repeat
        if (_leader) { SetLeader(false); }             // second tap cancels leader
        _specialDown = true;
        _usedWhileHeld = false;
        _specialDownAt = DateTime.UtcNow;
        StateChanged?.Invoke(SpecialState.Held);
    }

    private void SpecialReleased()
    {
        _specialDown = false;
        var held = (DateTime.UtcNow - _specialDownAt).TotalMilliseconds;
        if (!_usedWhileHeld && held < _tapThresholdMs)
            SetLeader(true);
        else
            StateChanged?.Invoke(SpecialState.Idle);
    }

    private void SetLeader(bool on)
    {
        _leader = on;
        _leaderTimer?.Dispose();
        _leaderTimer = null;
        if (on)
        {
            _leaderTimer = new System.Threading.Timer(_ =>
            {
                lock (_gate) { if (_leader) { _leader = false; StateChanged?.Invoke(SpecialState.Idle); } }
            }, null, _leaderTimeoutMs, Timeout.Infinite);
        }
        StateChanged?.Invoke(on ? SpecialState.Leader : (_specialDown ? SpecialState.Held : SpecialState.Idle));
    }

    /// <summary>
    /// Injects a chord as if typed: the special key as the Copilot sequence (or the configured key),
    /// then the command key. Used by `TaLLon.exe --send Special+W` so scripts can drive TaLLon.
    /// Untagged on purpose, so a running daemon's hook treats it like real input.
    /// </summary>
    public static void InjectChord(KeyChord chord, SpecialKeyConfig special, bool leaderStyle = false)
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
        if (leaderStyle && chord.Mods.HasFlag(Mods.Special))
        {
            // Tap: release the special key first, then press the command key a bit later.
            foreach (var k in Enumerable.Reverse(specialSeq)) Win32.SendKey(k, true, tagged: false);
            Thread.Sleep(400);
        }
        foreach (var m in mods) Win32.SendKey(m, false, tagged: false);
        Win32.SendKey(chord.Vk, false, tagged: false);
        Thread.Sleep(30);
        Win32.SendKey(chord.Vk, true, tagged: false);
        foreach (var m in Enumerable.Reverse(mods)) Win32.SendKey(m, true, tagged: false);
        Thread.Sleep(30);
        if (!leaderStyle && chord.Mods.HasFlag(Mods.Special))
            foreach (var k in Enumerable.Reverse(specialSeq)) Win32.SendKey(k, true, tagged: false);
    }

    /// <summary>Public escape hatch (e.g. the menu window closing): drop leader mode.</summary>
    public void CancelLeader() { lock (_gate) if (_leader) SetLeader(false); }

    private void CancelPending(bool replay)
    {
        _pendingTimer?.Dispose();
        _pendingTimer = null;
        if (_pending.Count == 0) return;
        var copy = _pending.ToArray();
        _pending.Clear();
        if (!replay) return;
        foreach (var k in copy) Win32.SendKey(k.Vk, false, k.ScanCode);
    }

    private static bool IsDown(int vk) => (Win32.GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        _hook.Dispose();
        _pendingTimer?.Dispose();
        _leaderTimer?.Dispose();
    }
}
