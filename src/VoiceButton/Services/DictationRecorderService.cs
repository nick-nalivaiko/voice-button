using System.IO;
using NAudio.Wave;

namespace VoiceButton.Services;

public sealed class DictationRecorderService : IDisposable
{
    private readonly object _sync = new();
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private MemoryStream? _stream;
    private TaskCompletionSource<byte[]>? _completion;

    public bool IsRecording
    {
        get
        {
            lock (_sync)
            {
                return _waveIn is not null;
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_waveIn is not null)
            {
                throw new InvalidOperationException("Запись уже идет.");
            }

            if (WaveInEvent.DeviceCount == 0)
            {
                throw new InvalidOperationException("Windows не обнаружила доступный микрофон.");
            }

            var format = new WaveFormat(16000, 16, 1);
            _stream = new MemoryStream();
            _writer = new WaveFileWriter(_stream, format);
            _completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waveIn = new WaveInEvent
            {
                WaveFormat = format,
                BufferMilliseconds = 100
            };
            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;

            try
            {
                _waveIn.StartRecording();
            }
            catch
            {
                ReleaseResources(_waveIn, _writer, _stream);
                _waveIn = null;
                _writer = null;
                _stream = null;
                _completion = null;
                throw;
            }
        }
    }

    public Task<byte[]> StopAsync(CancellationToken cancellationToken)
    {
        Task<byte[]> completion;
        WaveInEvent waveIn;
        lock (_sync)
        {
            if (_waveIn is null || _completion is null)
            {
                throw new InvalidOperationException("Запись не запущена.");
            }

            completion = _completion.Task;
            waveIn = _waveIn;
        }

        waveIn.StopRecording();
        return completion.WaitAsync(cancellationToken);
    }

    public void Cancel()
    {
        WaveInEvent? waveIn;
        lock (_sync)
        {
            waveIn = _waveIn;
            _completion?.TrySetCanceled();
        }

        waveIn?.StopRecording();
    }

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
            _writer?.Flush();
        }
    }

    private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        WaveInEvent? waveIn;
        WaveFileWriter? writer;
        MemoryStream? stream;
        TaskCompletionSource<byte[]>? completion;

        lock (_sync)
        {
            waveIn = _waveIn;
            writer = _writer;
            stream = _stream;
            completion = _completion;
            _waveIn = null;
            _writer = null;
            _stream = null;
            _completion = null;
        }

        byte[] audio = [];
        try
        {
            writer?.Dispose();
            audio = stream?.ToArray() ?? [];
        }
        finally
        {
            ReleaseResources(waveIn, null, stream);
        }

        if (e.Exception is not null)
        {
            completion?.TrySetException(e.Exception);
        }
        else if (audio.Length <= 44)
        {
            completion?.TrySetException(new InvalidOperationException("Запись не содержит аудио."));
        }
        else
        {
            completion?.TrySetResult(audio);
        }
    }

    private void ReleaseResources(WaveInEvent? waveIn, WaveFileWriter? writer, MemoryStream? stream)
    {
        if (waveIn is not null)
        {
            waveIn.DataAvailable -= WaveIn_DataAvailable;
            waveIn.RecordingStopped -= WaveIn_RecordingStopped;
            waveIn.Dispose();
        }

        writer?.Dispose();
        stream?.Dispose();
    }

    public void Dispose()
    {
        Cancel();
    }
}