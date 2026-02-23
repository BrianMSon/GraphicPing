using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using GraphicPing.Models;
using GraphicPing.ViewModels;

namespace GraphicPing.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private bool _isDark = true;

    public MainWindow()
    {
        _vm = new MainWindowViewModel();
        DataContext = _vm;
        InitializeComponent();

        _vm.LogUpdated += () =>
        {
            var scroller = this.FindControl<ScrollViewer>("LogScroller");
            if (scroller == null) return;
            var atBottom = scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 20;
            if (atBottom) scroller.ScrollToEnd();
        };

        _vm.ThemeChanged += ApplyTheme;
        _vm.IsDarkMode = true;

        // Force NumericUpDown to clamp typed values on LostFocus
        SetupNumericClamp("IntervalInput", 500, 60000);
        SetupNumericClamp("TimeoutInput", 500, 30000);
        SetupNumericClamp("MaxChartInput", 50, 10000);

        var toggleBtn = this.FindControl<Button>("ToggleStartStopBtn")!;
        toggleBtn.Content = "▶ Start (F5)";
        var pauseBtn = this.FindControl<Button>("PauseResumeBtn")!;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.IsPinging))
                toggleBtn.Content = _vm.IsPinging ? "■ Stop (Esc)" : "▶ Start (F5)";
            if (e.PropertyName == nameof(_vm.IsPaused))
            {
                var color = _vm.IsPaused ? "#E53935" : (_isDark ? "#CDD6F4" : "#1E3A5F");
                pauseBtn.Content = new TextBlock
                {
                    Text = _vm.IsPaused ? "▶" : "⏸",
                    Foreground = new SolidColorBrush(Color.Parse(color)),
                    FontSize = 14
                };
            }
        };

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

    private async void OnSavePreset(object? sender, RoutedEventArgs e)
    {
        var hosts = _vm.HostsInput.Trim();
        if (string.IsNullOrEmpty(hosts)) return;

        var combo = this.FindControl<ComboBox>("PresetCombo");
        var selected = combo?.SelectedItem as HostPreset;
        var currentName = selected?.Name ?? $"Custom ({DateTime.Now:HH:mm})";

        var name = await MessageBox.ShowInput(this, "Preset name:", "Save Preset",
            currentName, $"Custom ({DateTime.Now:HH:mm})");
        if (!string.IsNullOrEmpty(name))
        {
            _vm.SaveCurrentAsPreset(name);
            if (combo != null)
                combo.SelectedItem = _vm.Presets.FirstOrDefault(p => p.Name == name);
        }
    }

    private async void OnDeletePreset(object? sender, RoutedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("PresetCombo");
        if (combo?.SelectedItem is HostPreset preset)
        {
            var result = await MessageBox.Show(this, $"Delete preset \"{preset.Name}\"?", "Confirm",
                MessageBox.MessageBoxButtons.YesNo);
            if (result == MessageBox.MessageBoxResult.Yes)
            {
                _vm.DeletePreset(preset);
                combo.SelectedIndex = -1;
            }
        }
    }

    private void OnLogKeyDown(object? sender, KeyEventArgs e)
    {
        var scroller = this.FindControl<ScrollViewer>("LogScroller");
        if (scroller == null) return;
        if (e.Key == Key.Home) { scroller.ScrollToHome(); e.Handled = true; }
        else if (e.Key == Key.End) { scroller.ScrollToEnd(); e.Handled = true; }
    }

    private void OnExportCsv(object? sender, RoutedEventArgs e)
    {
        _vm.ExportCsvCommand.Execute().Subscribe();
    }

    private void OnExportLog(object? sender, RoutedEventArgs e)
    {
        _vm.ExportLogCommand.Execute().Subscribe();
    }

    private async void OnClearData(object? sender, RoutedEventArgs e)
    {
        var result = await MessageBox.Show(this, "Clear all data?", "Confirm",
            MessageBox.MessageBoxButtons.YesNo);
        if (result == MessageBox.MessageBoxResult.Yes)
            _vm.ClearData();
    }

    private void OnToggleDarkMode(object? sender, RoutedEventArgs e)
    {
        _isDark = !_isDark;
        _vm.IsDarkMode = _isDark;
    }

    private void ApplyTheme()
    {
        // FluentTheme handles standard controls (Button, TextBox, ComboBox, NumericUpDown, CheckBox)
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = _isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        var topPanel = this.FindControl<Border>("TopPanel");
        var graphBorder = this.FindControl<Border>("GraphBorder");
        var logBorder = this.FindControl<Border>("LogBorder");
        var logHeader = this.FindControl<Border>("LogHeader");
        var pingGraph = this.FindControl<GraphicPing.Controls.PingGraphControl>("PingGraph");
        var statsBorder = this.FindControl<Border>("StatsBorder");
        var splitter = this.FindControl<GridSplitter>("MainSplitter");
        var logText = this.FindControl<SelectableTextBlock>("LogText");

        if (_isDark)
        {
            if (topPanel != null) topPanel.Background = new SolidColorBrush(Color.Parse("#1E2030"));
            if (graphBorder != null) graphBorder.BorderBrush = new SolidColorBrush(Color.Parse("#3B3F5C"));
            if (logBorder != null) logBorder.Background = new SolidColorBrush(Color.Parse("#191A2A"));
            if (logHeader != null) logHeader.Background = new SolidColorBrush(Color.Parse("#252738"));
            if (statsBorder != null) statsBorder.Background = new SolidColorBrush(Color.Parse("#232538"));
            if (splitter != null) splitter.Background = new SolidColorBrush(Color.Parse("#3B3F5C"));
            if (pingGraph != null) pingGraph.IsDark = true;
            if (logText != null) logText.Foreground = new SolidColorBrush(Color.Parse("#CDD6F4"));
            Background = new SolidColorBrush(Color.Parse("#181926"));

            SetLabelColors(topPanel, "#CDD6F4", "#8B8FA8");
            SetLabelColors(logBorder, "#CDD6F4", "#8B8FA8");
        }
        else
        {
            if (topPanel != null) topPanel.Background = new SolidColorBrush(Color.Parse("#F0F4F8"));
            if (graphBorder != null) graphBorder.BorderBrush = new SolidColorBrush(Color.Parse("#7B9ABF"));
            if (logBorder != null) logBorder.Background = new SolidColorBrush(Color.Parse("#F8F6F2"));
            if (logHeader != null) logHeader.Background = new SolidColorBrush(Color.Parse("#CDDCEB"));
            if (statsBorder != null) statsBorder.Background = new SolidColorBrush(Color.Parse("#E3ECF4"));
            if (splitter != null) splitter.Background = new SolidColorBrush(Color.Parse("#7B9ABF"));
            if (pingGraph != null) pingGraph.IsDark = false;
            if (logText != null) logText.Foreground = new SolidColorBrush(Color.Parse("#1E3A5F"));
            Background = new SolidColorBrush(Color.Parse("#FFFFFF"));

            SetLabelColors(topPanel, "#1E3A5F", "#888888");
            SetLabelColors(logBorder, "#1E3A5F", "#4A6A8A");
        }

        // Re-apply pause button color after theme change
        var pauseBtn = this.FindControl<Button>("PauseResumeBtn");
        if (pauseBtn != null)
        {
            var color = _vm.IsPaused ? "#E53935" : (_isDark ? "#CDD6F4" : "#1E3A5F");
            pauseBtn.Content = new TextBlock
            {
                Text = _vm.IsPaused ? "▶" : "⏸",
                Foreground = new SolidColorBrush(Color.Parse(color)),
                FontSize = 14
            };
        }
    }

    private void SetupNumericClamp(string name, int min, int max)
    {
        var nud = this.FindControl<NumericUpDown>(name);
        if (nud == null) return;
        nud.Loaded += (_, _) =>
        {
            var textBox = nud.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (textBox == null) return;
            textBox.LostFocus += (_, _) =>
            {
                var text = textBox.Text;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (int.TryParse(text, out int typed))
                        nud.Value = Math.Clamp(typed, min, max);
                });
            };
        };
    }

    private static readonly HashSet<string> SecondaryColors =
        ["#FF888888", "#FF8B8FA8", "#FF4A6A8A", "#FF999999", "#FF8899AA"];

    private static void SetLabelColors(Visual? root, string primaryHex, string secondaryHex)
    {
        if (root == null) return;
        var primary = new SolidColorBrush(Color.Parse(primaryHex));
        var secondary = new SolidColorBrush(Color.Parse(secondaryHex));

        foreach (var tb in root.GetVisualDescendants().OfType<TextBlock>())
        {
            var current = (tb.Foreground as SolidColorBrush)?.Color.ToString() ?? "";
            tb.Foreground = SecondaryColors.Contains(current) ? secondary : primary;
        }
    }
}
