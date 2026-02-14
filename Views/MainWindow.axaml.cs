using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GraphicPing.Models;
using GraphicPing.ViewModels;

namespace GraphicPing.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private bool _isDark;

    public MainWindow()
    {
        _vm = new MainWindowViewModel();
        DataContext = _vm;
        InitializeComponent();

        _vm.LogUpdated += () =>
        {
            var scroller = this.FindControl<ScrollViewer>("LogScroller");
            scroller?.ScrollToEnd();
        };

        _vm.ThemeChanged += ApplyTheme;

        _vm.SaveFileRequested += async (title, defaultName) =>
        {
            var sp = StorageProvider;
            var result = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultName,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });
            return result?.Path?.LocalPath;
        };
    }

    private void OnPresetSelected(object? sender, SelectionChangedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("PresetCombo");
        if (combo?.SelectedItem is HostPreset preset)
        {
            _vm.ApplyPreset(preset);
        }
    }

    private void OnSavePreset(object? sender, RoutedEventArgs e)
    {
        // Simple input: use the first host as name, or prompt
        var hosts = _vm.HostsInput.Trim();
        if (string.IsNullOrEmpty(hosts)) return;

        var name = $"Custom ({DateTime.Now:HH:mm})";
        _vm.SaveCurrentAsPreset(name);
    }

    private void OnDeletePreset(object? sender, RoutedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("PresetCombo");
        if (combo?.SelectedItem is HostPreset preset)
        {
            _vm.DeletePreset(preset);
            combo.SelectedIndex = -1;
        }
    }

    private void OnExportCsv(object? sender, RoutedEventArgs e)
    {
        _vm.ExportCsvCommand.Execute().Subscribe();
    }

    private void OnExportLog(object? sender, RoutedEventArgs e)
    {
        _vm.ExportLogCommand.Execute().Subscribe();
    }

    private void OnToggleDarkMode(object? sender, RoutedEventArgs e)
    {
        _isDark = !_isDark;
        _vm.IsDarkMode = _isDark;
    }

    private void ApplyTheme()
    {
        var topPanel = this.FindControl<Border>("TopPanel");
        var graphBorder = this.FindControl<Border>("GraphBorder");
        var logBorder = this.FindControl<Border>("LogBorder");
        var logHeader = this.FindControl<Border>("LogHeader");
        var pingGraph = this.FindControl<GraphicPing.Controls.PingGraphControl>("PingGraph");

        if (_isDark)
        {
            if (topPanel != null) topPanel.Background = new SolidColorBrush(Color.Parse("#2B2B3D"));
            if (graphBorder != null) graphBorder.BorderBrush = new SolidColorBrush(Color.Parse("#555568"));
            if (logBorder != null) logBorder.Background = new SolidColorBrush(Color.Parse("#1E1E2E"));
            if (logHeader != null) logHeader.Background = new SolidColorBrush(Color.Parse("#35354A"));
            if (pingGraph != null) pingGraph.IsDark = true;
            Background = new SolidColorBrush(Color.Parse("#1E1E2E"));
        }
        else
        {
            if (topPanel != null) topPanel.Background = new SolidColorBrush(Color.Parse("#F0F4F8"));
            if (graphBorder != null) graphBorder.BorderBrush = new SolidColorBrush(Color.Parse("#7B9ABF"));
            if (logBorder != null) logBorder.Background = new SolidColorBrush(Color.Parse("#F8F6F2"));
            if (logHeader != null) logHeader.Background = new SolidColorBrush(Color.Parse("#CDDCEB"));
            if (pingGraph != null) pingGraph.IsDark = false;
            Background = new SolidColorBrush(Color.Parse("#FFFFFF"));
        }
    }
}
