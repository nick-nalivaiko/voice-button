namespace VoiceButton.Models;

public sealed record VoiceOption(string Id, string Label)
{
    public static IReadOnlyList<VoiceOption> All { get; } =
    [
        new("marin", "Marin"),
        new("cedar", "Cedar"),
        new("alloy", "Alloy"),
        new("nova", "Nova"),
        new("coral", "Coral"),
        new("sage", "Sage"),
        new("verse", "Verse"),
        new("ash", "Ash"),
        new("ballad", "Ballad"),
        new("echo", "Echo"),
        new("fable", "Fable"),
        new("onyx", "Onyx"),
        new("shimmer", "Shimmer")
    ];

    public static IReadOnlyList<VoiceOption> Legacy { get; } =
    [
        new("alloy", "Alloy"),
        new("echo", "Echo"),
        new("fable", "Fable"),
        new("onyx", "Onyx"),
        new("nova", "Nova"),
        new("shimmer", "Shimmer")
    ];

    public override string ToString() => Label;
}
