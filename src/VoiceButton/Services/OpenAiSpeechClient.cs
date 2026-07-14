using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using VoiceButton.Models;

namespace VoiceButton.Services;

public sealed class OpenAiSpeechClient(HttpClient httpClient)
{
    public static bool HasUsableApiKey()
    {
        return GetApiKey() is not null;
    }

    public static string? GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return key.Contains("PASTE_YOUR_OPENAI_API_KEY_HERE", StringComparison.OrdinalIgnoreCase)
            ? null
            : key.Trim();
    }

    public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey();
        if (apiKey is null)
        {
            return new ApiKeyValidationResult(false, "OPENAI_API_KEY не задан.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return new ApiKeyValidationResult(true, "OpenAI API key принят сервером.");
        }

        return new ApiKeyValidationResult(false, $"OpenAI вернул {(int)response.StatusCode}: {TrimError(body)}");
    }

    public async Task<byte[]> CreateSpeechAsync(string text, VoiceButtonSettings settings, CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey()
            ?? throw new InvalidOperationException("OPENAI_API_KEY не задан. Вставь ключ в разделе Озвучка.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new SpeechRequest(
            settings.Model,
            text,
            settings.Voice,
            SupportsInstructions(settings.Model) ? settings.Instructions : null,
            settings.ResponseFormat,
            settings.Speed));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI TTS вернул {(int)response.StatusCode}: {TrimError(body)}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static bool SupportsInstructions(string model)
    {
        return model.StartsWith("gpt-4o-mini-tts", StringComparison.OrdinalIgnoreCase);
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

    private sealed record SpeechRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("voice")] string Voice,
        [property: JsonPropertyName("instructions")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Instructions,
        [property: JsonPropertyName("response_format")] string ResponseFormat,
        [property: JsonPropertyName("speed")] double Speed);
}

public sealed record ApiKeyValidationResult(bool IsValid, string Detail);
