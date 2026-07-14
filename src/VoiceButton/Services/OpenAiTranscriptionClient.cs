using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VoiceButton.Services;

public sealed class OpenAiTranscriptionClient(HttpClient httpClient)
{
    public async Task<string> TranscribeAsync(
        byte[] wavAudio,
        string model,
        string? language,
        CancellationToken cancellationToken)
    {
        var apiKey = OpenAiSpeechClient.GetApiKey()
            ?? throw new InvalidOperationException("OPENAI_API_KEY не задан. Вставь ключ в разделе Озвучка.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(model), "model");
        content.Add(new StringContent("json"), "response_format");
        if (!string.IsNullOrWhiteSpace(language))
        {
            content.Add(new StringContent(language), "language");
        }

        var audioContent = new ByteArrayContent(wavAudio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "dictation.wav");
        request.Content = content;

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI STT вернул {(int)response.StatusCode}: {TrimError(body)}");
        }

        using var json = JsonDocument.Parse(body);
        var text = json.RootElement.TryGetProperty("text", out var textElement)
            ? textElement.GetString()?.Trim()
            : null;
        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidOperationException("OpenAI не вернул текст распознавания.");
    }

    private static string TrimError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "без текста ошибки";
        }

        body = body.ReplaceLineEndings(" ").Trim();
        return body.Length <= 320 ? body : body[..320] + "...";
    }
}