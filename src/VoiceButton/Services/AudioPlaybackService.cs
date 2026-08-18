using System.Buffers;
using System.IO;
using System.Windows.Threading;
using NAudio.Wave;

namespace VoiceButton.Services;

public sealed record PlaybackSnapshot(bool IsActive, bool IsPaused, TimeSpan Position, TimeSpan Duration)
{
    public static PlaybackSnapshot Inactive { get; } = new(false, false, TimeSpan.Zero, TimeSpan.Zero);

    public double Progress => Duration.TotalMilliseconds > 0
        ? Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1)
        : 0;
}

public sealed class AudioPlaybackService(Dispatcher dispatcher, float outputVolume = 1.0f)
{
    private readonly object _gate = new();
    private StreamingPlaybackSession? _currentSession;
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

    public async Task PlayStreamingAsync(
        Stream audioStream,
        string responseFormat,
        CancellationToken cancellationToken,
        bool startStopped = false)
    {
        if (!string.Equals(responseFormat, "mp3", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Потоковое воспроизведение сейчас поддерживает формат MP3.");
        }

        var session = new StreamingPlaybackSession(PublishSnapshot, outputVolume, startStopped);
        lock (_gate)
        {
            if (_currentSession is not null)
            {
                throw new InvalidOperationException("Другая озвучка уже воспроизводится.");
            }

            _currentSession = session;
        }

        try
        {
            await session.RunAsync(audioStream, cancellationToken);
        }
        finally
        {
            session.Dispose();
            lock (_gate)
            {
                if (ReferenceEquals(_currentSession, session))
                {
                    _currentSession = null;
                }
            }

            PublishSnapshot(PlaybackSnapshot.Inactive);
        }
    }

    public void TogglePause()
    {
        StreamingPlaybackSession? session;
        lock (_gate)
        {
            session = _currentSession;
        }

        session?.TogglePause();
    }

    public void Seek(double progress)
    {
        StreamingPlaybackSession? session;
        lock (_gate)
        {
            session = _currentSession;
        }

        session?.Seek(progress);
    }

    public bool StopAndCollapse()
    {
        StreamingPlaybackSession? session;
        lock (_gate)
        {
            session = _currentSession;
        }

        if (session is null)
        {
            PublishSnapshot(PlaybackSnapshot.Inactive);
            return false;
        }

        session.SoftStop();
        return true;
    }

    public bool Resume()
    {
        StreamingPlaybackSession? session;
        lock (_gate)
        {
            session = _currentSession;
        }

        return session?.Resume() == true;
    }

    public void Cancel()
    {
        StreamingPlaybackSession? session;
        lock (_gate)
        {
            session = _currentSession;
        }

        session?.Cancel();
        PublishSnapshot(PlaybackSnapshot.Inactive);
    }

    private void PublishSnapshot(PlaybackSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
        }

        void RaiseChanged()
        {
            PlaybackChanged?.Invoke(this, snapshot);
        }

        if (dispatcher.CheckAccess())
        {
            RaiseChanged();
        }
        else if (!dispatcher.HasShutdownStarted)
        {
            _ = dispatcher.BeginInvoke(RaiseChanged, DispatcherPriority.Background);
        }
    }

    private sealed class StreamingPlaybackSession(Action<PlaybackSnapshot> publishSnapshot, float outputVolume, bool startStopped) : IDisposable
    {
        private static readonly TimeSpan InitialBuffer = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MinimumStartupDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MinimumBuffer = TimeSpan.FromSeconds(4.5);
        private static readonly TimeSpan ResumeBuffer = TimeSpan.FromSeconds(8);

        private readonly object _gate = new();
        private ProgressiveWaveProvider? _provider;
        private WaveOutEvent? _output;
        private CancellationTokenSource? _runCancellation;
        private Exception? _decodeError;
        private Exception? _playbackError;
        private DateTime _providerReadyUtc;
        private bool _downloadComplete;
        private bool _started;
        private bool _userPaused;
        private bool _buffering = true;
        private bool _playbackStopped;
        private bool _transportStopped = startStopped;
        private bool _disposed;

        public async Task RunAsync(Stream audioStream, CancellationToken cancellationToken)
        {
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_gate)
            {
                _runCancellation = runCancellation;
            }

            using var streamCancellation = runCancellation.Token.Register(
                static state => TryDisposeStream((Stream)state!),
                audioStream);

            var decodeTask = Task.Run(
                () => DecodeMp3(audioStream, runCancellation.Token),
                CancellationToken.None);
            var monitorTask = MonitorPlaybackAsync(runCancellation.Token);

            try
            {
                await Task.WhenAll(decodeTask, monitorTask);
            }
            catch
            {
                runCancellation.Cancel();
                TryDisposeStream(audioStream);
                throw;
            }
            finally
            {
                lock (_gate)
                {
                    _runCancellation = null;
                }

                StopAndDisposeOutput();
            }
        }

        public void TogglePause()
        {
            WaveOutEvent? output;
            bool shouldPlay;
            lock (_gate)
            {
                if (_provider is null || _transportStopped || _disposed)
                {
                    return;
                }

                _userPaused = !_userPaused;
                output = _output;
                shouldPlay = !_userPaused && _started && CanResumeLocked();
            }

            if (_userPaused)
            {
                SafePause(output);
            }
            else if (shouldPlay)
            {
                SafePlay(output);
            }

            PublishCurrentSnapshot();
        }

        public void Seek(double progress)
        {
            ProgressiveWaveProvider? provider;
            WaveOutEvent? output;
            bool shouldPause;
            bool shouldPlay;

            lock (_gate)
            {
                provider = _provider;
                output = _output;
                if (provider is null || _transportStopped || _disposed)
                {
                    return;
                }

                provider.Seek(progress);
                var state = provider.GetState();
                shouldPause = _started && !_userPaused && !state.IsComplete && state.BufferedDuration < MinimumBuffer;
                if (shouldPause)
                {
                    _buffering = true;
                }

                shouldPlay = _started && !_userPaused && CanResumeLocked();
                if (shouldPlay)
                {
                    _playbackStopped = false;
                }
            }

            if (shouldPause)
            {
                SafePause(output);
            }
            else if (shouldPlay)
            {
                SafePlay(output);
            }

            PublishCurrentSnapshot();
        }

        public void SoftStop()
        {
            WaveOutEvent? output;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _transportStopped = true;
                _userPaused = false;
                output = _output;
            }

            SafeStop(output);
            publishSnapshot(PlaybackSnapshot.Inactive);
        }

        public bool Resume()
        {
            ProgressiveWaveProvider? provider;
            WaveOutEvent? output;
            bool shouldPlay;

            lock (_gate)
            {
                if (_disposed)
                {
                    return false;
                }

                _transportStopped = false;
                _userPaused = false;
                provider = _provider;
                output = _output;

                if (provider is not null)
                {
                    var state = provider.GetState();
                    if (state.IsComplete && !state.HasRemainingAudio)
                    {
                        provider.Seek(0);
                    }

                    shouldPlay = _started && CanResumeLocked();
                    if (shouldPlay)
                    {
                        _playbackStopped = false;
                    }
                }
                else
                {
                    shouldPlay = false;
                }
            }

            if (shouldPlay)
            {
                SafePlay(output);
            }

            PublishCurrentSnapshot(forceActiveWithoutProvider: true);
            return true;
        }

        public void Cancel()
        {
            CancellationTokenSource? cancellation;
            WaveOutEvent? output;
            lock (_gate)
            {
                cancellation = _runCancellation;
                output = _output;
            }

            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The playback task has already completed.
            }

            SafeStop(output);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            Cancel();
            StopAndDisposeOutput();
        }

        private void DecodeMp3(Stream audioStream, CancellationToken cancellationToken)
        {
            IMp3FrameDecompressor? decompressor = null;
            var decodeBuffer = ArrayPool<byte>.Shared.Rent(65536);
            var frameStream = audioStream.CanSeek
                ? audioStream
                : new PositionTrackingReadStream(audioStream);

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Mp3Frame? frame;
                    try
                    {
                        frame = Mp3Frame.LoadFromStream(frameStream);
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }

                    if (frame is null)
                    {
                        break;
                    }

                    if (decompressor is null)
                    {
                        var sourceFormat = new Mp3WaveFormat(
                            frame.SampleRate,
                            frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                            frame.FrameLength,
                            frame.BitRate);
                        decompressor = new AcmMp3FrameDecompressor(sourceFormat);
                        var provider = new ProgressiveWaveProvider(decompressor.OutputFormat);

                        lock (_gate)
                        {
                            _provider = provider;
                            _providerReadyUtc = DateTime.UtcNow;
                        }

                        PublishCurrentSnapshot();
                    }

                    var decoded = decompressor.DecompressFrame(frame, decodeBuffer, 0);
                    _provider!.Append(decodeBuffer, 0, decoded);
                }

                if (_provider is null)
                {
                    throw new InvalidDataException("OpenAI вернул пустой или неподдерживаемый MP3-поток.");
                }
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Потоковая озвучка остановлена.", ex, cancellationToken);
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    _decodeError = ex;
                }

                throw;
            }
            finally
            {
                decompressor?.Dispose();
                ArrayPool<byte>.Shared.Return(decodeBuffer);

                lock (_gate)
                {
                    _downloadComplete = true;
                    _provider?.Complete();
                }

                PublishCurrentSnapshot();
            }
        }

        private async Task MonitorPlaybackAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ProgressiveWaveProvider? provider;
                Exception? decodeError;
                lock (_gate)
                {
                    provider = _provider;
                    decodeError = _decodeError;
                }

                if (decodeError is not null)
                {
                    throw new InvalidOperationException("Не удалось декодировать поток OpenAI TTS.", decodeError);
                }

                if (provider is null)
                {
                    bool downloadComplete;
                    lock (_gate)
                    {
                        downloadComplete = _downloadComplete;
                    }

                    if (downloadComplete)
                    {
                        throw new InvalidDataException("OpenAI не вернул аудиоданные для воспроизведения.");
                    }

                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                var output = EnsureOutput(provider);
                var state = provider.GetState();
                bool startPlayback = false;
                bool pauseForBuffer = false;
                bool resumePlayback = false;
                bool playbackComplete;
                Exception? playbackError;

                lock (_gate)
                {
                    playbackError = _playbackError;
                    var startupDelayElapsed = _providerReadyUtc != default
                        && DateTime.UtcNow - _providerReadyUtc >= MinimumStartupDelay;

                    if (!_started
                        && startupDelayElapsed
                        && (state.DownloadedDuration >= InitialBuffer || state.IsComplete))
                    {
                        _started = true;
                        _buffering = false;
                        _playbackStopped = false;
                        startPlayback = !_userPaused && !_transportStopped;
                    }
                    else if (_started && !_userPaused && !_transportStopped)
                    {
                        if (!state.IsComplete && !_buffering && state.BufferedDuration < MinimumBuffer)
                        {
                            _buffering = true;
                            pauseForBuffer = true;
                        }
                        else if (_buffering && (state.IsComplete || state.BufferedDuration >= ResumeBuffer))
                        {
                            _buffering = false;
                            _playbackStopped = false;
                            resumePlayback = true;
                        }
                        else if (!_buffering && state.HasRemainingAudio && output.PlaybackState == PlaybackState.Stopped)
                        {
                            _playbackStopped = false;
                            resumePlayback = true;
                        }
                    }

                    playbackComplete = !_transportStopped
                        && _started
                        && state.IsComplete
                        && !state.HasRemainingAudio
                        && _playbackStopped;
                }

                if (playbackError is not null)
                {
                    throw new InvalidOperationException("Аудиоустройство остановило воспроизведение.", playbackError);
                }

                if (pauseForBuffer)
                {
                    SafePause(output);
                }
                else if (startPlayback || resumePlayback)
                {
                    SafePlay(output);
                }

                PublishCurrentSnapshot();

                if (playbackComplete)
                {
                    return;
                }

                await Task.Delay(100, cancellationToken);
            }
        }
        private WaveOutEvent EnsureOutput(ProgressiveWaveProvider provider)
        {
            lock (_gate)
            {
                if (_output is not null)
                {
                    return _output;
                }

                var output = new WaveOutEvent
                {
                    DesiredLatency = 120,
                    NumberOfBuffers = 3,
                    Volume = Math.Clamp(outputVolume, 0f, 1f)
                };
                output.PlaybackStopped += Output_PlaybackStopped;
                output.Init(provider);
                _output = output;
                return output;
            }
        }

        private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            lock (_gate)
            {
                _playbackStopped = true;
                if (e.Exception is not null)
                {
                    _playbackError = e.Exception;
                }
            }
        }

        private bool CanResumeLocked()
        {
            if (_provider is null)
            {
                return false;
            }

            var state = _provider.GetState();
            if (state.IsComplete)
            {
                _buffering = false;
                return state.HasRemainingAudio;
            }

            if (_buffering && state.BufferedDuration < ResumeBuffer)
            {
                return false;
            }

            _buffering = false;
            return state.HasRemainingAudio;
        }

        private void PublishCurrentSnapshot(bool forceActiveWithoutProvider = false)
        {
            ProgressiveWaveProvider? provider;
            bool userPaused;
            bool transportStopped;
            lock (_gate)
            {
                provider = _provider;
                userPaused = _userPaused;
                transportStopped = _transportStopped;
            }

            if (transportStopped)
            {
                publishSnapshot(PlaybackSnapshot.Inactive);
                return;
            }

            if (provider is null)
            {
                if (forceActiveWithoutProvider)
                {
                    publishSnapshot(new PlaybackSnapshot(true, false, TimeSpan.Zero, TimeSpan.Zero));
                }

                return;
            }

            var state = provider.GetState();
            publishSnapshot(new PlaybackSnapshot(
                true,
                userPaused,
                state.Position,
                state.DownloadedDuration));
        }

        private void StopAndDisposeOutput()
        {
            WaveOutEvent? output;
            lock (_gate)
            {
                output = _output;
                _output = null;
            }

            if (output is null)
            {
                return;
            }

            output.PlaybackStopped -= Output_PlaybackStopped;
            SafeStop(output);
            output.Dispose();
        }

        private static void SafePlay(WaveOutEvent? output)
        {
            try
            {
                output?.Play();
            }
            catch (ObjectDisposedException)
            {
                // Playback is already shutting down.
            }
        }

        private static void SafePause(WaveOutEvent? output)
        {
            try
            {
                output?.Pause();
            }
            catch (ObjectDisposedException)
            {
                // Playback is already shutting down.
            }
        }

        private static void SafeStop(WaveOutEvent? output)
        {
            try
            {
                output?.Stop();
            }
            catch (ObjectDisposedException)
            {
                // Playback is already shutting down.
            }
        }

        private sealed class PositionTrackingReadStream(Stream inner) : Stream
        {
            private long _position;

            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => Interlocked.Read(ref _position);
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var totalRead = 0;
                while (totalRead < count)
                {
                    var read = inner.Read(buffer, offset + totalRead, count - totalRead);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                Interlocked.Add(ref _position, totalRead);
                return totalRead;
            }

            public override int Read(Span<byte> buffer)
            {
                var totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    var read = inner.Read(buffer[totalRead..]);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                Interlocked.Add(ref _position, totalRead);
                return totalRead;
            }

            public override int ReadByte()
            {
                var value = inner.ReadByte();
                if (value >= 0)
                {
                    Interlocked.Increment(ref _position);
                }

                return value;
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static void TryDisposeStream(Stream stream)
        {
            try
            {
                stream.Dispose();
            }
            catch
            {
                // Disposing is only used to unblock a canceled network read.
            }
        }
    }
}
