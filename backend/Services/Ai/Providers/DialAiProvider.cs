using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace backend.Services.Ai.Providers;

public class DialAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string     _endpoint;
    private readonly string     _model;

    public DialAiProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http     = http;
        _endpoint = options.Value.Dial.Endpoint;
        _model    = options.Value.Dial.Model;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var body = new
        {
            model    = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  },
            }
        };

        var response = await _http.PostAsJsonAsync(_endpoint, body, ct);
        response.EnsureSuccessStatusCode();

        var parsed = await response.Content
            .ReadFromJsonAsync<DialCompletionResponse>(ct);

        return parsed?.Choices?[0]?.Message?.Content
            ?? throw new InvalidOperationException(
                "DIAL response did not contain a message content.");
    }

    private sealed record DialCompletionResponse(
        [property: JsonPropertyName("choices")] DialChoice[]? Choices);

    private sealed record DialChoice(
        [property: JsonPropertyName("message")] DialMessage? Message);

    private sealed record DialMessage(
        [property: JsonPropertyName("content")] string? Content);
}
