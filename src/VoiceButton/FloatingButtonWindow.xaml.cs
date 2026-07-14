using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using VoiceButton.Models;
using VoiceButton.Services;

namespace VoiceButton;

public partial class FloatingButtonWindow : Window
{
    private const double CompactWidth = 92;
    private const double PlayerWidth = 274;
    private const double EdgePadding = 18;
    private const double ControlZoneWidth = 43;
    private const double DividerWidth = 1;
    private const double SeekZoneWidth = 186;
    private const double PlayedWaveformWidth = 135;

    private readonly Action _speak;
    private readonly Action _startVoiceInput;
    private readonly Action _togglePause;
    private readonly Action<double> _seek;
    private readonly Action _stop;
    private readonly Action<double, double> _savePosition;
    private readonly AppSettings _settings;

    private PlaybackSnapshot _playbackSnapshot = PlaybackSnapshot.Inactive;
    private bool _isPlaybackActive;
    private bool _isSeeking;
    private double _compactLeft;
    private double _compactTop;

    public FloatingButtonWindow(
        Action speak,
        Action startVoiceInput,
        Action togglePause,
        Action<double> seek,
        Action stop,
        AppSettings settings,
        Action<double, double> savePosition)
    {
        InitializeComponent();
        _speak = speak;
        _startVoiceInput = startVoiceInput;
        _togglePause = togglePause;
        _seek = seek;
        _stop = stop;
        _settings = settings;
        _savePosition = savePosition;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        KeepTargetApplicationActive();
        ApplyInitialPosition();
        ApplyPlaybackVisual();
    }

    private void KeepTargetApplicationActive()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var currentStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        _ = NativeMethods.SetWindowLongPtr(
            handle,
            NativeMethods.GwlExStyle,
            new IntPtr(currentStyle | NativeMethods.WsExNoActivate));
    }

    private void ButtonShell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var clickPoint = e.GetPosition(this);

        if (_isPlaybackActive)
        {
            if (clickPoint.X <= ControlZoneWidth)
            {
                _togglePause();
            }
            else if (clickPoint.X >= Width - ControlZoneWidth)
            {
                _stop();
            }
            else
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
        _savePosition(Left, Top);

        var moved = Math.Abs(Left - startLeft) > 3 || Math.Abs(Top - startTop) > 3;
        if (moved)
        {
            return;
        }

        if (clickPoint.X <= ControlZoneWidth)
        {
            _startVoiceInput();
        }
        else
        {
            _speak();
        }
    }

    private void ButtonShell_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isSeeking || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        SeekFromPoint(e.GetPosition(this).X);
        e.Handled = true;
    }

    private void ButtonShell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSeeking)
        {
            return;
        }

        SeekFromPoint(e.GetPosition(this).X);
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

        PauseGlyph.Visibility = active && !_playbackSnapshot.IsPaused
            ? Visibility.Visible
            : Visibility.Collapsed;
        PlayGlyph.Visibility = active && _playbackSnapshot.IsPaused
            ? Visibility.Visible
            : Visibility.Collapsed;

        PlayedWaveformClip.Width = PlayedWaveformWidth * _playbackSnapshot.Progress;
        PlaybackTimeText.Text = FormatTime(_playbackSnapshot.Position);
        ButtonShell.ToolTip = active
            ? (_playbackSnapshot.IsPaused ? "Продолжить / перемотка / стоп" : "Пауза / перемотка / стоп")
            : "Микрофон активного приложения / озвучить последний ответ";
    }

    private void ResizeForPlayback(bool active)
    {
        if (active)
        {
            _compactLeft = Left;
            _compactTop = Top;
            Width = PlayerWidth;
            Left = _compactLeft - (PlayerWidth - CompactWidth);
            Top = _compactTop;
        }
        else
        {
            Width = CompactWidth;
            Left = _compactLeft;
            Top = _compactTop;
            _isSeeking = false;
            ButtonShell.ReleaseMouseCapture();
        }

        ClampToWorkArea();
    }

    private void SeekFromPoint(double x)
    {
        var seekStart = ControlZoneWidth + DividerWidth;
        var progress = (x - seekStart) / SeekZoneWidth;
        _seek(Math.Clamp(progress, 0, 1));
    }

    private void ApplyInitialPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Width = CompactWidth;
        Left = _settings.FloatingButtonLeft ?? workArea.Right - CompactWidth - 24;
        Top = _settings.FloatingButtonTop ?? workArea.Bottom - Height - 24;
        ClampToWorkArea();
        _compactLeft = Left;
        _compactTop = Top;
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
}
