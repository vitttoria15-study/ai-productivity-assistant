using System.Net;
using System.Text.Json;
using backend.Services.Ai;
using backend.Services.Ai.Providers;
using backend.Tests.Helpers;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Ai;

public class DialAiProviderTests
{
    [Fact]
    public async Task CompleteAsync_SendsCorrectRequestBodyToEndpoint()
    {
        // Arrange — capture the outgoing request via TestHttpMessageHandler
        HttpRequestMessage? captured = null;

        var handler = new TestHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"hello"}}]}""")
            };
        });

        var http    = new HttpClient(handler);
        var options = Options.Create(new AiOptions
        {
            Provider = "dial",
            Dial     = new AiOptions.DialOptions
            {
                Endpoint = "https://dial.example.com/chat/completions",
                Model    = "gpt-4o",
            }
        });

        var provider = new DialAiProvider(http, options);

        // Act
        var result = await provider.CompleteAsync("sys", "usr");

        // Assert
        Assert.Equal("hello", result);
        Assert.NotNull(captured);

        var body = await captured!.Content!.ReadAsStringAsync();
        var doc  = JsonDocument.Parse(body);
        Assert.Equal("gpt-4o", doc.RootElement.GetProperty("model").GetString());

        var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("sys",    messages[0].GetProperty("content").GetString());
        Assert.Equal("user",   messages[1].GetProperty("role").GetString());
        Assert.Equal("usr",    messages[1].GetProperty("content").GetString());
    }
}
