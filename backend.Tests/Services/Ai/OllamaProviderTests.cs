using System.Net;
using System.Text.Json;
using backend.Services.Ai;
using backend.Services.Ai.Providers;
using backend.Tests.Helpers;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Ai;

public class OllamaProviderTests
{
    [Fact]
    public async Task CompleteAsync_SendsRequestToApiChatWithStreamFalse()
    {
        // Arrange — capture the outgoing request via TestHttpMessageHandler
        HttpRequestMessage? captured = null;

        var handler = new TestHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"message":{"content":"ollama reply"}}""")
            };
        });

        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };
        var options = Options.Create(new AiOptions
        {
            Ollama = new AiOptions.OllamaOptions { Model = "llama3" }
        });

        var provider = new OllamaProvider(http, options);
        var result   = await provider.CompleteAsync("sys", "usr");

        Assert.Equal("ollama reply", result);
        Assert.NotNull(captured);
        Assert.Equal("api/chat", captured!.RequestUri?.PathAndQuery.TrimStart('/'));

        var body = await captured.Content!.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(body);
        Assert.Equal("llama3", doc.RootElement.GetProperty("model").GetString());
        Assert.False(doc.RootElement.GetProperty("stream").GetBoolean());

        var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user",   messages[1].GetProperty("role").GetString());
    }
}
