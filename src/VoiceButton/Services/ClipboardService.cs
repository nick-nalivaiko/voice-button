using WpfClipboard = System.Windows.Clipboard;
using WpfTextDataFormat = System.Windows.TextDataFormat;

namespace VoiceButton.Services;

public sealed class ClipboardService
{
    public ClipboardSnapshot Capture()
    {
        try
        {
            return new ClipboardSnapshot(WpfClipboard.GetDataObject());
        }
        catch
        {
            return new ClipboardSnapshot(null);
        }
    }

    public string GetText()
    {
        try
        {
            return WpfClipboard.ContainsText(WpfTextDataFormat.UnicodeText)
                ? WpfClipboard.GetText(WpfTextDataFormat.UnicodeText)
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<string> WaitForChangedTextAsync(uint previousSequence, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentSequence = NativeMethods.GetClipboardSequenceNumber();
            var text = GetText();
            if ((previousSequence == 0 || currentSequence != previousSequence) && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            await Task.Delay(80, cancellationToken);
        }

        throw new InvalidOperationException("Кнопка Copy нажата, но текст в clipboard не появился.");
    }
}

public sealed class ClipboardSnapshot(System.Windows.IDataObject? dataObject)
{
    public void Restore()
    {
        if (dataObject is null)
        {
            return;
        }

        try
        {
            WpfClipboard.SetDataObject(dataObject, copy: true);
        }
        catch
        {
            // Clipboard can be temporarily locked by another process.
        }
    }
}
