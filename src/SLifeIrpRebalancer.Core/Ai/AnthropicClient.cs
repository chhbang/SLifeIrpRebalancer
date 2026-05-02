using System.Net.Http.Json;
using System.Text.Json;

namespace SLifeIrpRebalancer.Core.Ai;

/// <summary>
/// Calls Anthropic's Messages API at https://api.anthropic.com/v1/messages.
/// Defaults to Claude Opus 4.7 (the latest as of 2026). Override the model id
/// via constructor when the user upgrades or wants a faster/cheaper tier.
/// </summary>
public sealed class AnthropicClient : IAiClient
{
    public const string DefaultModel = "claude-opus-4-7";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
    };

    private readonly string _apiKey;
    private readonly string _model;

    public AnthropicClient(string apiKey, string? model = null)
    {
        _apiKey = apiKey;
        _model = model ?? DefaultModel;
    }

    public string ProviderName => "Claude";
    public string ModelId => _model;

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model = _model,
            max_tokens = 8192,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt },
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
            throw new AiClientException($"Anthropic API 요청 실패: {ex.Message}", ex);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new AiClientException($"Anthropic API 오류 ({(int)response.StatusCode}): {Truncate(json)}");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var contentArray = doc.RootElement.GetProperty("content");
            if (contentArray.GetArrayLength() == 0)
                throw new AiClientException("Anthropic 응답이 비어 있습니다.");
            return contentArray[0].GetProperty("text").GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is not AiClientException)
        {
            throw new AiClientException($"Anthropic 응답 파싱 실패: {ex.Message}. 본문: {Truncate(json)}", ex);
        }
    }

    private static string Truncate(string s, int max = 500)
        => s.Length <= max ? s : s[..max] + "...";
}
