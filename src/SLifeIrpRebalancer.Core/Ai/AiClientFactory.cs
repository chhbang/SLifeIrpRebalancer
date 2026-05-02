namespace SLifeIrpRebalancer.Core.Ai;

public static class AiClientFactory
{
    /// <summary>
    /// Creates an <see cref="IAiClient"/> for the given provider label as persisted by SettingsService:
    /// "Claude" / "Gemini" / "GPT". Throws on unknown labels so misconfiguration surfaces early.
    /// </summary>
    public static IAiClient Create(string providerName, string apiKey)
        => providerName switch
        {
            "Claude" => new AnthropicClient(apiKey),
            "Gemini" => new GeminiClient(apiKey),
            "GPT" => new OpenAiClient(apiKey),
            _ => throw new ArgumentException($"알 수 없는 AI 공급자: {providerName}", nameof(providerName)),
        };
}
