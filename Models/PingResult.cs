namespace GraphicPing.Models;

public class PingResult
{
    public DateTime Timestamp { get; set; }
    public string Host { get; set; } = "";
    public long? RoundtripTime { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public string ToCsvLine() =>
        $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff},{Host},{RoundtripTime?.ToString() ?? ""},{(IsSuccess ? "" : ErrorMessage ?? "FAIL")}";
}

public class HostStatistics
{
    public string Host { get; set; } = "";
    public long Min { get; set; } = long.MaxValue;
    public long Max { get; set; }
    public double Avg { get; set; }
    public double Jitter { get; set; }
    public int Sent { get; set; }
    public int Received { get; set; }
    public double LossPercent => Sent > 0 ? (Sent - Received) * 100.0 / Sent : 0;

    private long _lastRtt;

    public void Update(PingResult result)
    {
        Sent++;
        if (result.IsSuccess && result.RoundtripTime.HasValue)
        {
            Received++;
            var rt = result.RoundtripTime.Value;
            if (rt < Min) Min = rt;
            if (rt > Max) Max = rt;
            Avg = Avg == 0 ? rt : (Avg * (Received - 1) + rt) / Received;
            if (Received > 1)
                Jitter = Jitter + (Math.Abs(rt - _lastRtt) - Jitter) / Received;
            _lastRtt = rt;
        }
    }

    public void Reset()
    {
        Min = long.MaxValue;
        Max = 0;
        Avg = 0;
        Jitter = 0;
        Sent = 0;
        Received = 0;
        _lastRtt = 0;
    }

    public string Summary => Sent == 0 ? "No data" :
        Min == long.MaxValue
            ? $"Loss: {LossPercent:F1}% ({Sent - Received}/{Sent})"
            : $"Min:{Min}ms Max:{Max}ms Avg:{Avg:F1}ms Loss:{LossPercent:F1}%";

    public string ShortSummary => Sent == 0 ? "" :
        Min == long.MaxValue
            ? $"Loss:{LossPercent:F0}%"
            : $"Avg:{Avg:F0}ms Loss:{LossPercent:F0}%";

    public string DetailSummary => Sent == 0 ? "No data" :
        Min == long.MaxValue
            ? $"Sent:{Sent} Received:{Received} Loss:{LossPercent:F1}%"
            : $"Sent:{Sent} Received:{Received} Min:{Min}ms Max:{Max}ms Avg:{Avg:F1}ms Jitter:{Jitter:F1}ms Loss:{LossPercent:F1}%";
}
