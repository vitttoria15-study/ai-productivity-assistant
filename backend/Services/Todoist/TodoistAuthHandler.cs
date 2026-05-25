using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace backend.Services.Todoist;

/// <summary>
/// Single source of truth for missing-token validation.
/// Throws InvalidOperationException before the HTTP call if ApiToken is empty,
/// then injects the Bearer header on every outgoing request.
/// TodoistService does NOT duplicate this check.
/// </summary>
public sealed class TodoistAuthHandler : DelegatingHandler
{
    private readonly IOptions<TodoistOptions> _options;

    public TodoistAuthHandler(IOptions<TodoistOptions> options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Value.ApiToken))
            throw new InvalidOperationException("Todoist ApiToken is not configured");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.Value.ApiToken);
        return base.SendAsync(request, cancellationToken);
    }
}
