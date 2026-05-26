using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace backend.Services.Ai.Providers;

public class DialAuthHandler : DelegatingHandler
{
    private readonly string _apiKey;

    public DialAuthHandler(IOptions<AiOptions> options)
    {
        _apiKey = options.Value.Dial.ApiKey;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_apiKey))
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        return base.SendAsync(request, ct);
    }
}
