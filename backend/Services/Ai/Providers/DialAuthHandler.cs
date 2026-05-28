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
            request.Headers.TryAddWithoutValidation("Api-Key", _apiKey);
        return base.SendAsync(request, ct);
    }
}
