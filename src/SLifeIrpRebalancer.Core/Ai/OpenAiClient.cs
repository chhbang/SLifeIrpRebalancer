using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SLifeIrpRebalancer.Core.Ai;

/// <summary>
/// Calls OpenAI's chat completions endpoint at https://api.openai.com/v1/chat/completions.
/// </summary>
public sealed class OpenAiClient : IAiClient
{
    public const string DefaultModel = "gpt-5";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
    };

    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiClient(string apiKey, string? model = null)
    {
        _apiKey = apiKey;
        _model = model ?? DefaultModel;
    }

    public string ProviderName => "GPT";
    public string ModelId => _model;

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var body = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
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
            throw new AiClientException($"OpenAI API 요청 실패: {ex.Message}", ex);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new AiClientException($"OpenAI API 오류 ({(int)response.StatusCode}): {Truncate(json)}");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
                throw new AiClientException("OpenAI 응답이 비어 있습니다.");
            return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is not AiClientException)
        {
            throw new AiClientException($"OpenAI 응답 파싱 실패: {ex.Message}. 본문: {Truncate(json)}", ex);
        }
    }

    private static string Truncate(string s, int max = 500)
        => s.Length <= max ? s : s[..max] + "...";
}
