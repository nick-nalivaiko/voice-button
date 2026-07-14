namespace VoiceButton.Models;

public sealed record TranscriptionLanguageOption(string Id, string TranslationKey, string ApiLanguage)
{
    public static IReadOnlyList<TranscriptionLanguageOption> All { get; } =
    [
        new("auto", "TranscriptionLanguageAuto", string.Empty),
        new("ru", "TranscriptionLanguageRussian", "ru"),
        new("uk", "TranscriptionLanguageUkrainian", "uk"),
        new("en", "TranscriptionLanguageEnglish", "en")
    ];
}
