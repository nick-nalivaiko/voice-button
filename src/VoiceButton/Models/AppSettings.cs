namespace VoiceButton.Models;

public sealed class AppSettings
{
    public string InterfaceLanguage { get; set; } = "ru";

    public string SpeechModel { get; set; } = "gpt-4o-mini-tts";

    public string Voice { get; set; } = "marin";

    public double SpeechSpeed { get; set; } = 1.0;

    public string SpeakLatestHotkey { get; set; } = "Ctrl+Alt+V";

    public string SpeakClipboardHotkey { get; set; } = string.Empty;

    public string CodexMicHotkey { get; set; } = string.Empty;

    public string CodexWindowKeywords { get; set; } = "Codex";

    public bool HoverToRevealCopyButton { get; set; } = true;

    public bool RestoreClipboardAfterCopy { get; set; } = true;

    public bool FallbackToClipboardWhenCopyMissing { get; set; }

    public bool RetryMicrophoneIfInactive { get; set; } = true;

    public bool HideFilePathsInSpeech { get; set; } = true;

    public bool HideCodeBlocksInSpeech { get; set; } = true;

    public bool HideInlineCodeInSpeech { get; set; }

    public bool ShortenLinksInSpeech { get; set; } = true;

    public bool HideSecretsInSpeech { get; set; } = true;

    public bool ShortenHashesInSpeech { get; set; } = true;

    public bool CollapseStackTracesInSpeech { get; set; } = true;

    public bool RemoveMarkdownNoiseInSpeech { get; set; } = true;

    public bool CollapseTablesInSpeech { get; set; } = true;

    public bool CollapseStructuredDataInSpeech { get; set; } = true;

    public bool ShortenShellCommandsInSpeech { get; set; } = true;

    public bool HideLongNumbersInSpeech { get; set; } = true;

    public bool ShowFloatingButton { get; set; } = true;

    public bool MinimizeToTray { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public bool RememberFloatingButtonPosition { get; set; } = true;

    public double? FloatingButtonLeft { get; set; }

    public double? FloatingButtonTop { get; set; }
}
