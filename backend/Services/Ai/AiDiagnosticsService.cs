// backend/Services/Ai/AiDiagnosticsService.cs
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Options;

namespace backend.Services.Ai;

public class AiDiagnosticsService
{
    // Minimal, provider-agnostic probe — identical for Dial, Ollama, and any
    // OpenAI-compatible endpoint. Makes no domain assumptions.
    private const string ProbeSystem = "You are a health check. Reply with JSON only.";
    private const string ProbeUser   = """Return {"status":"ok"}.""";

    private readonly IAiProvider                   _provider;
    private readonly AiOptions                     _options;
    private readonly ILogger<AiDiagnosticsService> _logger;

    public AiDiagnosticsService(
        IAiProvider provider,
        IOptions<AiOptions> options,
        ILogger<AiDiagnosticsService> logger)
    {
        _provider = provider;
        _options  = options.Value;
        _logger   = logger;
    }

    public async Task<AiHealthResponse> CheckAsync(CancellationToken ct = default)
    {
        var providerName = _options.Provider;
        var timestamp    = DateTime.UtcNow;

        // ── 1. Config check — skip HTTP if misconfigured ──────────────────────
        var (isConfigured, missingReason) = AiProviderConfigValidator.Check(_options);
        if (!isConfigured)
        {
            _logger.LogInformation(
                "AI health check: provider={Provider} configured={Configured} success={Success} latencyMs={LatencyMs} reason={Reason}",
                providerName, false, false, 0, missingReason);

            return new AiHealthResponse
            {
                Provider     = providerName,
                Configured   = false,
                Success      = false,
                LatencyMs    = 0,
                TimestampUtc = timestamp,
                Message      = missingReason ?? "Provider is not configured.",
            };
        }

        // ── 2. Probe ──────────────────────────────────────────────────────────
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await _provider.CompleteAsync(ProbeSystem, ProbeUser, ct);
            sw.Stop();

            var preview = response.Length > 100 ? response[..100] : response;

            _logger.LogInformation(
                "AI health check: provider={Provider} configured={Configured} success={Success} latencyMs={LatencyMs} reason={Reason}",
                providerName, true, true, sw.ElapsedMilliseconds, "ok");

            return new AiHealthResponse
            {
                Provider           = providerName,
                Configured         = true,
                Success            = true,
                LatencyMs          = sw.ElapsedMilliseconds,
                TimestampUtc       = timestamp,
                Message            = "Provider responded successfully.",
                RawResponsePreview = preview,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            var (statusCode, message) = Classify(ex);
            var preview = statusCode.HasValue ? $"HTTP {(int)statusCode}" : null;

            _logger.LogInformation(
                "AI health check: provider={Provider} configured={Configured} success={Success} latencyMs={LatencyMs} reason={Reason}",
                providerName, true, false, sw.ElapsedMilliseconds, message);

            return new AiHealthResponse
            {
                Provider           = providerName,
                Configured         = true,
                Success            = false,
                StatusCode         = statusCode,
                LatencyMs          = sw.ElapsedMilliseconds,
                TimestampUtc       = timestamp,
                Message            = message,
                RawResponsePreview = preview,
            };
        }
    }

    // ── Exception → diagnostic mapping ────────────────────────────────────────
    // IMPORTANT: never include ex.Message — it can contain raw URLs with embedded
    // API keys (common in some OpenAI-compatible gateways that append ?api-key=...).
    private static (int? StatusCode, string Message) Classify(Exception ex) => ex switch
    {
        TaskCanceledException =>
            (null, "Request timed out."),

        HttpRequestException { StatusCode: null } =>
            (null, "Network error — cannot reach provider endpoint. Check Ai:Dial:Endpoint / Ai:Ollama:Endpoint."),

        HttpRequestException { StatusCode: HttpStatusCode.BadRequest } =>
            (400, "Bad request — check Model/Deployment name (Ai:Dial:Model or Ai:Ollama:Model)."),

        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } =>
            (401, "Unauthorized — API key is set but rejected. Check Ai:Dial:ApiKey."),

        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } =>
            (403, "Forbidden — key valid but access denied for this resource."),

        HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
            (404, "Not found — check endpoint URL (Ai:Dial:Endpoint or Ai:Ollama:Endpoint)."),

        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            (429, "Rate limit exceeded."),

        HttpRequestException { StatusCode: { } code } when (int)code >= 500 =>
            ((int)code, $"Provider server error ({(int)code})."),

        HttpRequestException { StatusCode: { } code } =>
            ((int)code, $"HTTP error {(int)code}."),

        InvalidOperationException =>
            (null, "Provider returned an unexpected response shape."),

        _ =>
            (null, $"Unexpected error: {ex.GetType().Name}")
    };
}
