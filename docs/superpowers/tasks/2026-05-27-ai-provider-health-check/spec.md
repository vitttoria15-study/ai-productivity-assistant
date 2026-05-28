# Spec — AI Provider Diagnostic Health-Check Endpoint

**Slug:** ai-provider-health-check  
**Date:** 2026-05-27  
**Branch:** feature/ai-provider-health-check  
**Status:** pending review

---

## Goal

Add a backend diagnostic endpoint (`GET /api/ai/health`) that verifies whether the currently configured AI provider is reachable and responding. The endpoint is intended for local development and staging debugging only — it never returns secrets, API keys, or authorization headers.

This task is **diagnostic only** and must not alter the journal extraction flow or any existing provider behaviour.

---

## Context

The project uses a provider abstraction (`IAiProvider`) with three concrete implementations:

| Provider | Selected by `Ai:Provider` |
|----------|--------------------------|
| `DialAiProvider` | `"dial"` |
| `OllamaProvider` | `"ollama"` |
| `MockAiProvider` | `"mock"` |

Configuration lives in `AiOptions` (section `"Ai"`). The Dial API key is supplied via .NET user-secrets or the `Ai__Dial__ApiKey` environment variable — never from `appsettings.json`.

`IAiProvider.CompleteAsync` throws:
- `HttpRequestException` (with `.StatusCode` populated in .NET 8) on HTTP-level failures
- `TaskCanceledException` on timeout
- `InvalidOperationException` when response body parsing fails
- Other `Exception` subtypes for unexpected failures

---

## Scope

**In scope:**
- New `GET /api/ai/health` controller endpoint
- New `AiDiagnosticsService` that orchestrates the probe, timing, and error classification
- New shared `AiProviderConfigValidator` helper (replaces the inline check currently in `Program.cs`)
- Unit tests for `AiDiagnosticsService` covering configuration checks and exception mapping
- Updating `Program.cs` to use `AiProviderConfigValidator` (removes duplicate logic)

**Out of scope:**
- No database migrations
- No frontend changes
- No changes to `IAiProvider`, `DialAiProvider`, `OllamaProvider`, or `MockAiProvider`
- No changes to `JournalExtractionService` or `JournalController`
- No new NuGet packages

---

## New Files

```
backend/
  Services/Ai/
    AiProviderConfigValidator.cs   ← shared config check; replaces inline check in Program.cs
    AiDiagnosticsService.cs        ← probe + timing + error classification
    AiHealthResponse.cs            ← response DTO
  Controllers/
    AiHealthController.cs          ← GET /api/ai/health
backend.Tests/Services/Ai/
  AiDiagnosticsServiceTests.cs     ← unit tests
```

---

## Response DTO

```json
{
  "provider":           "dial",
  "configured":         true,
  "success":            false,
  "statusCode":         401,
  "latencyMs":          312,
  "timestampUtc":       "2026-05-27T14:03:00.000Z",
  "message":            "Unauthorized — API key is set but rejected. Check Ai:Dial:ApiKey.",
  "rawResponsePreview": "HTTP 401 Unauthorized"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `provider` | `string` | Value of `AiOptions.Provider` |
| `configured` | `bool` | `true` when all required config values are non-empty (see below) |
| `success` | `bool` | `true` when `CompleteAsync` returned without throwing |
| `statusCode` | `int?` | HTTP status code from `HttpRequestException.StatusCode`; null for network errors, timeouts, or success |
| `latencyMs` | `long` | Wall-clock milliseconds for the `CompleteAsync` call; 0 if skipped (not configured) |
| `timestampUtc` | `DateTime` | UTC timestamp of when the check was performed (`DateTime.UtcNow`) |
| `message` | `string` | Human-readable diagnostic, references config paths not values |
| `rawResponsePreview` | `string?` | First 100 chars of the provider's successful response. On HTTP failure: `"HTTP {statusCode}"` (e.g. `"HTTP 401"`). On network/timeout/other: `null`. Never contains secrets. |

---

## AiProviderConfigValidator

A `static` helper with a single method:

```csharp
public static (bool IsConfigured, string? MissingReason) Check(AiOptions opts)
```

Rules:

| Provider | IsConfigured = true when |
|----------|--------------------------|
| `dial` | `Dial.Endpoint`, `Dial.Model`, and `Dial.ApiKey` are all non-empty |
| `ollama` | `Ollama.Endpoint` and `Ollama.Model` are both non-empty |
| `mock` | always `true` |

`MissingReason` is a short human-safe string naming which config key is missing (e.g. `"Ai:Dial:ApiKey is not set"`). It never includes the key value.

**Usage in `Program.cs`** — replaces the existing inline DIAL key check:
```csharp
var (configured, reason) = AiProviderConfigValidator.Check(aiOpts);
if (!configured)
    startupLogger.LogWarning("AI provider misconfigured: {Reason}", reason);
```

---

## AiDiagnosticsService

```csharp
public class AiDiagnosticsService(IAiProvider provider, IOptions<AiOptions> options,
                                  ILogger<AiDiagnosticsService> logger)
{
    Task<AiHealthResponse> CheckAsync(CancellationToken ct);
}
```

### Probe prompt

The service sends a minimal, provider-agnostic completion to verify end-to-end connectivity:

```
system: "You are a health check. Reply with JSON only."
user:   "Return {\"status\":\"ok\"}."
```

This prompt is intentionally tiny and makes no domain assumptions — it works identically for Dial, Ollama, and any OpenAI-compatible endpoint.

### Flow

1. Call `AiProviderConfigValidator.Check(options.Value)`.
2. If `!IsConfigured`: return `{configured: false, success: false, latencyMs: 0, message: MissingReason, ...}` without making any HTTP call.
3. Start `Stopwatch`.
4. Call `IAiProvider.CompleteAsync(systemPrompt, userMessage, ct)`.
5. Stop stopwatch. Record `latencyMs`.
6. On success: `success = true`, `rawResponsePreview = response[..Math.Min(100, response.Length)]`.
7. On exception: classify (see table below) into `message` and optional `statusCode`.
8. Log one structured line (no secrets).
9. Return `AiHealthResponse`.

### Exception classification

| Exception | `statusCode` | `message` |
|-----------|-------------|-----------|
| `TaskCanceledException` | null | `"Request timed out"` |
| `HttpRequestException`, `StatusCode == null` | null | `"Network error — cannot reach {provider} endpoint. Check Ai:Dial:Endpoint / Ai:Ollama:Endpoint."` |
| `HttpRequestException`, `400` | 400 | `"Bad request — check Model/Deployment name (Ai:Dial:Model or Ai:Ollama:Model)."` |
| `HttpRequestException`, `401` | 401 | `"Unauthorized — API key is set but rejected. Check Ai:Dial:ApiKey."` |
| `HttpRequestException`, `403` | 403 | `"Forbidden — key valid but access denied for this resource."` |
| `HttpRequestException`, `404` | 404 | `"Not found — check endpoint URL (Ai:Dial:Endpoint or Ai:Ollama:Endpoint)."` |
| `HttpRequestException`, `429` | 429 | `"Rate limit exceeded."` |
| `HttpRequestException`, `>= 500` | code | `"Provider server error ({code})."` |
| `HttpRequestException`, other | code | `"HTTP error {code}."` |
| `InvalidOperationException` | null | `"Provider returned an unexpected response shape."` |
| other `Exception` | null | `"Unexpected error: {ExceptionTypeName}"` (type name only, never `.Message`) |

> **Secret safety:** `message` and `rawResponsePreview` are built from hardcoded strings and config *path names* only — never from config values, `ex.Message`, or request/response bodies. `ex.Message` can contain raw URLs (which may embed API keys via query strings in some gateways).

### Structured log line

```
AI health check: provider={Provider} configured={Configured} success={Success} latencyMs={LatencyMs} reason={Reason}
```

No `ApiKey`, no `Authorization`, no raw exception message.

---

## AiHealthController

```csharp
[ApiController]
[Route("api/ai")]
public class AiHealthController(AiDiagnosticsService diagnostics,
                                IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthAsync(CancellationToken ct)
```

- If `!env.IsDevelopment()` → return `NotFound()` (HTTP 404). This hides the endpoint in production and staging.
- Otherwise: call `diagnostics.CheckAsync(ct)`, return `Ok(result)`.
- HTTP status is always **200 OK** when the endpoint is accessible — the `success` field in the JSON body carries the AI call outcome.

---

## Tests (AiDiagnosticsServiceTests)

| Test name | Scenario |
|-----------|----------|
| `MockProvider_ReturnsSuccess_WithPreview` | `MockAiProvider` succeeds; `success = true`, preview populated |
| `DialMissingApiKey_ConfiguredFalse_NoHttpCall` | `Dial.ApiKey` empty; returns immediately, `configured = false`, `latencyMs = 0` |
| `OllamaMissingEndpoint_ConfiguredFalse` | `Ollama.Endpoint` empty; `configured = false` |
| `HttpRequest_401_MapsToUnauthorized` | `TestHttpMessageHandler` returns 401; message contains "Unauthorized" |
| `HttpRequest_NullStatusCode_MapsToNetworkError` | Handler throws `HttpRequestException` with null status; message contains "Network error" |
| `TaskCanceled_MapsToTimeout` | Handler throws `TaskCanceledException`; message = "Request timed out" |
| `HttpRequest_400_MapsToModelError` | Returns 400; message references `:Model` config key |
| `SuccessResponse_PreviewTruncatedTo100Chars` | Provider returns 150-char string; preview is exactly 100 chars |

Tests use the existing `TestHttpMessageHandler` helper and `Moq` where needed. No new test packages required.

---

## Program.cs Change

Replace the existing inline DIAL key check:

```csharp
// before
if (aiOpts.Provider.Equals("dial", ...) && string.IsNullOrWhiteSpace(aiOpts.Dial.ApiKey))
    startupLogger.LogWarning(...);

// after
var (aiConfigured, aiMissingReason) = AiProviderConfigValidator.Check(aiOpts);
if (!aiConfigured)
    startupLogger.LogWarning("AI provider misconfigured at startup: {Reason}", aiMissingReason);
```

This is the only change to existing `Program.cs` logic — no other existing files are modified.

---

## Acceptance Criteria

1. `GET /api/ai/health` returns 404 in any non-Development environment.
2. `GET /api/ai/health` returns 200 with well-formed JSON in Development.
3. When `Ai:Provider = mock`, response has `configured: true`, `success: true`.
4. When `Ai:Provider = dial` and `Ai:Dial:ApiKey` is blank, response has `configured: false`, `success: false`, no HTTP call made.
5. When DIAL returns HTTP 401, response has `success: false`, `statusCode: 401`, `message` contains "Unauthorized".
6. `rawResponsePreview` is never longer than 100 characters.
7. `rawResponsePreview` and `message` never contain the API key string.
8. All 8 specified unit tests pass.
9. `dotnet build` and `dotnet test` pass with no new warnings.
10. `Program.cs` startup check uses `AiProviderConfigValidator` (no duplicated inline logic).
