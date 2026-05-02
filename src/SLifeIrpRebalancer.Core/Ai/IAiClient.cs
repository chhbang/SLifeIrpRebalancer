namespace SLifeIrpRebalancer.Core.Ai;

public interface IAiClient
{
    string ProviderName { get; }
    string ModelId { get; }

    Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}

public sealed class AiClientException : Exception
{
    public AiClientException(string message) : base(message) { }
    public AiClientException(string message, Exception inner) : base(message, inner) { }
}
