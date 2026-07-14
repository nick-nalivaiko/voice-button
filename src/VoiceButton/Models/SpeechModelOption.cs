namespace VoiceButton.Models;

public sealed record SpeechModelOption(string Id, string Label)
{
    public static IReadOnlyList<SpeechModelOption> All { get; } =
    [
        new("gpt-4o-mini-tts", "gpt-4o-mini-tts"),
        new("gpt-4o-mini-tts-2025-12-15", "gpt-4o-mini-tts-2025-12-15"),
        new("tts-1", "tts-1"),
        new("tts-1-hd", "tts-1-hd")
    ];

    public override string ToString() => Label;
}
