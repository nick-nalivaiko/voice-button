namespace VoiceButton.Models;

public sealed record InterfaceLanguageOption(string Id, string Label)
{
    public static IReadOnlyList<InterfaceLanguageOption> All { get; } =
    [
        new("ru", "Русский"),
        new("uk", "Українська"),
        new("en", "English")
    ];

    public override string ToString() => Label;
}
