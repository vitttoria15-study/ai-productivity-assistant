namespace backend.Tests.Helpers;

/// <summary>
/// A fake HttpMessageHandler that returns a pre-configured response.
/// Use this to unit-test classes that take an HttpClient without making real HTTP calls.
/// Supports both synchronous and async handler delegates.
/// </summary>
public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    /// <summary>Convenience overload for synchronous handlers.</summary>
    public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : this(req => Task.FromResult(handler(req))) { }

    /// <summary>Convenience overload: always return the same response.</summary>
    public TestHttpMessageHandler(HttpResponseMessage response)
        : this(_ => Task.FromResult(response)) { }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request);
}
