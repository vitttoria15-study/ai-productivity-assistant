namespace backend.Services.Ai.Providers;

public class MockAiProvider : IAiProvider
{
    private const string MockJson = """
        {
          "completed_tasks": [],
          "new_tasks": ["Fix frontend validation"],
          "blockers": [],
          "priorities": ["Prepare demo"],
          "summary": "User worked on project setup."
        }
        """;

    public Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default)
        => Task.FromResult(MockJson);
}
