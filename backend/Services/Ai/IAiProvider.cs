namespace backend.Services.Ai;

public interface IAiProvider
{
    Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default);
}
