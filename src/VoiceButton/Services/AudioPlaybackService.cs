using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace VoiceButton.Services;

public sealed record PlaybackSnapshot(bool IsActive, bool IsPaused, TimeSpan Position, TimeSpan Duration)
{
    public static PlaybackSnapshot Inactive { get; } = new(false, false, TimeSpan.Zero, TimeSpan.Zero);

    public double Progress => Duration.TotalMilliseconds > 0
        ? Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1)
        : 0;
}

public sealed class AudioPlaybackService(Dispatcher dispatcher)
{
    private readonly object _gate = new();
    private MediaPlayer? _currentPlayer;
    private PlaybackSnapshot _snapshot = PlaybackSnapshot.Inactive;

    public event EventHandler<PlaybackSnapshot>? PlaybackChanged;

    public PlaybackSnapshot CurrentSnapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public async Task PlayAsync(byte[] audioBytes, string responseFormat, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "VoiceButton");
        Directory.CreateDirectory(directory);

        var extension = string.Equals(responseFormat, "wav", StringComparison.OrdinalIgnoreCase) ? "wav" : "mp3";
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.{extension}");
        await File.WriteAllBytesAsync(path, audioBytes, cancellationToken);

        try
        {
            await await dispatcher.InvokeAsync(() => PlayOnDispatcherAsync(path, cancellationToken));
        }
        finally
        {
            TryDelete(path);
        }
    }

    public void TogglePause()
    {
        RunOnDispatcher(() =>
        {
            MediaPlayer? player;
            PlaybackSnapshot snapshot;
            lock (_gate)
            {
                player = _currentPlayer;
                snapshot = _snapshot;
            }

            if (player is null || !snapshot.IsActive)
            {
                return;
            }

            if (snapshot.IsPaused)
            {
                player.Play();
                SetSnapshot(snapshot with { IsPaused = false });
            }
            else
            {
                player.Pause();
                SetSnapshot(snapshot with { IsPaused = true, Position = player.Position });
            }
        });
    }

    public void Seek(double progress)
    {
        RunOnDispatcher(() =>
        {
            MediaPlayer? player;
            PlaybackSnapshot snapshot;
            lock (_gate)
            {
                player = _currentPlayer;
                snapshot = _snapshot;
            }

            if (player is null || !snapshot.IsActive || snapshot.Duration <= TimeSpan.Zero)
            {
                return;
            }

            var position = TimeSpan.FromTicks((long)(snapshot.Duration.Ticks * Math.Clamp(progress, 0, 1)));
            player.Position = position;
            SetSnapshot(snapshot with { Position = position });
        });
    }

    public void Stop()
    {
        RunOnDispatcher(() =>
        {
            lock (_gate)
            {
                _currentPlayer?.Stop();
            }
        });
    }

    private async Task PlayOnDispatcherAsync(string path, CancellationToken cancellationToken)
    {
        var player = new MediaPlayer { Volume = 1.0 };
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };

        void PublishPosition()
        {
            PlaybackSnapshot snapshot;
            lock (_gate)
            {
                snapshot = _snapshot;
            }

            if (snapshot.IsActive)
            {
                SetSnapshot(snapshot with { Position = player.Position });
            }
        }

        void Opened(object? _, EventArgs __)
        {
            var duration = player.NaturalDuration.HasTimeSpan
                ? player.NaturalDuration.TimeSpan
                : TimeSpan.Zero;

            SetSnapshot(new PlaybackSnapshot(true, false, TimeSpan.Zero, duration));
            timer.Start();
            player.Play();
        }

        void Complete(object? _, EventArgs __)
        {
            completion.TrySetResult();
        }

        void Fail(object? _, ExceptionEventArgs args)
        {
            completion.TrySetException(args.ErrorException);
        }

        timer.Tick += (_, _) => PublishPosition();
        player.MediaOpened += Opened;
        player.MediaEnded += Complete;
        player.MediaFailed += Fail;

        lock (_gate)
        {
            _currentPlayer = player;
        }

        using var registration = cancellationToken.Register(() =>
        {
            dispatcher.BeginInvoke(() =>
            {
                player.Stop();
                completion.TrySetCanceled(cancellationToken);
            });
        });

        try
        {
            player.Open(new Uri(path, UriKind.Absolute));
            await completion.Task;
        }
        finally
        {
            timer.Stop();
            player.Close();
            lock (_gate)
            {
                if (ReferenceEquals(_currentPlayer, player))
                {
                    _currentPlayer = null;
                }
            }

            SetSnapshot(PlaybackSnapshot.Inactive);
        }
    }

    private void SetSnapshot(PlaybackSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
        }

        PlaybackChanged?.Invoke(this, snapshot);
    }

    private void RunOnDispatcher(Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Temporary audio cleanup is best-effort.
        }
    }
}
