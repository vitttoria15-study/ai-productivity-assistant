// backend.Tests/Services/Ai/AiDiagnosticsServiceTests.cs
using System.Net;
using backend.Services.Ai;
using backend.Services.Ai.Providers;
using backend.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Ai;

public class AiDiagnosticsServiceTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    /// <summary>Builds AiDiagnosticsService with a NullLogger (no test output noise).</summary>
    private static AiDiagnosticsService BuildService(IAiProvider provider, AiOptions options)
        => new(provider, Options.Create(options), NullLogger<AiDiagnosticsService>.Instance);

    /// <summary>Creates a DialAiProvider wired to the given handler and dial options.</summary>
    private static DialAiProvider DialWith(TestHttpMessageHandler handler, AiOptions.DialOptions dial)
        => new(new HttpClient(handler), Options.Create(new AiOptions { Provider = "dial", Dial = dial }));

    // Fully-configured DIAL options used by tests that exercise HTTP-level errors.
    private static AiOptions.DialOptions ValidDial => new()
    {
        Endpoint = "https://dial.example.com/chat/completions",
        Model    = "gpt-4o",
        ApiKey   = "sk-test"
    };

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MockProvider_ReturnsSuccess_WithPreview()
    {
        var service = BuildService(
            new MockAiProvider(),
            new AiOptions { Provider = "mock" });

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.True(result.Configured);
        Assert.True(result.Success);
        Assert.Null(result.StatusCode);
        Assert.NotNull(result.RawResponsePreview);
        Assert.True(result.RawResponsePreview!.Length <= 100);
        Assert.True(result.LatencyMs >= 0);
        Assert.True(result.TimestampUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task DialMissingApiKey_ConfiguredFalse_NoHttpCall()
    {
        var httpCalled = false;
        var handler = new TestHttpMessageHandler(req =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var missingKeyDial = new AiOptions.DialOptions
        {
            Endpoint = "https://dial.example.com/chat/completions",
            Model    = "gpt-4o",
            ApiKey   = ""   // deliberately missing
        };
        var opts    = new AiOptions { Provider = "dial", Dial = missingKeyDial };
        var service = BuildService(DialWith(handler, missingKeyDial), opts);

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.False(result.Configured);
        Assert.False(result.Success);
        Assert.Equal(0, result.LatencyMs);
        Assert.False(httpCalled, "No HTTP call should be made when API key is missing");
        Assert.Contains("Ai:Dial:ApiKey", result.Message);
    }

    [Fact]
    public async Task OllamaMissingEndpoint_ConfiguredFalse_NoHttpCall()
    {
        var httpCalled = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var ollamaOpts = new AiOptions.OllamaOptions { Endpoint = "", Model = "llama3" };

        // OllamaProvider requires a BaseAddress on the HttpClient at construction;
        // we set a dummy one because the service must skip the call before reaching it.
        var provider = new OllamaProvider(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") },
            Options.Create(new AiOptions { Provider = "ollama", Ollama = ollamaOpts }));

        var opts   = new AiOptions { Provider = "ollama", Ollama = ollamaOpts };
        var result = await BuildService(provider, opts).CheckAsync(CancellationToken.None);

        Assert.False(result.Configured);
        Assert.False(result.Success);
        Assert.Equal(0, result.LatencyMs);
        Assert.False(httpCalled);
        Assert.Contains("Ai:Ollama:Endpoint", result.Message);
    }

    [Fact]
    public async Task HttpRequest_401_MapsToUnauthorized()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var dial    = ValidDial;
        var service = BuildService(
            DialWith(handler, dial),
            new AiOptions { Provider = "dial", Dial = dial });

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.True(result.Configured);
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("Unauthorized", result.Message);
        Assert.Equal("HTTP 401", result.RawResponsePreview);
    }

    [Fact]
    public async Task HttpRequest_NullStatusCode_MapsToNetworkError()
    {
        var handler = new TestHttpMessageHandler(
            (Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
                throw new HttpRequestException("Connection refused", inner: null, statusCode: null)));

        var dial    = ValidDial;
        var service = BuildService(
            DialWith(handler, dial),
            new AiOptions { Provider = "dial", Dial = dial });

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.True(result.Configured);
        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Contains("Network error", result.Message);
        Assert.Null(result.RawResponsePreview);
    }

    [Fact]
    public async Task TaskCanceled_MapsToTimeout()
    {
        var handler = new TestHttpMessageHandler(
            (Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
                throw new TaskCanceledException("Simulated timeout")));

        var dial    = ValidDial;
        var service = BuildService(
            DialWith(handler, dial),
            new AiOptions { Provider = "dial", Dial = dial });

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.True(result.Configured);
        Assert.False(result.Success);
        Assert.Null(result.StatusCode);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.RawResponsePreview);
    }

    [Fact]
    public async Task HttpRequest_400_MapsToModelError()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest));

        var dial    = ValidDial;
        var service = BuildService(
            DialWith(handler, dial),
            new AiOptions { Provider = "dial", Dial = dial });

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Model", result.Message);
        Assert.Equal("HTTP 400", result.RawResponsePreview);
    }

    [Fact]
    public async Task SuccessResponse_PreviewTruncatedTo100Chars()
    {
        // The DIAL provider parses the JSON and returns the "content" field value.
        // We embed a 150-char string there; the service must truncate it to 100.
        var longContent  = new string('x', 150);
        var responseJson = "{\"choices\":[{\"message\":{\"content\":\"" + longContent + "\"}}]}";

        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });

        var dial    = ValidDial;
        var service = BuildService(
            DialWith(handler, dial),
            new AiOptions { Provider = "dial", Dial = dial });

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.RawResponsePreview);
        Assert.Equal(100, result.RawResponsePreview!.Length);
    }
}
