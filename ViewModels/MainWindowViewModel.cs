using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Reactive;
using System.Text;
using Avalonia.Threading;
using GraphicPing.Models;
using ReactiveUI;

namespace GraphicPing.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _hostsInput = "8.8.8.8\n1.1.1.1";
    private int? _pingInterval = 1000;
    private int? _pingTimeout = 10000;
    private bool _isPinging;
    private bool _isPaused;
    private string _logText = "";
    private string _statusText = "Ready";
    private bool _isDarkMode;
    private bool _alwaysOnTop;
    private bool _soundAlertOnFail;
    private int? _maxDataPoints = 300;
    private int _graphViewStart;
    private int _graphViewEnd;
    private CancellationTokenSource? _cts;
    private DateTime _startTime;
    private int _totalPingCount;
    private System.Timers.Timer? _statusTimer;

    // Graph zoom/scroll
    private double _zoomLevel = 1.0;

    private static readonly string[] HostColors =
    [
        "#2196F3", "#4CAF50", "#FF9800", "#9C27B0", "#F44336",
        "#00BCD4", "#795548", "#E91E63", "#3F51B5", "#8BC34A",
        "#FF5722", "#009688", "#673AB7", "#CDDC39", "#607D8B"
    ];

    public MainWindowViewModel()
    {
        StartCommand = ReactiveCommand.Create(StartPinging, this.WhenAnyValue(x => x.IsPinging, p => !p));
        StopCommand = ReactiveCommand.Create(StopPinging, this.WhenAnyValue(x => x.IsPinging));
        PauseResumeCommand = ReactiveCommand.Create(TogglePause, this.WhenAnyValue(x => x.IsPinging));
        ClearCommand = ReactiveCommand.Create(ClearData);
        ClearLogCommand = ReactiveCommand.Create(() => { LogText = ""; });
        ExportCsvCommand = ReactiveCommand.Create(ExportCsv);
        ExportLogCommand = ReactiveCommand.Create(ExportLog);

        Presets = new ObservableCollection<HostPreset>(PresetManager.Load());
    }

    public string HostsInput
    {
        get => _hostsInput;
        set => this.RaiseAndSetIfChanged(ref _hostsInput, value);
    }

    public int? PingInterval
    {
        get => _pingInterval;
        set => this.RaiseAndSetIfChanged(ref _pingInterval, value.HasValue ? Math.Max(100, value.Value) : value);
    }

    public int? PingTimeout
    {
        get => _pingTimeout;
        set => this.RaiseAndSetIfChanged(ref _pingTimeout, value.HasValue ? Math.Max(500, value.Value) : value);
    }

    public bool IsPinging
    {
        get => _isPinging;
        set => this.RaiseAndSetIfChanged(ref _isPinging, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    public string LogText
    {
        get => _logText;
        set => this.RaiseAndSetIfChanged(ref _logText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isDarkMode, value);
            ThemeChanged?.Invoke();
        }
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => this.RaiseAndSetIfChanged(ref _alwaysOnTop, value);
    }

    public bool SoundAlertOnFail
    {
        get => _soundAlertOnFail;
        set => this.RaiseAndSetIfChanged(ref _soundAlertOnFail, value);
    }

    public int? MaxDataPoints
    {
        get => _maxDataPoints;
        set => this.RaiseAndSetIfChanged(ref _maxDataPoints, value.HasValue ? Math.Max(50, value.Value) : value);
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            this.RaiseAndSetIfChanged(ref _zoomLevel, Math.Clamp(value, 0.1, 10.0));
            GraphDataUpdated?.Invoke();
        }
    }

    public int GraphViewStart
    {
        get => _graphViewStart;
        set { this.RaiseAndSetIfChanged(ref _graphViewStart, value); GraphDataUpdated?.Invoke(); }
    }

    public int GraphViewEnd
    {
        get => _graphViewEnd;
        set { this.RaiseAndSetIfChanged(ref _graphViewEnd, value); GraphDataUpdated?.Invoke(); }
    }

    public ObservableCollection<HostPingData> HostDataList { get; } = new();
    public ObservableCollection<HostPreset> Presets { get; }

    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseResumeCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearLogCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCsvCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportLogCommand { get; }

    public string StatsSummary
    {
        get
        {
            if (HostDataList.Count == 0) return "";
            int totalSent = HostDataList.Sum(h => h.Stats.Sent);
            if (totalSent == 0) return "";
            int totalReceived = HostDataList.Sum(h => h.Stats.Received);
            int totalErrors = totalSent - totalReceived;
            double lossPercent = totalSent > 0 ? totalErrors * 100.0 / totalSent : 0;
            var perHost = string.Join("  |  ", HostDataList.Select(h =>
                $"{h.Host}: {h.Stats.Sent}req, {h.Stats.Sent - h.Stats.Received}err"));
            return $"Sent: {totalSent}  |  OK: {totalReceived}  |  Errors: {totalErrors}  |  Loss: {lossPercent:F1}%     [{perHost}]";
        }
    }

    public event Action? GraphDataUpdated;
    public event Action? LogUpdated;
    public event Action? ThemeChanged;
    public event Func<string, string?, Task<string?>>? SaveFileRequested;

    private List<string> ParseHosts()
    {
        return _hostsInput
            .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(h => h.Trim())
            .Where(h => h.Length > 0)
            .Distinct()
            .ToList();
    }

    public void ApplyPreset(HostPreset preset)
    {
        HostsInput = string.Join("\n", preset.Hosts);
        PingInterval = preset.Interval;
        PingTimeout = preset.Timeout;
    }

    public void SaveCurrentAsPreset(string name)
    {
        var hosts = ParseHosts();
        if (hosts.Count == 0) return;
        var preset = new HostPreset
        {
            Name = name,
            Hosts = hosts,
            Interval = PingInterval ?? 1000,
            Timeout = PingTimeout ?? 10000
        };
        // Remove existing with same name
        var existing = Presets.FirstOrDefault(p => p.Name == name);
        if (existing != null) Presets.Remove(existing);
        Presets.Add(preset);
        PresetManager.Save(Presets.ToList());
    }

    public void DeletePreset(HostPreset preset)
    {
        Presets.Remove(preset);
        PresetManager.Save(Presets.ToList());
    }

    private void StartPinging()
    {
        var hosts = ParseHosts();
        if (hosts.Count == 0) return;

        HostDataList.Clear();
        LogText = "";
        _isPaused = false;
        this.RaisePropertyChanged(nameof(IsPaused));

        for (int i = 0; i < hosts.Count; i++)
        {
            var hd = new HostPingData
            {
                Host = hosts[i],
                Color = HostColors[i % HostColors.Length],
                IsVisible = true
            };
            HostDataList.Add(hd);
        }

        // Resolve hostnames in background
        foreach (var hd in HostDataList)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var entry = Dns.GetHostEntry(hd.Host);
                    var resolved = entry.HostName;
                    if (resolved != hd.Host)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            hd.ResolvedName = resolved;
                        });
                    }
                }
                catch { }
            });
        }

        IsPinging = true;
        _totalPingCount = 0;
        _startTime = DateTime.Now;
        StatusText = $"Pinging {hosts.Count} host(s)...";
        _cts = new CancellationTokenSource();

        _statusTimer?.Dispose();
        _statusTimer = new System.Timers.Timer(1000);
        _statusTimer.Elapsed += (_, _) =>
        {
            var elapsed = DateTime.Now - _startTime;
            Dispatcher.UIThread.Post(() =>
                StatusText = $"Running: {elapsed:hh\\:mm\\:ss}, {_totalPingCount} pings — {HostDataList.Count(h => h.IsEnabled)} host(s)");
        };
        _statusTimer.Start();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            int cycle = 0;
            while (!token.IsCancellationRequested)
            {
                if (!_isPaused)
                {
                    cycle++;
                    var tasks = HostDataList
                        .Where(hd => hd.IsEnabled)
                        .Select(hd => PingHostAsync(hd, token))
                        .ToArray();
                    await Task.WhenAll(tasks);

                    Dispatcher.UIThread.Post(() =>
                    {
                        // Update view range
                        int maxPts = HostDataList.Where(h => h.IsEnabled).Select(h => h.Results.Count).DefaultIfEmpty(0).Max();
                        GraphViewEnd = maxPts;
                        var visibleCount = (int)(100 / _zoomLevel);
                        GraphViewStart = Math.Max(0, maxPts - visibleCount);

                        StatusText = $"Cycle #{cycle} — {HostDataList.Count(h => h.IsEnabled)} host(s) active";
                        GraphDataUpdated?.Invoke();
                        this.RaisePropertyChanged(nameof(LogText));
                        this.RaisePropertyChanged(nameof(StatsSummary));
                    });
                }

                try { await Task.Delay(_pingInterval ?? 1000, token); }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    private async Task PingHostAsync(HostPingData hostData, CancellationToken token)
    {
        using var ping = new Ping();
        var result = new PingResult
        {
            Timestamp = DateTime.Now,
            Host = hostData.Host
        };

        try
        {
            var reply = await ping.SendPingAsync(hostData.Host, _pingTimeout ?? 10000);
            result.IsSuccess = reply.Status == IPStatus.Success;
            result.RoundtripTime = result.IsSuccess ? reply.RoundtripTime : null;
            if (!result.IsSuccess) result.ErrorMessage = reply.Status.ToString();
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }

        Dispatcher.UIThread.Post(() =>
        {
            hostData.Results.Add(result);
            while (hostData.Results.Count > (_maxDataPoints ?? 300))
                hostData.Results.RemoveAt(0);

            hostData.Stats.Update(result);
            _totalPingCount++;

            if (!result.IsSuccess && _soundAlertOnFail)
            {
                try { Console.Beep(); } catch { }
            }

            var timeStr = result.Timestamp.ToString("HH:mm:ss.fff");
            var displayHost = hostData.ResolvedName != null
                ? $"{hostData.Host} ({hostData.ResolvedName})"
                : hostData.Host;
            var rtStr = result.IsSuccess ? $"{result.RoundtripTime}ms" : $"FAIL ({result.ErrorMessage})";
            var logLine = $"[{timeStr}] {displayHost}: {rtStr}\n";
            _logText += logLine;

            if (_logText.Length > 80000)
                _logText = _logText[(_logText.Length - 50000)..];

            LogUpdated?.Invoke();
        });
    }

    private void StopPinging()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _statusTimer?.Stop();
        _statusTimer?.Dispose();
        _statusTimer = null;
        IsPinging = false;
        IsPaused = false;
        var elapsed = DateTime.Now - _startTime;
        StatusText = $"Stopped. {elapsed:hh\\:mm\\:ss}, {_totalPingCount} pings";
    }

    private void TogglePause()
    {
        IsPaused = !IsPaused;
        StatusText = IsPaused ? "Paused" : $"Pinging {HostDataList.Count(h => h.IsEnabled)} host(s)...";
    }

    private void ClearData()
    {
        foreach (var hd in HostDataList)
        {
            hd.Results.Clear();
            hd.Stats.Sent = 0;
            hd.Stats.Received = 0;
            hd.Stats.Min = long.MaxValue;
            hd.Stats.Max = 0;
            hd.Stats.Avg = 0;
            hd.Stats.Jitter = 0;
        }
        LogText = "";
        GraphDataUpdated?.Invoke();
        this.RaisePropertyChanged(nameof(StatsSummary));
    }

    private void ExportCsv()
    {
        _ = ExportCsvAsync();
    }

    private async Task ExportCsvAsync()
    {
        if (SaveFileRequested == null) return;
        var path = await SaveFileRequested("Export CSV", "ping_results.csv");
        if (path == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Host,RoundtripTime_ms,Error");
        foreach (var hd in HostDataList)
            foreach (var r in hd.Results)
                sb.AppendLine(r.ToCsvLine());

        await File.WriteAllTextAsync(path, sb.ToString());
        StatusText = $"Exported to {Path.GetFileName(path)}";
    }

    private void ExportLog()
    {
        _ = ExportLogAsync();
    }

    private async Task ExportLogAsync()
    {
        if (SaveFileRequested == null) return;
        var path = await SaveFileRequested("Export Log", "ping_log.txt");
        if (path == null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"GraphicPing Log — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 60));
        foreach (var hd in HostDataList)
        {
            var display = hd.ResolvedName != null ? $"{hd.Host} ({hd.ResolvedName})" : hd.Host;
            sb.AppendLine($"\n[{display}] {hd.Stats.DetailSummary}");
        }
        sb.AppendLine($"\n{new string('-', 60)}\n");
        sb.Append(_logText);

        await File.WriteAllTextAsync(path, sb.ToString());
        StatusText = $"Log exported to {Path.GetFileName(path)}";
    }
}

public class HostPingData : ReactiveObject
{
    private bool _isVisible = true;
    private bool _isEnabled = true;
    private string? _resolvedName;

    public string Host { get; set; } = "";
    public string Color { get; set; } = "#2196F3";
    public List<PingResult> Results { get; } = new();
    public HostStatistics Stats { get; } = new();

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    public string? ResolvedName
    {
        get => _resolvedName;
        set => this.RaiseAndSetIfChanged(ref _resolvedName, value);
    }

    public string DisplayName => ResolvedName != null ? $"{Host} ({ResolvedName})" : Host;
}
