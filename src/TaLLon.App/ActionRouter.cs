using TaLLon.Core.Apps;
using TaLLon.Core.Config;
using TaLLon.Core.Input;

namespace TaLLon.App;

/// <summary>Maps a pressed <see cref="KeyChord"/> to a named action or a launcher.</summary>
public sealed class ActionRouter
{
    private readonly App _app;
    private readonly Dictionary<KeyChord, Action> _map = new();

    public ActionRouter(App app) { _app = app; }

    public void Rebuild(TallonConfig cfg)
    {
        _map.Clear();
        foreach (var (name, chordText) in cfg.Bindings)
        {
            if (!KeyChord.TryParse(chordText, out var chord)) { Log.Warn($"binding {name}: bad chord '{chordText}'"); continue; }
            var act = Resolve(name);
            if (act != null) _map[chord] = act;
        }
        foreach (var l in cfg.Launchers)
        {
            if (!KeyChord.TryParse(l.Chord, out var chord)) continue;
            var cmd = l.Command; var args = l.Args;
            _map[chord] = () => AppCatalog.LaunchCommand(cmd, args);
        }
    }

    public bool Handle(KeyChord chord)
    {
        if (_map.TryGetValue(chord, out var act))
        {
            try { act(); } catch (Exception ex) { Log.Error($"action {chord} failed: {ex}"); }
            return true;
        }
        Log.Info($"unbound chord {chord}");
        _app.Osd?.Flash($"{chord} — not bound");
        return false;
    }

    private Action? Resolve(string name) => name switch
    {
        Actions.ToggleManager => () => _app.Wm.Toggle(),
        Actions.OpenMenu => () => _app.Menu?.Open(),
        Actions.ToggleTiling => () => _app.Wm.ToggleTiling(),
        Actions.OpenTerminal => () => AppCatalog.LaunchTerminal(_app.Config),
        Actions.CloseWindow => () => _app.Wm.CloseFocused(),
        Actions.FocusNext => () => _app.Wm.FocusNext(+1),
        Actions.FocusPrev => () => _app.Wm.FocusNext(-1),
        Actions.ShrinkMaster => () => _app.Wm.AdjustMaster(-0.05),
        Actions.GrowMaster => () => _app.Wm.AdjustMaster(+0.05),
        Actions.ToggleMonocle => () => _app.Wm.ToggleMonocle(),
        Actions.ToggleFloat => () => _app.Wm.ToggleFloatFocused(),
        Actions.ZoomToMaster => () => _app.Wm.ZoomToMaster(),
        Actions.OpenSettings => () => _app.OpenSettings(),
        Actions.QuitDaemon => () => _app.Quit(),
        _ => null,
    };
}
