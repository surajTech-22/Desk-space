using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ThreadingTimer = System.Threading.Timer;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfPath = System.Windows.Shapes.Path;

namespace DeskSpace;

internal enum FocusMode
{
    Idle,
    Focus,
    ShortBreak,
    LongBreak
}

internal sealed record FocusPresetV380(string Title, int FocusMinutes, int BreakMinutes);

internal sealed record FocusSessionLogEntry(DateTime CompletedUtc, string Mode, string Label, int Minutes);

internal static class DeskSpaceFocusRingV380
{
    private const string FocusOverlayTag = "DeskSpaceFocusRingOverlayV380";

    internal static readonly FocusPresetV380[] Presets =
    {
        new("25 / 5 Deep Work", 25, 5),
        new("50 / 10 Flow", 50, 10),
        new("90 / 15 Ultradian", 90, 15)
    };

    private sealed class FocusStatsDto
    {
        public int CompletedToday { get; set; }
        public string CompletedDate { get; set; } = "";
        public int StreakDays { get; set; }
        public string LastActiveDate { get; set; } = "";
        public int TotalFocusMinutesToday { get; set; }
        public List<FocusSessionLogEntry> RecentSessions { get; set; } = new();
    }

    private sealed class DonutOverlayBinding
    {
        private readonly WeakReference<Canvas> _canvasRef;
        private readonly Action _handler;

        public DonutOverlayBinding(Canvas canvas)
        {
            _canvasRef = new WeakReference<Canvas>(canvas);
            _handler = OnStateChanged;
            StateChanged += _handler;
            canvas.Unloaded += OnCanvasUnloaded;
        }

        private void OnCanvasUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is Canvas c)
                c.Unloaded -= OnCanvasUnloaded;
            StateChanged -= _handler;
        }

        private void OnStateChanged()
        {
            if (!_canvasRef.TryGetTarget(out var canvas))
            {
                StateChanged -= _handler;
                return;
            }

            if (canvas.Dispatcher.HasShutdownStarted)
                return;

            canvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_canvasRef.TryGetTarget(out var target))
                    RenderDonutOverlay(target);
            }));
        }
    }

    private static readonly object StateLock = new();
    private static readonly ConditionalWeakTable<Canvas, DonutOverlayBinding> CanvasBindings = new();

    private static FocusStatsDto? _stats;
    private static FocusMode _mode = FocusMode.Idle;
    private static string _sessionLabel = "Focus Session";
    private static int _totalSeconds;
    private static int _remainingSeconds;
    private static bool _paused;
    private static DateTime _endUtc;
    private static ThreadingTimer? _timer;
    private static Window? _studioWindow;

    private static string StatsPath => System.IO.Path.Combine(WorkspaceStore.Root, "focus-v380.json");

    internal static event Action? StateChanged;
    internal static event Action<string>? SessionCompleted;

    internal static bool IsActive
    {
        get { lock (StateLock) return _mode != FocusMode.Idle; }
    }

    internal static bool IsRunning
    {
        get { lock (StateLock) return _mode != FocusMode.Idle && !_paused; }
    }

    internal static bool IsPaused
    {
        get { lock (StateLock) return _mode != FocusMode.Idle && _paused; }
    }

    internal static string ModeName
    {
        get
        {
            lock (StateLock)
                return FormatModeName(_mode);
        }
    }

    internal static string SessionLabel
    {
        get { lock (StateLock) return _sessionLabel; }
    }

    internal static int TotalSeconds
    {
        get { lock (StateLock) return _totalSeconds; }
    }

    internal static int RemainingSeconds
    {
        get { lock (StateLock) return Math.Max(0, _remainingSeconds); }
    }

    internal static double ProgressFraction
    {
        get
        {
            lock (StateLock)
            {
                if (_mode == FocusMode.Idle || _totalSeconds <= 0)
                    return 0.0;
                double elapsed = _totalSeconds - Math.Clamp(_remainingSeconds, 0, _totalSeconds);
                return Math.Clamp(elapsed / _totalSeconds, 0.0, 1.0);
            }
        }
    }

    internal static int CompletedToday
    {
        get
        {
            lock (StateLock)
            {
                var stats = EnsureStatsLocked();
                return stats.CompletedToday;
            }
        }
    }

    internal static int StreakDays
    {
        get
        {
            lock (StateLock)
            {
                var stats = EnsureStatsLocked();
                return stats.StreakDays;
            }
        }
    }

    internal static int TotalFocusMinutesToday
    {
        get
        {
            lock (StateLock)
            {
                var stats = EnsureStatsLocked();
                return stats.TotalFocusMinutesToday;
            }
        }
    }

    internal static string FormattedRemaining
    {
        get
        {
            int rem = RemainingSeconds;
            int mins = rem / 60;
            int secs = rem % 60;
            return $"{mins:00}:{secs:00}";
        }
    }

    internal static void StartFocus(int focusMinutes = 25, string label = "Focus Session")
    {
        int mins = Math.Clamp(focusMinutes, 1, 240);
        string cleanLabel = string.IsNullOrWhiteSpace(label) ? "Focus Session" : label.Trim();

        lock (StateLock)
        {
            EnsureStatsLocked();
            _mode = FocusMode.Focus;
            _sessionLabel = cleanLabel;
            _totalSeconds = mins * 60;
            _remainingSeconds = _totalSeconds;
            _paused = false;
            _endUtc = DateTime.UtcNow.AddSeconds(_totalSeconds);
            StartTimerLocked();
        }

        StateChanged?.Invoke();
    }

    internal static void StartBreak(int breakMinutes = 5, string label = "Short Break")
    {
        int mins = Math.Clamp(breakMinutes, 1, 120);
        string cleanLabel = string.IsNullOrWhiteSpace(label) ? (mins >= 15 ? "Long Break" : "Short Break") : label.Trim();
        FocusMode breakMode = mins >= 15 || cleanLabel.Contains("Long", StringComparison.OrdinalIgnoreCase)
            ? FocusMode.LongBreak
            : FocusMode.ShortBreak;

        lock (StateLock)
        {
            EnsureStatsLocked();
            _mode = breakMode;
            _sessionLabel = cleanLabel;
            _totalSeconds = mins * 60;
            _remainingSeconds = _totalSeconds;
            _paused = false;
            _endUtc = DateTime.UtcNow.AddSeconds(_totalSeconds);
            StartTimerLocked();
        }

        StateChanged?.Invoke();
    }

    internal static void TogglePauseResume()
    {
        bool changed = false;
        lock (StateLock)
        {
            if (_mode == FocusMode.Idle)
                return;

            if (_paused)
            {
                _paused = false;
                _endUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, _remainingSeconds));
                StartTimerLocked();
            }
            else
            {
                int rem = (int)Math.Ceiling((_endUtc - DateTime.UtcNow).TotalSeconds);
                _remainingSeconds = Math.Clamp(rem, 0, Math.Max(0, _totalSeconds));
                _paused = true;
                StopTimerLocked();
            }
            changed = true;
        }

        if (changed)
            StateChanged?.Invoke();
    }

    internal static void Reset()
    {
        lock (StateLock)
        {
            StopTimerLocked();
            _mode = FocusMode.Idle;
            _paused = false;
            _totalSeconds = 0;
            _remainingSeconds = 0;
            _sessionLabel = "Focus Session";
        }

        StateChanged?.Invoke();
    }

    private static void StartTimerLocked()
    {
        StopTimerLocked();
        _timer = new ThreadingTimer(OnTimerTick, null, 1000, 1000);
    }

    private static void StopTimerLocked()
    {
        if (_timer != null)
        {
            try { _timer.Dispose(); } catch { }
            _timer = null;
        }
    }

    private static void OnTimerTick(object? state)
    {
        bool raiseState = false;
        string? completedModeName = null;

        lock (StateLock)
        {
            if (_mode == FocusMode.Idle || _paused)
                return;

            int rem = (int)Math.Ceiling((_endUtc - DateTime.UtcNow).TotalSeconds);
            if (rem <= 0)
            {
                completedModeName = FormatModeName(_mode);
                int completedMinutes = Math.Max(1, _totalSeconds / 60);
                string completedLabel = _sessionLabel;
                bool wasFocus = _mode == FocusMode.Focus;

                StopTimerLocked();
                _mode = FocusMode.Idle;
                _paused = false;
                _remainingSeconds = 0;

                RecordCompletionLocked(wasFocus, completedModeName, completedLabel, completedMinutes);
                raiseState = true;
            }
            else if (rem != _remainingSeconds)
            {
                _remainingSeconds = rem;
                raiseState = true;
            }
        }

        if (raiseState)
            StateChanged?.Invoke();
        if (completedModeName != null)
            SessionCompleted?.Invoke(completedModeName);
    }

    private static FocusStatsDto EnsureStatsLocked()
    {
        if (_stats == null)
        {
            try
            {
                if (File.Exists(StatsPath))
                    _stats = JsonSerializer.Deserialize<FocusStatsDto>(File.ReadAllText(StatsPath));
            }
            catch
            {
            }
            _stats ??= new FocusStatsDto();
            _stats.RecentSessions ??= new List<FocusSessionLogEntry>();
        }

        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (!string.Equals(_stats.CompletedDate, today, StringComparison.Ordinal))
        {
            _stats.CompletedDate = today;
            _stats.CompletedToday = 0;
            _stats.TotalFocusMinutesToday = 0;
        }

        if (!string.IsNullOrEmpty(_stats.LastActiveDate) &&
            !string.Equals(_stats.LastActiveDate, today, StringComparison.Ordinal) &&
            !string.Equals(_stats.LastActiveDate, yesterday, StringComparison.Ordinal))
        {
            _stats.StreakDays = 0;
        }

        return _stats;
    }

    private static void RecordCompletionLocked(bool wasFocus, string modeName, string label, int minutes)
    {
        var stats = EnsureStatsLocked();
        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (wasFocus)
        {
            if (!string.Equals(stats.LastActiveDate, today, StringComparison.Ordinal))
            {
                stats.StreakDays = string.Equals(stats.LastActiveDate, yesterday, StringComparison.Ordinal)
                    ? stats.StreakDays + 1
                    : 1;
                stats.LastActiveDate = today;
            }
            stats.CompletedDate = today;
            stats.CompletedToday++;
            stats.TotalFocusMinutesToday += minutes;
        }

        stats.RecentSessions.Insert(0, new FocusSessionLogEntry(DateTime.UtcNow, modeName, label, minutes));
        if (stats.RecentSessions.Count > 30)
            stats.RecentSessions.RemoveRange(30, stats.RecentSessions.Count - 30);

        SaveStatsLocked(stats);
    }

    private static void SaveStatsLocked(FocusStatsDto stats)
    {
        try
        {
            Directory.CreateDirectory(WorkspaceStore.Root);
            string tmp = StatsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, StatsPath, true);
            }
            finally
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
        }
        catch
        {
        }
    }

    private static string FormatModeName(FocusMode mode) => mode switch
    {
        FocusMode.Focus => "Focus",
        FocusMode.ShortBreak => "Short Break",
        FocusMode.LongBreak => "Long Break",
        _ => "Idle"
    };

    internal static PathGeometry BuildProgressArcGeometry(double cx, double cy, double radius, double fraction)
    {
        var geometry = new PathGeometry();
        if (!double.IsFinite(cx) || !double.IsFinite(cy) || !double.IsFinite(radius) || !double.IsFinite(fraction) ||
            radius <= 0 || fraction <= 0.0001)
        {
            geometry.Freeze();
            return geometry;
        }

        double clamped = Math.Clamp(fraction, 0.0, 1.0);
        var startPoint = new Point(cx, cy - radius);

        if (clamped >= 0.9999)
        {
            var bottomPoint = new Point(cx, cy + radius);
            var fullFigure = new PathFigure { StartPoint = startPoint, IsClosed = true };
            fullFigure.Segments.Add(new ArcSegment(bottomPoint, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            fullFigure.Segments.Add(new ArcSegment(startPoint, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            geometry.Figures.Add(fullFigure);
            geometry.Freeze();
            return geometry;
        }

        double sweepDegrees = clamped * 360.0;
        double endRadians = (-90.0 + sweepDegrees) * Math.PI / 180.0;
        var endPoint = new Point(cx + radius * Math.Cos(endRadians), cy + radius * Math.Sin(endRadians));
        bool isLargeArc = sweepDegrees > 180.0;

        var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
        figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true));
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    internal static void AttachToDonutCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        if (!CanvasBindings.TryGetValue(canvas, out _))
            CanvasBindings.Add(canvas, new DonutOverlayBinding(canvas));

        RenderDonutOverlay(canvas);
    }

    private static void RenderDonutOverlay(Canvas canvas)
    {
        for (int i = canvas.Children.Count - 1; i >= 0; i--)
        {
            if (canvas.Children[i] is FrameworkElement fe && Equals(fe.Tag, FocusOverlayTag))
                canvas.Children.RemoveAt(i);
        }

        if (!IsActive)
            return;

        bool isBreak = ModeName != "Focus";
        Color ringColor = isBreak
            ? Color.FromRgb(52, 211, 153)
            : (SystemParameters.HighContrast ? SystemColors.HighlightColor : Color.FromRgb(56, 189, 248));

        var trackRing = new WpfEllipse
        {
            Tag = FocusOverlayTag,
            Width = 265,
            Height = 265,
            Stroke = new SolidColorBrush(Color.FromArgb(50, ringColor.R, ringColor.G, ringColor.B)),
            StrokeThickness = 4.5,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(trackRing, 13.5);
        Canvas.SetTop(trackRing, 12.5);
        canvas.Children.Add(trackRing);

        var arcPath = new WpfPath
        {
            Tag = FocusOverlayTag,
            Data = BuildProgressArcGeometry(146, 145, 132.5, ProgressFraction),
            Stroke = new SolidColorBrush(ringColor),
            StrokeThickness = 5.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false,
            Effect = SystemParameters.HighContrast ? null : new DropShadowEffect
            {
                Color = ringColor,
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.85
            }
        };
        canvas.Children.Add(arcPath);

        string badgeText = $"{(IsPaused ? "⏸ " : "")}{(isBreak ? "BREAK" : "FOCUS")} · {FormattedRemaining}";
        var badgeHost = new Grid
        {
            Tag = FocusOverlayTag,
            Width = 292,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(badgeHost, 0);
        Canvas.SetTop(badgeHost, 248);

        badgeHost.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(228, 12, 18, 35)),
            BorderBrush = new SolidColorBrush(ringColor),
            BorderThickness = new Thickness(1.2),
            Padding = new Thickness(9, 3, 9, 3),
            Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(244, 248, 255))
            }
        });
        canvas.Children.Add(badgeHost);
    }

    internal static void ShowStudio(Window? owner)
    {
        if (_studioWindow != null)
        {
            try
            {
                if (_studioWindow.WindowState == WindowState.Minimized)
                    _studioWindow.WindowState = WindowState.Normal;
                _studioWindow.Show();
                _studioWindow.Activate();
                return;
            }
            catch
            {
                _studioWindow = null;
            }
        }

        var p = DeskSpaceDesignV283.Current;
        var win = DeskSpaceFluentBackdropV380.CreateModernWindow("DeskSpace · Focus Studio", 560, 660, owner, transient: false);
        win.MinWidth = 480;
        win.MinHeight = 560;
        _studioWindow = win;

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var root = new StackPanel { Margin = new Thickness(20) };
        scroll.Content = root;

        root.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("DESKSPACE  /  FOCUS RING", p));
        root.Children.Add(new TextBlock
        {
            Text = "Focus Studio",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            Margin = new Thickness(0, 0, 0, 14)
        });

        // Today's Streak stats row
        var statsGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        for (int i = 0; i < 3; i++)
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        TextBlock completedVal = new() { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = DeskSpaceDesignV283.Solid(p.Text) };
        TextBlock minutesVal = new() { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = DeskSpaceDesignV283.Solid(p.Text) };
        TextBlock streakVal = new() { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = DeskSpaceDesignV283.Solid(p.Accent) };

        Border StatCard(string title, TextBlock valueTb, Thickness margin)
        {
            var stack = new StackPanel();
            stack.Children.Add(valueTb);
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                Foreground = DeskSpaceDesignV283.Solid(p.Muted),
                Margin = new Thickness(0, 2, 0, 0)
            });
            var card = DeskSpaceFluentBackdropV380.CreateCard(stack, p);
            card.Padding = new Thickness(12, 10, 12, 10);
            card.Margin = margin;
            return card;
        }

        var c0 = StatCard("Completed Today", completedVal, new Thickness(0, 0, 6, 0));
        var c1 = StatCard("Focus Minutes", minutesVal, new Thickness(3, 0, 3, 0));
        var c2 = StatCard("Day Streak", streakVal, new Thickness(6, 0, 0, 0));
        Grid.SetColumn(c0, 0);
        Grid.SetColumn(c1, 1);
        Grid.SetColumn(c2, 2);
        statsGrid.Children.Add(c0);
        statsGrid.Children.Add(c1);
        statsGrid.Children.Add(c2);
        root.Children.Add(statsGrid);

        // Live Ring Gauge card
        var gaugeCanvas = new Canvas
        {
            Width = 210,
            Height = 210,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var trackCircle = new WpfEllipse
        {
            Width = 172,
            Height = 172,
            Stroke = DeskSpaceDesignV283.Solid(p.Border),
            StrokeThickness = 10,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(trackCircle, 19);
        Canvas.SetTop(trackCircle, 19);
        gaugeCanvas.Children.Add(trackCircle);

        var arcShape = new WpfPath
        {
            Stroke = DeskSpaceDesignV283.Solid(p.Accent),
            StrokeThickness = 10,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent
        };
        gaugeCanvas.Children.Add(arcShape);

        var modeText = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Accent),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var timeText = new TextBlock
        {
            FontSize = 36,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var activeLabelText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 140
        };

        var centerStack = new StackPanel
        {
            Width = 160,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { modeText, timeText, activeLabelText }
        };
        Canvas.SetLeft(centerStack, 25);
        Canvas.SetTop(centerStack, 64);
        gaugeCanvas.Children.Add(centerStack);

        var gaugeCard = DeskSpaceFluentBackdropV380.CreateCard(gaugeCanvas, p);
        gaugeCard.Margin = new Thickness(0, 0, 0, 14);
        root.Children.Add(gaugeCard);

        // Controls card
        var controlsBody = new StackPanel();
        controlsBody.Children.Add(new TextBlock
        {
            Text = "Task / Session Label",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            Margin = new Thickness(0, 0, 0, 5)
        });

        var labelBox = new TextBox
        {
            Text = IsActive ? SessionLabel : "Deep Work",
            FontSize = 13.5,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DeskSpaceDesignV283.Input(labelBox, p);
        controlsBody.Children.Add(labelBox);

        int lastBreakMinutes = 5;
        controlsBody.Children.Add(new TextBlock
        {
            Text = "Quick Presets",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            Margin = new Thickness(0, 0, 0, 6)
        });

        var presetWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var preset in Presets)
        {
            var captured = preset;
            var btn = DeskSpaceFluentBackdropV380.CreatePillButton(captured.Title, () =>
            {
                lastBreakMinutes = captured.BreakMinutes;
                StartFocus(captured.FocusMinutes, string.IsNullOrWhiteSpace(labelBox.Text) ? captured.Title : labelBox.Text);
            });
            btn.Margin = new Thickness(0, 0, 8, 6);
            presetWrap.Children.Add(btn);
        }
        controlsBody.Children.Add(presetWrap);

        var customRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Center };
        customRow.Children.Add(new TextBlock
        {
            Text = "Custom min:",
            FontSize = 12.5,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 4)
        });

        var customMinutesBox = new TextBox
        {
            Text = "25",
            Width = 64,
            Margin = new Thickness(0, 0, 8, 4),
            ToolTip = "Focus duration in minutes (1–240)"
        };
        DeskSpaceDesignV283.Input(customMinutesBox, p);
        customRow.Children.Add(customMinutesBox);

        var startFocusBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Start Focus", () =>
        {
            if (!int.TryParse(customMinutesBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int mins) || mins < 1 || mins > 240)
                mins = 25;
            customMinutesBox.Text = mins.ToString(CultureInfo.InvariantCulture);
            StartFocus(mins, string.IsNullOrWhiteSpace(labelBox.Text) ? "Focus Session" : labelBox.Text);
        }, primary: true);
        startFocusBtn.Margin = new Thickness(0, 0, 8, 4);
        customRow.Children.Add(startFocusBtn);

        var startBreakBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Start Break", () =>
        {
            StartBreak(lastBreakMinutes, lastBreakMinutes >= 15 ? "Long Break" : "Short Break");
        });
        startBreakBtn.Margin = new Thickness(0, 0, 8, 4);
        customRow.Children.Add(startBreakBtn);

        Button? pauseBtn = null;
        pauseBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Pause", TogglePauseResume);
        pauseBtn.Margin = new Thickness(0, 0, 8, 4);
        customRow.Children.Add(pauseBtn);

        var resetBtn = DeskSpaceFluentBackdropV380.CreatePillButton("Reset", Reset);
        resetBtn.Margin = new Thickness(0, 0, 0, 4);
        customRow.Children.Add(resetBtn);

        controlsBody.Children.Add(customRow);

        var statusNote = new TextBlock
        {
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            TextWrapping = TextWrapping.Wrap,
            Text = "Active focus sessions render automatically around the compact DeskSpace donut."
        };
        controlsBody.Children.Add(statusNote);

        var controlsCard = DeskSpaceFluentBackdropV380.CreateCard(controlsBody, p);
        controlsCard.Margin = new Thickness(0, 0, 0, 14);
        root.Children.Add(controlsCard);

        // Recent sessions list
        root.Children.Add(DeskSpaceFluentBackdropV380.CreateSectionHeader("RECENT SESSIONS", p));
        var recentList = new ListBox
        {
            MaxHeight = 140,
            BorderThickness = new Thickness(1)
        };
        DeskSpaceDesignV283.Selection(recentList, p);
        root.Children.Add(recentList);

        win.Content = scroll;
        DeskSpaceFluentBackdropV380.ApplyFadeEntrance(root);

        void RefreshUi()
        {
            completedVal.Text = CompletedToday.ToString(CultureInfo.CurrentCulture);
            minutesVal.Text = TotalFocusMinutesToday.ToString(CultureInfo.CurrentCulture);
            streakVal.Text = StreakDays == 1 ? "1 day" : $"{StreakDays} days";

            bool active = IsActive;
            bool paused = IsPaused;
            string mode = ModeName;
            bool isBreak = mode is "Short Break" or "Long Break";

            modeText.Text = active ? (paused ? $"{mode.ToUpperInvariant()} · PAUSED" : mode.ToUpperInvariant()) : "READY";
            timeText.Text = active ? FormattedRemaining : "00:00";
            activeLabelText.Text = active ? SessionLabel : "Choose a preset to start";

            Color arcColor = isBreak ? Color.FromRgb(52, 211, 153) : p.Accent;
            arcShape.Stroke = new SolidColorBrush(arcColor);
            arcShape.Data = BuildProgressArcGeometry(105, 105, 86, active ? ProgressFraction : 0.0);

            if (pauseBtn != null)
            {
                pauseBtn.IsEnabled = active;
                pauseBtn.Content = paused ? "Resume" : "Pause";
            }
            resetBtn.IsEnabled = active;

            recentList.Items.Clear();
            List<FocusSessionLogEntry> logs;
            lock (StateLock)
                logs = EnsureStatsLocked().RecentSessions.Take(10).ToList();

            if (logs.Count == 0)
            {
                recentList.Items.Add("No completed sessions recorded yet.");
            }
            else
            {
                foreach (var entry in logs)
                {
                    recentList.Items.Add($"{entry.CompletedUtc.ToLocalTime():t} · {entry.Mode} ({entry.Minutes}m) — {entry.Label}");
                }
            }
        }

        void OnStateChanged()
        {
            if (!win.Dispatcher.HasShutdownStarted)
                win.Dispatcher.BeginInvoke(new Action(RefreshUi));
        }

        void OnSessionCompleted(string completedMode)
        {
            if (!win.Dispatcher.HasShutdownStarted)
            {
                win.Dispatcher.BeginInvoke(new Action(() =>
                {
                    statusNote.Text = $"{completedMode} completed! Take a moment or start the next round.";
                    RefreshUi();
                }));
            }
        }

        StateChanged += OnStateChanged;
        SessionCompleted += OnSessionCompleted;
        win.Closed += (_, _) =>
        {
            StateChanged -= OnStateChanged;
            SessionCompleted -= OnSessionCompleted;
            if (ReferenceEquals(_studioWindow, win))
                _studioWindow = null;
        };

        RefreshUi();
        win.Show();
        win.Activate();
    }
}
