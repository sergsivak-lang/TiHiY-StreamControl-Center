using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Data;
using Microsoft.Win32;
using TiHiY.StreamControlCenter.Services;

namespace TiHiY.StreamControlCenter.Windows;

public partial class JournalWindow : ModuleWindowBase
{
    private readonly AppServices _services = App.Services;
    private readonly ICollectionView _view;

    public JournalWindow()
    {
        InitializeComponent();
        ConfigureModule(DesignSurface, 1100, 720, "JournalWindow");
        _view = CollectionViewSource.GetDefaultView(_services.Logger.Entries);
        _view.Filter = FilterEntry;
        EntriesList.ItemsSource = _view;
        _services.Logger.Entries.CollectionChanged += Entries_CollectionChanged;
        Closed += (_, _) => _services.Logger.Entries.CollectionChanged -= Entries_CollectionChanged;
        UpdateCount();
        Loaded += (_, _) => ScrollToLast();
    }

    private bool FilterEntry(object item)
    {
        var query = SearchBox.Text.Trim();
        return string.IsNullOrWhiteSpace(query) ||
               item?.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view.Refresh();
        UpdateCount();
    }

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatcher.BeginInvoke(new Action(() =>
    {
        _view.Refresh();
        UpdateCount();
        ScrollToLast();
    }));

    private void UpdateCount() => CountText.Text = $"{EntriesList.Items.Count} записів";

    private void ScrollToLast()
    {
        if (EntriesList.Items.Count > 0)
            EntriesList.ScrollIntoView(EntriesList.Items[EntriesList.Items.Count - 1]);
    }

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        var lines = EntriesList.SelectedItems.Cast<object>().Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x));
        var text = string.Join(Environment.NewLine, lines!);
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusText.Text = "Спочатку виберіть один або кілька записів.";
            StatusText.Foreground = (Brush)FindResource("Yellow");
            return;
        }
        Clipboard.SetText(text);
        StatusText.Text = "Вибрані записи скопійовано.";
        StatusText.Foreground = (Brush)FindResource("Green");
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join(Environment.NewLine, EntriesList.Items.Cast<object>().Select(x => x.ToString()));
        if (!string.IsNullOrWhiteSpace(text)) Clipboard.SetText(text);
        StatusText.Text = "Увесь видимий журнал скопійовано.";
        StatusText.Foreground = (Brush)FindResource("Green");
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Експорт журналу TiHiY",
            Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
            FileName = $"TiHiY-StreamControl-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
        };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllLines(dialog.FileName, EntriesList.Items.Cast<object>().Select(x => x.ToString() ?? string.Empty));
        StatusText.Text = $"Журнал експортовано: {dialog.FileName}";
        StatusText.Foreground = (Brush)FindResource("Green");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(_services.Logger.Folder) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            _services.Logger.Error("Відкриття папки журналу", ex);
            StatusText.Text = ex.Message;
            StatusText.Foreground = (Brush)FindResource("Red");
        }
    }

    private void ClearView_Click(object sender, RoutedEventArgs e)
    {
        _services.Logger.Entries.Clear();
        StatusText.Text = "Вікно журналу очищено. Файл журналу на диску не видалено.";
        StatusText.Foreground = (Brush)FindResource("Yellow");
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragTitle(sender, e);
    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeWindow(sender, e);
    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeWindow(sender, e);
    private void Close_Click(object sender, RoutedEventArgs e) => CloseWindow(sender, e);
}
