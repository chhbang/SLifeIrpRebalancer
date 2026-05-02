using System.Net.Http.Json;
using System.Text.Json;

namespace SLifeIrpRebalancer.Core.Ai;

/// <summary>
/// Calls Google Gemini's generateContent endpoint at
/// https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent.
/// </summary>
public sealed class GeminiClient : IAiClient
{
    public const string DefaultModel = "gemini-2.5-pro";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
    };

    private readonly string _apiKey;
    private readonly string _model;

    public GeminiClient(string apiKey, string? model = null)
    {
        _apiKey = apiKey;
        _model = model ?? DefaultModel;
    }

    public string ProviderName => "Gemini";
    public string ModelId => _model;

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={Uri.EscapeDataString(_apiKey)}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var body = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } },
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } },
                },
            },
        };
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiClientException($"Gemini API 요청 실패: {ex.Message}", ex);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new AiClientException($"Gemini API 오류 ({(int)response.StatusCode}): {Truncate(json)}");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0)
                throw new AiClientException("Gemini 응답에 candidates가 없습니다.");
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            if (parts.GetArrayLength() == 0)
                throw new AiClientException("Gemini 응답이 비어 있습니다.");
            return parts[0].GetProperty("text").GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is not AiClientException)
        {
            throw new AiClientException($"Gemini 응답 파싱 실패: {ex.Message}. 본문: {Truncate(json)}", ex);
        }
    }

    private static string Truncate(string s, int max = 500)
        => s.Length <= max ? s : s[..max] + "...";
}
