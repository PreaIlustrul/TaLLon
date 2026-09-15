using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaLLon.Core.Input;

namespace TaLLon.App.Windows;

/// <summary>
/// Click, then press a key combination; shows it as text. A "Special" toggle sits to the left
/// because the special key itself is swallowed by the daemon and can't be typed here.
/// </summary>
public sealed class KeyChordBox : Grid
{
    private readonly ToggleButton _special = new() { Content = "Special", Width = 68, Margin = "0,0,6,0".ToThickness() };
    private readonly TextBox _box = new() { IsReadOnly = true, Cursor = Cursors.Hand, MinWidth = 150 };
    private readonly Button _clear = new() { Content = "✕", Width = 30, Margin = "6,0,0,0".ToThickness(), Padding = new Thickness(0) };
    private int _vk;
    private Mods _mods;

    public event Action? ChordChanged;

    public KeyChordBox()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_special, 0); SetColumn(_box, 1); SetColumn(_clear, 2);
        Children.Add(_special); Children.Add(_box); Children.Add(_clear);

        _special.Checked += (_, _) => { _mods |= Mods.Special; Render(); };
        _special.Unchecked += (_, _) => { _mods &= ~Mods.Special; Render(); };
        _box.GotKeyboardFocus += (_, _) => { _box.Text = "press keys…"; _box.Background = Brushes.Black; };
        _box.LostKeyboardFocus += (_, _) => { Render(); _box.ClearValue(TextBox.BackgroundProperty); };
        _box.PreviewKeyDown += OnKeyDown;
        _clear.Click += (_, _) => { _vk = 0; _mods &= Mods.Special; Render(); };
    }

    public string Chord
    {
        get => _vk == 0 ? "" : new KeyChord(_mods, _vk).ToString();
        set
        {
            if (KeyChord.TryParse(value, out var c)) { _vk = c.Vk; _mods = c.Mods; }
            else { _vk = 0; _mods = Mods.None; }
            _special.IsChecked = _mods.HasFlag(Mods.Special);
            Render();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return; // wait for the real key
        if (key == Key.Escape) { Keyboard.ClearFocus(); return; }

        var mods = _mods & Mods.Special;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= Mods.Ctrl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= Mods.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= Mods.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= Mods.Win;
        _mods = mods;
        _vk = KeyInterop.VirtualKeyFromKey(key);
        Render();
        Keyboard.ClearFocus();
        ChordChanged?.Invoke();
    }

    private void Render() => _box.Text = _vk == 0 ? "(unassigned)" : new KeyChord(_mods, _vk).ToString();
}

internal static class ThicknessExt
{
    public static Thickness ToThickness(this string s)
    {
        var p = s.Split(',').Select(double.Parse).ToArray();
        return new Thickness(p[0], p[1], p[2], p[3]);
    }
}

/// <summary>Minimal themed toggle.</summary>
public sealed class ToggleButton : System.Windows.Controls.Primitives.ToggleButton
{
    public ToggleButton()
    {
        var t = new ControlTemplate(typeof(ToggleButton));
        var bd = new FrameworkElementFactory(typeof(Border), "bd");
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        bd.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        bd.AppendChild(cp);
        t.VisualTree = bd;

        // Defaults live in a Style so the IsChecked trigger can override them
        // (a local Background value would outrank any trigger).
        var style = new Style(typeof(ToggleButton));
        style.Setters.Add(new Setter(TemplateProperty, t));
        style.Setters.Add(new Setter(ForegroundProperty, Brushes.White));
        style.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x35, 0x35, 0x35))));
        style.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x3f, 0x3f, 0x3f))));
        style.Setters.Add(new Setter(PaddingProperty, new Thickness(6, 4, 6, 4)));
        var on = new Trigger { Property = IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2f, 0x6e, 0xaf))));
        on.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x4c, 0x9b, 0xe8))));
        style.Triggers.Add(on);
        Style = style;
    }
}
