namespace VoiceButton.Models;

public sealed record InterfaceLanguageOption(string Id, string Label)
{
    public static IReadOnlyList<InterfaceLanguageOption> All { get; } =
    [
        new("ru", "Русский"),
        new("uk", "Українська"),
        new("en", "English")
    ];

    public static string DetectWindowsLanguage()
    {
        var language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return language switch
        {
            "ru" => "ru",
            "uk" => "uk",
            _ => "en"
        };
    }

    public override string ToString() => Label;
}
