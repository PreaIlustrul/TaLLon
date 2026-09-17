using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using TaLLon.Core.Apps;

namespace TaLLon.App.Windows;

/// <summary>Searchable list of every installed program (with icons); also allows picking any file.</summary>
public partial class AppPickerDialog : Window
{
    public AppEntry? Chosen { get; private set; }

    public AppPickerDialog()
    {
        InitializeComponent();
        Search.TextChanged += (_, _) => Refresh();
        List.MouseDoubleClick += (_, _) => Choose_Click(this, new RoutedEventArgs());
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
            else if (e.Key == Key.Enter && List.SelectedItem != null) { Choose_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.Down && Search.IsKeyboardFocused && List.Items.Count > 0) { List.SelectedIndex = Math.Min(List.SelectedIndex + 1, List.Items.Count - 1); List.ScrollIntoView(List.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Up && Search.IsKeyboardFocused && List.SelectedIndex > 0) { List.SelectedIndex--; List.ScrollIntoView(List.SelectedItem); e.Handled = true; }
        };
        Loaded += (_, _) => { Search.Focus(); if (AppCatalog.Apps.Count == 0) _ = AppCatalog.RefreshAsync().ContinueWith(_ => Dispatcher.BeginInvoke(Refresh)); };
        Refresh();
    }

    private void Refresh()
    {
        var app = App.Current;
        var items = new List<MenuEntry>();
        foreach (var a in AppCatalog.Search(Search.Text, 400))
        {
            var entry = new MenuEntry { Title = a.Name, Hint = a.Source == "startmenu" ? "shortcut" : "", IconKey = a.LaunchTarget, Run = () => { } };
            var cached = app.Icons.Get(a.LaunchTarget, bmp => entry.Icon = bmp);
            if (cached != null) entry.Icon = cached;
            items.Add(entry);
        }
        List.ItemsSource = items;
        if (items.Count > 0) List.SelectedIndex = 0;
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not MenuEntry m) return;
        Chosen = new AppEntry(m.Title, m.IconKey, "picker");
        DialogResult = true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd|All files|*.*" };
        if (d.ShowDialog(this) == true)
        {
            Chosen = new AppEntry(Path.GetFileNameWithoutExtension(d.FileName), d.FileName, "file");
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
