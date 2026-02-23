using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GraphicPing.ViewModels;

namespace GraphicPing.Controls;

public class PingGraphControl : Control
{
    private static readonly Typeface LabelFont = new("Inter", FontStyle.Normal, FontWeight.Normal);

    public static readonly StyledProperty<MainWindowViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<PingGraphControl, MainWindowViewModel?>(nameof(ViewModel));

    public static readonly StyledProperty<bool> IsDarkProperty =
        AvaloniaProperty.Register<PingGraphControl, bool>(nameof(IsDark));

    public MainWindowViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public bool IsDark
    {
        get => GetValue(IsDarkProperty);
        set => SetValue(IsDarkProperty, value);
    }

    static PingGraphControl()
    {
        AffectsRender<PingGraphControl>(ViewModelProperty, IsDarkProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewModelProperty)
        {
            if (change.OldValue is MainWindowViewModel oldVm)
                oldVm.GraphDataUpdated -= OnDataUpdated;
            if (change.NewValue is MainWindowViewModel newVm)
                newVm.GraphDataUpdated += OnDataUpdated;
        }
    }

    private void OnDataUpdated() => InvalidateVisual();

    // Scroll offset: 0 = show latest (rightmost), positive = scrolled left
    private int _scrollOffset;

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var vm = ViewModel;
        if (vm == null) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Horizontal scroll
            int visibleCount = Math.Max(10, (int)(100 / vm.ZoomLevel));
            int step = Math.Max(1, visibleCount / 10);
            _scrollOffset += e.Delta.Y > 0 ? step : -step;
            _scrollOffset = Math.Max(0, _scrollOffset);
            InvalidateVisual();
        }
        else
        {
            // Zoom
            double delta = e.Delta.Y > 0 ? 1.2 : 1 / 1.2;
            vm.ZoomLevel *= delta;
        }
        e.Handled = true;
    }

    // Drag state
    private enum DragMode { None, ChartPan, ScrollBar }
    private DragMode _dragMode;
    private Point _dragStart;
    private int _dragStartOffset;

    // Cached layout values for hit testing
    private double _lastGraphH;
    private int _lastMaxScroll;
    private int _lastMaxPoints;
    private int _lastVisibleCount;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(this);
        const double marginLeft = 58;
        const double marginRight = 15;
        const double marginTop = 10;
        double graphW = Bounds.Width - marginLeft - marginRight;

        // Check if click is on scrollbar area (bottom 10px of graph)
        double scrollBarY = marginTop + _lastGraphH - 10;
        if (_lastMaxScroll > 0 && pos.Y >= scrollBarY && pos.Y <= scrollBarY + 14
            && pos.X >= marginLeft && pos.X <= marginLeft + graphW)
        {
            _dragMode = DragMode.ScrollBar;
            UpdateScrollFromMouseX(pos.X, marginLeft, graphW);
        }
        else
        {
            _dragMode = DragMode.ChartPan;
            _dragStart = pos;
            _dragStartOffset = _scrollOffset;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragMode = DragMode.None;
        e.Pointer.Capture(null);
    }

    private void UpdateScrollFromMouseX(double mouseX, double marginLeft, double graphW)
    {
        if (_lastMaxScroll <= 0) return;
        double thumbRatio = (double)_lastVisibleCount / _lastMaxPoints;
        double thumbW = Math.Max(20, graphW * thumbRatio);
        double trackRange = graphW - thumbW;
        if (trackRange <= 0) return;
        double ratio = 1.0 - (mouseX - marginLeft - thumbW / 2) / trackRange;
        _scrollOffset = (int)Math.Round(Math.Clamp(ratio, 0, 1) * _lastMaxScroll);
        InvalidateVisual();
    }

    // Tooltip on hover
    private Point _lastMouse;
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _lastMouse = e.GetPosition(this);

        if (_dragMode == DragMode.ScrollBar)
        {
            const double marginLeft = 58;
            const double marginRight = 15;
            double graphW = Bounds.Width - marginLeft - marginRight;
            UpdateScrollFromMouseX(_lastMouse.X, marginLeft, graphW);
        }
        else if (_dragMode == DragMode.ChartPan)
        {
            var vm = ViewModel;
            if (vm == null) return;
            const double marginLeft = 58;
            const double marginRight = 15;
            double graphW = Bounds.Width - marginLeft - marginRight;
            int visibleCount = Math.Max(10, (int)(100 / vm.ZoomLevel));
            double xStep = visibleCount > 1 ? graphW / (visibleCount - 1) : 1;
            double dx = _lastMouse.X - _dragStart.X;
            int pointsDelta = (int)(dx / xStep);
            _scrollOffset = Math.Max(0, _dragStartOffset + pointsDelta);
            InvalidateVisual();
        }
        else
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var w = bounds.Width;
        var h = bounds.Height;

        var bgColor = IsDark ? Color.Parse("#181926") : Color.Parse("#FAFBFC");
        var gridColor = IsDark ? Color.Parse("#2E3050") : Color.Parse("#E0E0E0");
        var textColor = IsDark ? Color.Parse("#A6ADC8") : Color.Parse("#666666");
        var borderColor = IsDark ? Color.Parse("#3B3F5C") : Color.Parse("#CCCCCC");

        var bgBrush = new SolidColorBrush(bgColor);
        var textBrush = new SolidColorBrush(textColor);
        var failBrush = new SolidColorBrush(Color.Parse("#F44336"));
        var gridPen = new Pen(new SolidColorBrush(gridColor), 0.5, new DashStyle(new[] { 4.0, 4.0 }, 0));
        var borderPen = new Pen(new SolidColorBrush(borderColor), 1);

        context.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

        var vm = ViewModel;
        if (vm == null || vm.HostDataList.Count == 0)
        {
            var ft = MakeText("Enter hosts and click ▶ Start", 14, textBrush);
            context.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
            return;
        }

        const double marginLeft = 58;
        const double marginRight = 15;
        const double marginTop = 10;
        const double marginBottom = 25;
        double legendHeight = vm.HostDataList.Count * 18 + 8;

        var graphW = w - marginLeft - marginRight;
        var graphH = h - marginTop - marginBottom - legendHeight;
        if (graphW < 20 || graphH < 20) return;

        // Determine visible data range
        var visibleHosts = vm.HostDataList.Where(hd => hd.IsVisible && hd.IsEnabled).ToList();
        if (visibleHosts.Count == 0)
        {
            var ft = MakeText("All hosts hidden", 13, textBrush);
            context.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
            return;
        }

        int maxPoints = visibleHosts.Select(h => h.Results.Count).DefaultIfEmpty(0).Max();
        int visibleCount = Math.Max(10, (int)(100 / vm.ZoomLevel));
        // At minimum zoom, ensure all data is visible
        if (vm.ZoomLevel <= 0.1 + 1e-6)
            visibleCount = Math.Max(visibleCount, maxPoints);

        // Clamp scroll offset and cache for hit testing
        int maxScroll = Math.Max(0, maxPoints - visibleCount);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);
        _lastGraphH = graphH;
        _lastMaxScroll = maxScroll;
        _lastMaxPoints = maxPoints;
        _lastVisibleCount = visibleCount;

        int viewEnd = maxPoints - _scrollOffset;
        int viewStart = Math.Max(0, viewEnd - visibleCount);

        // Y-axis max
        long maxMs = 1;
        foreach (var hd in visibleHosts)
        {
            for (int i = viewStart; i < Math.Min(viewEnd, hd.Results.Count); i++)
            {
                var r = hd.Results[i];
                if (r.IsSuccess && r.RoundtripTime.HasValue && r.RoundtripTime.Value > maxMs)
                    maxMs = r.RoundtripTime.Value;
            }
        }
        maxMs = NiceMax((long)(maxMs * 1.25));

        // Grid lines
        int gridLines = 5;
        for (int i = 0; i <= gridLines; i++)
        {
            double y = marginTop + graphH - (graphH * i / gridLines);
            context.DrawLine(gridPen, new Point(marginLeft, y), new Point(marginLeft + graphW, y));
            var label = MakeText($"{maxMs * i / gridLines}ms", 10, textBrush);
            context.DrawText(label, new Point(marginLeft - label.Width - 5, y - label.Height / 2));
        }

        // Border
        context.DrawRectangle(null, borderPen, new Rect(marginLeft, marginTop, graphW, graphH));

        // Zero line highlight
        {
            double y0 = marginTop + graphH;
            var zeroPen = new Pen(new SolidColorBrush(borderColor), 1);
            context.DrawLine(zeroPen, new Point(marginLeft, y0), new Point(marginLeft + graphW, y0));
        }

        // Draw lines
        int displayCount = viewEnd - viewStart;
        double xStep = displayCount > 1 ? graphW / (displayCount - 1) : 0;

        // Hover detection
        int hoverIdx = -1;
        if (displayCount > 0 &&
            _lastMouse.X >= marginLeft && _lastMouse.X <= marginLeft + graphW &&
            _lastMouse.Y >= marginTop && _lastMouse.Y <= marginTop + graphH)
        {
            hoverIdx = displayCount > 1
                ? (int)Math.Round((_lastMouse.X - marginLeft) / xStep)
                : 0;
            hoverIdx = Math.Clamp(hoverIdx, 0, displayCount - 1);
        }

        // Draw failure region shading
        var failRegionBrush = new SolidColorBrush(Color.Parse(IsDark ? "#20FF4444" : "#18F44336"));
        for (int di = 0; di < displayCount; di++)
        {
            bool anyFail = false;
            foreach (var hd in visibleHosts)
            {
                int ri = viewStart + di;
                if (ri < hd.Results.Count && !hd.Results[ri].IsSuccess)
                { anyFail = true; break; }
            }
            if (anyFail)
            {
                double x = marginLeft + di * xStep;
                double barW = displayCount > 1 ? Math.Max(xStep, 2) : graphW;
                context.DrawRectangle(failRegionBrush, null,
                    new Rect(x - barW / 2, marginTop, barW, graphH));
            }
        }

        // Draw hover vertical line
        if (hoverIdx >= 0 && _dragMode == DragMode.None)
        {
            double hx = marginLeft + hoverIdx * xStep;
            var hoverPen = new Pen(new SolidColorBrush(IsDark ? Color.Parse("#4A4E6A") : Color.Parse("#BBBBBB")), 1,
                new DashStyle(new[] { 2.0, 2.0 }, 0));
            context.DrawLine(hoverPen, new Point(hx, marginTop), new Point(hx, marginTop + graphH));
        }

        foreach (var hd in visibleHosts)
        {
            var color = Color.Parse(hd.Color);
            var pen = new Pen(new SolidColorBrush(color), 1.8);
            var pointBrush = new SolidColorBrush(color);
            var fadedBrush = new SolidColorBrush(Color.FromArgb(100, color.R, color.G, color.B));

            Point? prevPoint = null;
            for (int di = 0; di < displayCount; di++)
            {
                int ri = viewStart + di;
                if (ri >= hd.Results.Count) break;
                var r = hd.Results[ri];
                double x = marginLeft + di * xStep;

                if (r.IsSuccess && r.RoundtripTime.HasValue)
                {
                    double y = marginTop + graphH - (graphH * Math.Min(r.RoundtripTime.Value, maxMs) / maxMs);
                    var pt = new Point(x, y);

                    // Fill area under line (subtle)
                    if (prevPoint.HasValue)
                    {
                        var geo = new StreamGeometry();
                        using (var ctx = geo.Open())
                        {
                            ctx.BeginFigure(new Point(prevPoint.Value.X, marginTop + graphH), true);
                            ctx.LineTo(prevPoint.Value);
                            ctx.LineTo(pt);
                            ctx.LineTo(new Point(pt.X, marginTop + graphH));
                            ctx.EndFigure(true);
                        }
                        context.DrawGeometry(fadedBrush, null, geo);
                        context.DrawLine(pen, prevPoint.Value, pt);
                    }

                    double dotR = (hoverIdx >= 0 && di == hoverIdx) ? 4 : 2;
                    context.DrawEllipse(pointBrush, null, pt, dotR, dotR);
                    prevPoint = pt;
                }
                else
                {
                    // Failed: red X at top area
                    double fy = marginTop + 8;
                    context.DrawEllipse(failBrush, null, new Point(x, fy), 3, 3);
                    var xPen = new Pen(failBrush, 1.5);
                    context.DrawLine(xPen, new Point(x - 3, fy - 3), new Point(x + 3, fy + 3));
                    context.DrawLine(xPen, new Point(x - 3, fy + 3), new Point(x + 3, fy - 3));
                    prevPoint = null;
                }
            }
        }

        // Hover tooltip
        if (hoverIdx >= 0 && _dragMode == DragMode.None)
        {
            int ri = viewStart + hoverIdx;
            var tooltipLines = new List<string>();
            DateTime? ts = null;
            foreach (var hd in visibleHosts)
            {
                if (ri < hd.Results.Count)
                {
                    var r = hd.Results[ri];
                    ts ??= r.Timestamp;
                    var val = r.IsSuccess ? $"{r.RoundtripTime}ms" : "FAIL";
                    tooltipLines.Add($"{hd.Host}: {val}");
                }
            }
            if (tooltipLines.Count > 0)
            {
                var header = ts?.ToString("HH:mm:ss.fff") ?? "";
                var tipText = header + "\n" + string.Join("\n", tooltipLines);
                var ft = MakeText(tipText, 11, textBrush);

                double tx = _lastMouse.X + 12;
                double ty = _lastMouse.Y - 10;
                if (tx + ft.Width + 10 > w) tx = _lastMouse.X - ft.Width - 15;
                if (ty + ft.Height + 6 > h) ty = h - ft.Height - 6;
                if (ty < 0) ty = 0;

                var tipBg = IsDark ? new SolidColorBrush(Color.Parse("#232538")) : new SolidColorBrush(Color.Parse("#FFFFFF"));
                var tipBorder = new Pen(new SolidColorBrush(borderColor), 1);
                context.DrawRectangle(tipBg, tipBorder,
                    new Rect(tx - 4, ty - 2, ft.Width + 10, ft.Height + 6), 4, 4);
                context.DrawText(ft, new Point(tx, ty));
            }
        }

        // X-axis time labels
        if (visibleHosts.Count > 0 && displayCount > 0)
        {
            var refHost = visibleHosts.OrderByDescending(h => h.Results.Count).First();
            int labelCount = Math.Min(8, displayCount);
            if (labelCount < 2) labelCount = 1;
            for (int i = 0; i < labelCount; i++)
            {
                int di = labelCount > 1 ? (int)((long)i * (displayCount - 1) / (labelCount - 1)) : 0;
                int ri = viewStart + di;
                if (ri >= refHost.Results.Count) continue;
                double x = marginLeft + di * xStep;
                var timeStr = refHost.Results[ri].Timestamp.ToString("HH:mm:ss");
                var ft = MakeText(timeStr, 9, textBrush);
                context.DrawText(ft, new Point(x - ft.Width / 2, marginTop + graphH + 4));
            }
        }

        // Scroll indicator (only when zoomed in and scrollable)
        if (maxScroll > 0)
        {
            double barH = 8;
            double barY = marginTop + graphH - barH - 2;
            double trackW = graphW;
            double thumbRatio = (double)visibleCount / maxPoints;
            double thumbW = Math.Max(20, trackW * thumbRatio);
            double thumbX = marginLeft + (trackW - thumbW) * (1.0 - (double)_scrollOffset / maxScroll);

            var trackBrush = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128));
            var thumbBrush = new SolidColorBrush(Color.FromArgb(_dragMode == DragMode.ScrollBar ? (byte)180 : (byte)120, 128, 128, 128));
            context.DrawRectangle(trackBrush, null, new Rect(marginLeft, barY, trackW, barH), 4, 4);
            context.DrawRectangle(thumbBrush, null, new Rect(thumbX, barY, thumbW, barH), 4, 4);
        }

        // Legend at bottom
        double legendY = marginTop + graphH + marginBottom + 2;
        double legendX = marginLeft;
        foreach (var hd in vm.HostDataList)
        {
            var color = Color.Parse(hd.Color);
            var brush = new SolidColorBrush(hd.IsVisible && hd.IsEnabled ? color : Color.Parse(IsDark ? "#3B3F5C" : "#CCC"));
            context.DrawRectangle(brush, null, new Rect(legendX, legendY, 12, 12));

            var prefix = (!hd.IsEnabled ? "⏸ " : !hd.IsVisible ? "👁 " : "");
            var name = hd.ResolvedName != null ? $"{hd.Host} ({hd.ResolvedName})" : hd.Host;
            var statText = hd.Stats.Sent > 0 ? $"  {hd.Stats.ShortSummary}" : "";
            var ft = MakeText($"{prefix}{name}{statText}", 11, textBrush);
            context.DrawText(ft, new Point(legendX + 16, legendY - 1));
            legendY += 18;
        }
    }

    private static FormattedText MakeText(string text, double size, IBrush brush) =>
        new(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, LabelFont, size, brush);

    private static long NiceMax(long val)
    {
        if (val <= 1) return 5;
        if (val <= 5) return 5;
        if (val <= 10) return 10;
        if (val <= 20) return 20;
        if (val <= 50) return 50;
        if (val <= 100) return 100;
        if (val <= 200) return 200;
        if (val <= 500) return 500;
        if (val <= 1000) return 1000;
        if (val <= 2000) return 2000;
        if (val <= 5000) return 5000;
        return ((val / 1000) + 1) * 1000;
    }
}
