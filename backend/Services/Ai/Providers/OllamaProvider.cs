using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace backend.Services.Ai.Providers;

public class OllamaProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string     _model;

    public OllamaProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http  = http;
        _model = options.Value.Ollama.Model;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var body = new
        {
            model    = _model,
            stream   = false,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  },
            }
        };

        var response = await _http.PostAsJsonAsync("api/chat", body, ct);
        response.EnsureSuccessStatusCode();

        var parsed = await response.Content
            .ReadFromJsonAsync<OllamaChatResponse>(ct);

        return parsed?.Message?.Content
            ?? throw new InvalidOperationException(
                "Ollama response did not contain a message content.");
    }

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("content")] string? Content);
}
