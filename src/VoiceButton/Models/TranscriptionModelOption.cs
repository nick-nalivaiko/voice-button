namespace VoiceButton.Models;

public sealed record TranscriptionModelOption(string Id, string Label)
{
    public static IReadOnlyList<TranscriptionModelOption> All { get; } =
    [
        new("gpt-4o-transcribe", "gpt-4o-transcribe"),
        new("gpt-4o-mini-transcribe", "gpt-4o-mini-transcribe")
    ];

    public override string ToString() => Label;
}
