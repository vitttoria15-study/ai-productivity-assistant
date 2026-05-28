using System.Text.Json;
using backend.Services.Ai;
using backend.Services.Ai.Providers;
using Xunit;

namespace backend.Tests.Services.Ai;

public class MockAiProviderTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsValidJsonWithAllRequiredFields()
    {
        var provider = new MockAiProvider();

        var json = await provider.CompleteAsync("system", "user");

        using var doc = JsonDocument.Parse(json);  // throws if invalid JSON
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array,  root.GetProperty("completed_tasks").ValueKind);
        Assert.Equal(JsonValueKind.Array,  root.GetProperty("new_tasks").ValueKind);
        Assert.Equal(JsonValueKind.Array,  root.GetProperty("blockers").ValueKind);
        Assert.Equal(JsonValueKind.Array,  root.GetProperty("priorities").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("summary").ValueKind);
    }
}
