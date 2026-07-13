using System.Collections.Specialized;
using System.Diagnostics;

namespace TiHiY.StreamControlCenter.Windows;

public partial class JournalWindow : Services.ModuleWindowBase
{
    private readonly ObservableCollection<string> _visibleEntries = new();
    public ObservableCollection<string> VisibleEntries => _visibleEntries;

    public JournalWindow()
    {
        InitializeComponent();
        DataContext = this;
        ConfigureModule(DesignSurface, 1180, 760, nameof(JournalWindow));
        App.Services.Logger.Entries.CollectionChanged += Entries_CollectionChanged;
        Closed += (_, _) => App.Services.Logger.Entries.CollectionChanged -= Entries_CollectionChanged;
        Rebuild();
    }

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(() =>
    {
        Rebuild();
        if (LogList.Items.Count > 0) LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
    }));

    private void Rebuild()
    {
        var filter = SearchBox?.Text?.Trim() ?? string.Empty;
        _visibleEntries.Clear();
        foreach (var line in App.Services.Logger.Entries)
        {
            if (filter.Length == 0 || line.Contains(filter, StringComparison.OrdinalIgnoreCase))
                _visibleEntries.Add(line);
        }
        if (StatusText is not null) StatusText.Text = $"Показано {_visibleEntries.Count} з {App.Services.Logger.Entries.Count} подій";
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => Rebuild();
    private void ResetFilter_Click(object sender, RoutedEventArgs e) { SearchBox.Clear(); Rebuild(); }
    private void ClearView_Click(object sender, RoutedEventArgs e) { _visibleEntries.Clear(); StatusText.Text = "Поточний перегляд очищено. Файл журналу не видалено."; }

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        var lines = LogList.SelectedItems.Cast<string>().ToArray();
        if (lines.Length == 0) lines = _visibleEntries.ToArray();
        if (lines.Length == 0) return;
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
        StatusText.Text = $"Скопійовано рядків: {lines.Length}";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(App.Services.Logger.Folder) { UseShellExecute = true }); }
        catch (Exception ex) { App.Services.Logger.Error("Відкриття папки журналу", ex); }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
