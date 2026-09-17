using TaLLon.Core.Apps;
using TaLLon.Core.Config;
using TaLLon.Core.Input;
using TaLLon.Core.Layout;

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
        Actions.ExitEnvironment => () => _app.Env.Exit(),
        Actions.OpenMenu => () => _app.Menu.Open(),
        Actions.SwitchMode => () => _app.Env.SwitchMode(),
        Actions.Overview => () => _app.Env.ToggleOverview(),
        Actions.GoHome => () => _app.Env.GoHome(),
        Actions.OpenTerminal => () => AppCatalog.LaunchTerminal(_app.Config),
        Actions.CloseWindow => () => _app.Env.CloseFocused(),
        Actions.FocusLeft => () => _app.Env.FocusDirection(Direction.Left),
        Actions.FocusRight => () => _app.Env.FocusDirection(Direction.Right),
        Actions.FocusUp => () => _app.Env.FocusDirection(Direction.Up),
        Actions.FocusDown => () => _app.Env.FocusDirection(Direction.Down),
        Actions.SwapLeft => () => _app.Env.SwapDirection(Direction.Left),
        Actions.SwapRight => () => _app.Env.SwapDirection(Direction.Right),
        Actions.SwapUp => () => _app.Env.SwapDirection(Direction.Up),
        Actions.SwapDown => () => _app.Env.SwapDirection(Direction.Down),
        Actions.FocusNext => () => _app.Env.FocusNext(+1),
        Actions.FocusPrev => () => _app.Env.FocusNext(-1),
        Actions.PinWindow => () => _app.Env.TogglePin(_app.Env.Focused),
        Actions.HibernateWindow => () => _app.Env.ToggleHibernate(_app.Env.Focused),
        Actions.ToggleFloat => () => _app.Env.ToggleFloatFocused(),
        Actions.ZoomToMaster => () => _app.Env.ZoomToMaster(),
        Actions.ShrinkMaster => () => _app.Env.AdjustMaster(-0.05),
        Actions.GrowMaster => () => _app.Env.AdjustMaster(+0.05),
        Actions.OpenSettings => () => _app.ShowMain(),
        Actions.QuitApp => () => _app.Quit(),
        _ => null,
    };
}
