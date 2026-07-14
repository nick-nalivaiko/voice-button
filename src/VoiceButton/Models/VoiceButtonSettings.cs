namespace VoiceButton.Models;

public sealed class VoiceButtonSettings
{
    public string Model { get; set; } = "gpt-4o-mini-tts";

    public string Voice { get; set; } = "marin";

    public double Speed { get; set; } = 1.0;

    public string ResponseFormat { get; set; } = "mp3";

    public string Instructions { get; set; } = "Говорить спокойно, естественно, на русском языке.";

    public int MaxChunkLength { get; set; } = 3900;
}
