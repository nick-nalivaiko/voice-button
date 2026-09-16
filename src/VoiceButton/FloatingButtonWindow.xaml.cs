using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using VoiceButton.Models;
using VoiceButton.Services;

namespace VoiceButton;

public partial class FloatingButtonWindow : Window
{
    private const double IdleCompactWidth = 92;
    private const double ResumeCompactWidth = 136;
    private const double ResumeZoneWidth = 48;
    private const double PlayerWidth = 334;
    private const double LiveTabOverhang = 9;
    private const double EdgePadding = 18;
    private const double ControlZoneWidth = 43;
    private const double DividerWidth = 1;
    private const double NavigationZoneWidth = 30;
    private const double SeekZoneWidth = 186;
    private const double PlayedWaveformWidth = 135;
    private static readonly TimeSpan ZOrderRefreshInterval = TimeSpan.FromMilliseconds(400);
    private static readonly HashSet<string> SystemWindowsAbovePlayer = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer",
        "ShellExperienceHost",
        "StartMenuExperienceHost"
    };
    private static readonly HashSet<string> ShellFlyoutsAbovePlayer = new(StringComparer.OrdinalIgnoreCase)
    {
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland"
    };
    private static readonly HashSet<string> IgnoredZOrderWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd"
    };

    private readonly Action _startVoiceInput;
    private readonly Action _resumePlayback;
    private readonly Action _speakLatest;
    private readonly Action _speakClipboard;
    private readonly Action _navigatePrevious;
    private readonly Action _navigateNext;
    private readonly Action _togglePause;
    private readonly Action<double> _seek;
    private readonly Action _stop;
    private readonly Action _toggleLiveNarration;
    private readonly Action<double, double> _savePosition;
    private readonly AppSettings _settings;
    private readonly CodexWindowFinder _codexWindowFinder;
    private readonly DispatcherTimer _zOrderTimer;

    private PlaybackSnapshot _playbackSnapshot = PlaybackSnapshot.Inactive;
    private bool _isPlaybackActive;
    private bool _isSeeking;
    private bool _dictationRecording;
    private bool _dictationProcessing;
    private bool _canResumePlayback;
    private bool _liveNarrationAvailable;
    private bool _liveNarrationActive;
    private bool _canNavigatePrevious;
    private bool _canNavigateNext;
    private bool _navigationUsesLiveHistory;
    private string _compactIdleTooltip = "Микрофон / новый ответ; ПКМ по динамику: озвучить clipboard";
    private string _compactResumeTooltip = "Микрофон / продолжить аудио / новый ответ; ПКМ по динамику: clipboard";
    private string _recordingTooltip = "Остановить запись и вставить текст";
    private string _processingTooltip = "Распознаю речь";
    private string _pausedPlaybackTooltip = "Продолжить / перемотка / стоп";
    private string _playingPlaybackTooltip = "Пауза / перемотка / стоп";
    private string _liveNarrationOffTooltip = "Включить озвучку хода работы Codex";
    private string _liveNarrationOnTooltip = "Выключить озвучку хода работы Codex";
    private string _previousAnswerTooltip = "Предыдущий ответ";
    private string _nextAnswerTooltip = "Следующий ответ";
    private string _previousLiveTooltip = "Предыдущий промежуточный диалог";
    private string _nextLiveTooltip = "Следующий промежуточный диалог";
    private double _compactRight;
    private double _compactTop;
    private IntPtr _windowHandle;
    private bool _topmostApplied;

    public FloatingButtonWindow(
        Action startVoiceInput,
        Action resumePlayback,
        Action speakLatest,
        Action speakClipboard,
        Action navigatePrevious,
        Action navigateNext,
        Action togglePause,
        Action<double> seek,
        Action stop,
        Action toggleLiveNarration,
        AppSettings settings,
        CodexWindowFinder codexWindowFinder,
        Action<double, double> savePosition)
    {
        InitializeComponent();
        _startVoiceInput = startVoiceInput;
        _resumePlayback = resumePlayback;
        _speakLatest = speakLatest;
        _speakClipboard = speakClipboard;
        _navigatePrevious = navigatePrevious;
        _navigateNext = navigateNext;
        _togglePause = togglePause;
        _seek = seek;
        _stop = stop;
        _toggleLiveNarration = toggleLiveNarration;
        _settings = settings;
        _codexWindowFinder = codexWindowFinder;
        _savePosition = savePosition;
        _zOrderTimer = new DispatcherTimer
        {
            Interval = ZOrderRefreshInterval
        };
        _zOrderTimer.Tick += ZOrderTimer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        ConfigureNonActivatingWindow();
        ApplyInitialPosition();
        UpdateContentClip();
        ApplyPlaybackVisual();
        ApplyZOrderPolicy();
        _zOrderTimer.Start();
    }

    private void ContentClip_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateContentClip();
    }

    private void UpdateContentClip()
    {
        if (ContentClip.ActualWidth <= 0 || ContentClip.ActualHeight <= 0)
        {
            return;
        }

        var radius = ContentClip.ActualHeight / 2;
        ContentClip.Clip = new RectangleGeometry(
            new Rect(0, 0, ContentClip.ActualWidth, ContentClip.ActualHeight),
            radius,
            radius);
    }

    protected override void OnClosed(EventArgs e)
    {
        _zOrderTimer.Stop();
        base.OnClosed(e);
    }

    private void ConfigureNonActivatingWindow()
    {
        var currentStyle = NativeMethods.GetWindowLongPtr(_windowHandle, NativeMethods.GwlExStyle).ToInt64();
        _ = NativeMethods.SetWindowLongPtr(
            _windowHandle,
            NativeMethods.GwlExStyle,
            new IntPtr(currentStyle | NativeMethods.WsExNoActivate));
    }

    private void ZOrderTimer_Tick(object? sender, EventArgs e)
    {
        ApplyZOrderPolicy();
    }

    public void SetAlwaysOnTop(bool enabled)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetAlwaysOnTop(enabled));
            return;
        }

        _settings.FloatingButtonAlwaysOnTop = enabled;
        if (!enabled)
        {
            SetTopmost(false);
            PlaceAtBottom();
        }

        ApplyZOrderPolicy();
    }

    private void ApplyZOrderPolicy()
    {
        if (_windowHandle == IntPtr.Zero
            || !IsVisible
            || !NativeMethods.IsWindow(_windowHandle))
        {
            return;
        }

        var shellFlyout = FindVisibleShellFlyout();
        if (shellFlyout != IntPtr.Zero)
        {
            SetTopmost(false);
            PlaceBelow(shellFlyout);
            return;
        }

        var topApplicationWindow = FindTopVisibleApplicationWindow();
        if (topApplicationWindow == IntPtr.Zero)
        {
            SetTopmost(_settings.FloatingButtonAlwaysOnTop);
            return;
        }

        if (IsSystemWindowAbovePlayer(topApplicationWindow))
        {
            SetTopmost(false);
            PlaceBelow(topApplicationWindow);
            return;
        }

        if (_settings.FloatingButtonAlwaysOnTop)
        {
            SetTopmost(true);
            return;
        }

        SetTopmost(false);
        if (_codexWindowFinder.ClassifyWindow(topApplicationWindow) is not null)
        {
            PlaceAtTop();
            return;
        }

        PlaceBelow(topApplicationWindow);
    }

    private void SetTopmost(bool enabled)
    {
        if (_topmostApplied == enabled)
        {
            return;
        }

        _ = NativeMethods.SetWindowPos(
            _windowHandle,
            enabled ? NativeMethods.HwndTopmost : NativeMethods.HwndNotTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoActivate
            | NativeMethods.SwpNoOwnerZOrder
            | NativeMethods.SwpNoSendChanging);
        _topmostApplied = enabled;
    }

    private void PlaceAtTop()
    {
        _ = NativeMethods.SetWindowPos(
            _windowHandle,
            NativeMethods.HwndTop,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoActivate
            | NativeMethods.SwpNoOwnerZOrder
            | NativeMethods.SwpNoSendChanging);
    }

    private void PlaceAtBottom()
    {
        _ = NativeMethods.SetWindowPos(
            _windowHandle,
            NativeMethods.HwndBottom,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoActivate
            | NativeMethods.SwpNoOwnerZOrder
            | NativeMethods.SwpNoSendChanging);
    }

    private void PlaceBelow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindow(windowHandle))
        {
            PlaceAtBottom();
            return;
        }

        _ = NativeMethods.SetWindowPos(
            _windowHandle,
            windowHandle,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoActivate
            | NativeMethods.SwpNoOwnerZOrder
            | NativeMethods.SwpNoSendChanging);
    }

    private IntPtr FindTopVisibleApplicationWindow()
    {
        var topWindow = IntPtr.Zero;
        _ = NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (IsZOrderApplicationWindow(windowHandle))
            {
                topWindow = windowHandle;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return topWindow;
    }

    private bool IsZOrderApplicationWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero
            || windowHandle == _windowHandle
            || !NativeMethods.IsWindowVisible(windowHandle)
            || NativeMethods.IsIconic(windowHandle)
            || NativeMethods.IsWindowCloaked(windowHandle))
        {
            return false;
        }

        var windowClass = NativeMethods.GetWindowClassName(windowHandle);
        if (IgnoredZOrderWindowClasses.Contains(windowClass)
            || ShellFlyoutsAbovePlayer.Contains(windowClass))
        {
            return false;
        }

        var extendedStyle = NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle).ToInt64();
        if ((extendedStyle & NativeMethods.WsExNoActivate) != 0)
        {
            return false;
        }

        return NativeMethods.GetWindowRect(windowHandle, out var bounds)
            && bounds.Width >= 80
            && bounds.Height >= 40;
    }

    private static bool IsSystemWindowAbovePlayer(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _ = NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return SystemWindowsAbovePlayer.Contains(process.ProcessName);
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr FindVisibleShellFlyout()
    {
        var found = IntPtr.Zero;
        _ = NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (NativeMethods.IsWindowVisible(windowHandle)
                && ShellFlyoutsAbovePlayer.Contains(NativeMethods.GetWindowClassName(windowHandle)))
            {
                found = windowHandle;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }

    private void ButtonShell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var clickPoint = e.GetPosition(ButtonShell);
        var controlWidth = ButtonShell.ActualWidth;

        if (_isPlaybackActive)
        {
            if (clickPoint.X <= ControlZoneWidth)
            {
                _togglePause();
            }
            else if (clickPoint.X >= controlWidth - ControlZoneWidth)
            {
                _stop();
            }
            else if (clickPoint.X >= PlayerSeekStart
                && clickPoint.X <= PlayerSeekStart + SeekZoneWidth)
            {
                _isSeeking = true;
                ButtonShell.CaptureMouse();
                SeekFromPoint(clickPoint.X);
            }

            e.Handled = true;
            return;
        }

        var startLeft = Left;
        var startTop = Top;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        ClampToWorkArea();
        UpdateCompactAnchor();
        _savePosition(_compactRight - IdleCompactWindowWidth, _compactTop + LiveTabOverhang);

        var moved = Math.Abs(Left - startLeft) > 3 || Math.Abs(Top - startTop) > 3;
        if (moved)
        {
            return;
        }

        if (clickPoint.X <= ControlZoneWidth)
        {
            if (!_dictationProcessing)
            {
                _startVoiceInput();
            }
        }
        else if (!_canResumePlayback || clickPoint.X >= controlWidth - ControlZoneWidth)
        {
            if (!_dictationRecording && !_dictationProcessing)
            {
                _speakLatest();
            }
        }
        else if (!_dictationRecording && !_dictationProcessing)
        {
            _resumePlayback();
        }
    }

    private void ButtonShell_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isSeeking || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        SeekFromPoint(e.GetPosition(ButtonShell).X);
        e.Handled = true;
    }

    private void CompactSpeakerZone_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_dictationRecording || _dictationProcessing)
        {
            return;
        }

        _speakClipboard();
    }

    private void LiveNarrationButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_liveNarrationAvailable)
        {
            _toggleLiveNarration();
        }
    }

    private void PreviousNavigationButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_canNavigatePrevious)
        {
            _navigatePrevious();
        }
    }

    private void NextNavigationButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_canNavigateNext)
        {
            _navigateNext();
        }
    }

    private void ButtonShell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSeeking)
        {
            return;
        }

        SeekFromPoint(e.GetPosition(ButtonShell).X);
        _isSeeking = false;
        ButtonShell.ReleaseMouseCapture();
        e.Handled = true;
    }

    public void SetPlaybackSnapshot(PlaybackSnapshot snapshot)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetPlaybackSnapshot(snapshot));
            return;
        }

        _playbackSnapshot = snapshot;
        if (IsLoaded)
        {
            ApplyPlaybackVisual();
        }
    }

    public void SetDictationState(bool recording, bool processing)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetDictationState(recording, processing));
            return;
        }

        _dictationRecording = recording;
        _dictationProcessing = processing;
        if (IsLoaded)
        {
            ApplyPlaybackVisual();
        }
    }

    public void SetResumeAvailable(bool available)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetResumeAvailable(available));
            return;
        }

        if (_canResumePlayback == available)
        {
            return;
        }

        _canResumePlayback = available;
        if (IsLoaded)
        {
            if (!_isPlaybackActive)
            {
                ResizeCompactForResumeAvailability();
            }

            ApplyPlaybackVisual();
        }
    }

    public void SetLiveNarrationState(bool available, bool active)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetLiveNarrationState(available, active));
            return;
        }

        _liveNarrationAvailable = available;
        _liveNarrationActive = available && active;
        if (IsLoaded)
        {
            ApplyPlaybackVisual();
        }
    }

    public void SetNavigationState(bool canPrevious, bool canNext, bool useLiveHistory)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetNavigationState(canPrevious, canNext, useLiveHistory));
            return;
        }

        _canNavigatePrevious = canPrevious;
        _canNavigateNext = canNext;
        _navigationUsesLiveHistory = useLiveHistory;
        if (IsLoaded)
        {
            ApplyPlaybackVisual();
        }
    }

    public void SetLocalizedTooltips(
        string compactIdle,
        string compactResume,
        string recording,
        string processing,
        string pausedPlayback,
        string playingPlayback,
        string liveNarrationOff,
        string liveNarrationOn,
        string previousAnswer,
        string nextAnswer,
        string previousLive,
        string nextLive)
    {
        _compactIdleTooltip = compactIdle;
        _compactResumeTooltip = compactResume;
        _recordingTooltip = recording;
        _processingTooltip = processing;
        _pausedPlaybackTooltip = pausedPlayback;
        _playingPlaybackTooltip = playingPlayback;
        _liveNarrationOffTooltip = liveNarrationOff;
        _liveNarrationOnTooltip = liveNarrationOn;
        _previousAnswerTooltip = previousAnswer;
        _nextAnswerTooltip = nextAnswer;
        _previousLiveTooltip = previousLive;
        _nextLiveTooltip = nextLive;
        if (IsLoaded)
        {
            ApplyPlaybackVisual();
        }
    }

    private void ApplyPlaybackVisual()
    {
        var active = _playbackSnapshot.IsActive;
        if (active != _isPlaybackActive)
        {
            ResizeForPlayback(active);
            _isPlaybackActive = active;
        }

        CompactGrid.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        PlayerGrid.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        CompactMicrophoneGlyph.Visibility = !active && !_dictationRecording
            ? Visibility.Visible
            : Visibility.Collapsed;
        CompactMicrophoneGlyph.Opacity = _dictationProcessing ? 0.38 : 1;
        CompactRecordingGlyph.Visibility = !active && _dictationRecording
            ? Visibility.Visible
            : Visibility.Collapsed;
        CompactResumeColumn.Width = new GridLength(_canResumePlayback ? ResumeZoneWidth : 0);
        CompactResumeDividerColumn.Width = new GridLength(_canResumePlayback ? DividerWidth : 0);
        CompactResumeZone.Visibility = _canResumePlayback ? Visibility.Visible : Visibility.Collapsed;
        CompactResumeDivider.Visibility = _canResumePlayback ? Visibility.Visible : Visibility.Collapsed;
        CompactResumeContent.Opacity = !_dictationRecording && !_dictationProcessing ? 1 : 0.32;
        CompactSpeakerContent.Opacity = !_dictationRecording && !_dictationProcessing ? 1 : 0.32;

        PauseGlyph.Visibility = active && !_playbackSnapshot.IsPaused
            ? Visibility.Visible
            : Visibility.Collapsed;
        PlayGlyph.Visibility = active && _playbackSnapshot.IsPaused
            ? Visibility.Visible
            : Visibility.Collapsed;

        PlayedWaveformClip.Width = PlayedWaveformWidth * _playbackSnapshot.Progress;
        PlaybackTimeText.Text = FormatTime(_playbackSnapshot.Position);
        LiveNarrationButton.Visibility = _liveNarrationAvailable ? Visibility.Visible : Visibility.Collapsed;
        LiveNarrationButton.Background = BrushFromHex(_liveNarrationActive ? "#3A1016" : "#F9C74F");
        LiveNarrationButton.BorderBrush = BrushFromHex(_liveNarrationActive ? "#FF5C5C" : "#FFE69A");
        LiveNarrationGlow.Fill = BrushFromHex(_liveNarrationActive ? "#F25F5C" : "#FFD166");
        LiveNarrationGlow.Opacity = _liveNarrationActive ? 0.82 : 0.5;
        LiveNarrationDot.Fill = BrushFromHex(_liveNarrationActive ? "#FFF1F1" : "#071528");
        LiveNarrationButton.ToolTip = _liveNarrationActive ? _liveNarrationOnTooltip : _liveNarrationOffTooltip;
        PreviousNavigationButton.Opacity = _canNavigatePrevious ? 1 : 0.28;
        PreviousNavigationButton.IsHitTestVisible = _canNavigatePrevious;
        PreviousNavigationButton.Cursor = _canNavigatePrevious
            ? System.Windows.Input.Cursors.Hand
            : System.Windows.Input.Cursors.Arrow;
        NextNavigationButton.Opacity = _canNavigateNext ? 1 : 0.28;
        NextNavigationButton.IsHitTestVisible = _canNavigateNext;
        NextNavigationButton.Cursor = _canNavigateNext
            ? System.Windows.Input.Cursors.Hand
            : System.Windows.Input.Cursors.Arrow;
        PreviousNavigationButton.ToolTip = _navigationUsesLiveHistory
            ? _previousLiveTooltip
            : _previousAnswerTooltip;
        NextNavigationButton.ToolTip = _navigationUsesLiveHistory
            ? _nextLiveTooltip
            : _nextAnswerTooltip;
        System.Windows.Automation.AutomationProperties.SetName(
            PreviousNavigationButton,
            PreviousNavigationButton.ToolTip?.ToString() ?? string.Empty);
        System.Windows.Automation.AutomationProperties.SetName(
            NextNavigationButton,
            NextNavigationButton.ToolTip?.ToString() ?? string.Empty);
        System.Windows.Automation.AutomationProperties.SetName(
            LiveNarrationButton,
            LiveNarrationButton.ToolTip?.ToString() ?? string.Empty);
        ButtonShell.ToolTip = active
            ? (_playbackSnapshot.IsPaused ? _pausedPlaybackTooltip : _playingPlaybackTooltip)
            : _dictationRecording
                ? _recordingTooltip
                : _dictationProcessing
                    ? _processingTooltip
                    : _canResumePlayback
                        ? _compactResumeTooltip
                        : _compactIdleTooltip;
        System.Windows.Automation.AutomationProperties.SetName(ButtonShell, ButtonShell.ToolTip?.ToString() ?? string.Empty);
    }

    private void ResizeForPlayback(bool active)
    {
        if (active)
        {
            UpdateCompactAnchor();
            Width = PlayerWindowWidth;
            Left = _compactRight - PlayerWindowWidth;
            Top = _compactTop;
        }
        else
        {
            Width = CurrentCompactWidth;
            Left = _compactRight - Width;
            Top = _compactTop;
            _isSeeking = false;
            ButtonShell.ReleaseMouseCapture();
        }

        ClampToWorkArea();
        UpdateCompactAnchor();
    }

    private void ResizeCompactForResumeAvailability()
    {
        var right = Left + Width;
        Width = CurrentCompactWidth;
        Left = right - Width;
        ClampToWorkArea();
        UpdateCompactAnchor();
    }

    private void UpdateCompactAnchor()
    {
        _compactRight = Left + Width;
        _compactTop = Top;
    }

    private double CurrentCompactWidth => (_canResumePlayback ? ResumeCompactWidth : IdleCompactWidth) + LiveTabOverhang;

    private static double IdleCompactWindowWidth => IdleCompactWidth + LiveTabOverhang;

    private static double PlayerWindowWidth => PlayerWidth + LiveTabOverhang;

    private static double PlayerSeekStart => ControlZoneWidth + DividerWidth + NavigationZoneWidth;

    private void SeekFromPoint(double x)
    {
        var progress = (x - PlayerSeekStart) / SeekZoneWidth;
        _seek(Math.Clamp(progress, 0, 1));
    }

    private void ApplyInitialPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Width = CurrentCompactWidth;
        var idleLeft = _settings.FloatingButtonLeft ?? workArea.Right - IdleCompactWidth - 24;
        Left = idleLeft + IdleCompactWindowWidth - Width;
        Top = _settings.FloatingButtonTop is double savedTop
            ? savedTop - LiveTabOverhang
            : workArea.Bottom - Height - 24;
        ClampToWorkArea();
        UpdateCompactAnchor();
    }

    private void ClampToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, workArea.Left + EdgePadding, workArea.Right - Width - EdgePadding);
        Top = Math.Clamp(Top, workArea.Top + EdgePadding, workArea.Bottom - Height - EdgePadding);
    }

    private static string FormatTime(TimeSpan position)
    {
        var totalMinutes = Math.Max(0, (int)position.TotalMinutes);
        return $"{totalMinutes}:{Math.Max(0, position.Seconds):00}";
    }

    private static SolidColorBrush BrushFromHex(string color)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }
}
