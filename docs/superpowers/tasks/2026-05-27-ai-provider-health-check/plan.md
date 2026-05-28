# AI Provider Health-Check Endpoint — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `GET /api/ai/health` — a Development-only diagnostic endpoint that probes the configured AI provider and returns a structured, secret-safe response.

**Architecture:** A new static `AiProviderConfigValidator` centralises config-presence checks (replaces the inline DIAL-key warning in `Program.cs` and is reused by the diagnostics service). `AiDiagnosticsService` calls `IAiProvider.CompleteAsync` with a minimal provider-agnostic probe, measures latency via `Stopwatch`, catches every exception, and classifies it into a human-safe message using `HttpRequestException.StatusCode` (.NET 8 built-in). `AiHealthController` checks `IWebHostEnvironment.IsDevelopment()` and returns 404 in non-Development; delegates to `AiDiagnosticsService` otherwise. No existing providers or the journal extraction flow are touched.

**Tech Stack:** ASP.NET Core 8, xUnit, `Microsoft.Extensions.Logging.Abstractions` (NullLogger — already transitive), `System.Diagnostics.Stopwatch`, `System.Net.HttpRequestException.StatusCode` — **no new NuGet packages**.

---

## File map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `backend/Services/Ai/AiHealthResponse.cs` | Response DTO |
| Create | `backend/Services/Ai/AiProviderConfigValidator.cs` | Static config-presence check |
| Create | `backend/Services/Ai/AiDiagnosticsService.cs` | Probe + timing + error classification |
| Create | `backend/Controllers/AiHealthController.cs` | Dev-only gate + HTTP surface |
| Create | `backend.Tests/Services/Ai/AiProviderConfigValidatorTests.cs` | 6 validator tests |
| Create | `backend.Tests/Services/Ai/AiDiagnosticsServiceTests.cs` | 8 service tests |
| Modify | `backend/Program.cs` | ① Swap inline DIAL check → `AiProviderConfigValidator.Check` ② Register `AiDiagnosticsService` |

---

## Task 1 — AiHealthResponse DTO

**Test-first: no** — pure data record, no behaviour.

**Files:**
- Create: `backend/Services/Ai/AiHealthResponse.cs`

- [ ] **Step 1: Create the DTO**

```csharp
// backend/Services/Ai/AiHealthResponse.cs
namespace backend.Services.Ai;

public sealed class AiHealthResponse
{
    public string   Provider           { get; init; } = string.Empty;
    public bool     Configured         { get; init; }
    public bool     Success            { get; init; }
    public int?     StatusCode         { get; init; }
    public long     LatencyMs          { get; init; }
    public DateTime TimestampUtc       { get; init; }
    public string   Message            { get; init; } = string.Empty;
    public string?  RawResponsePreview { get; init; }
}
```

- [ ] **Step 2: Verify it compiles**

```
cd backend
dotnet build
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Commit**

```
git add backend/Services/Ai/AiHealthResponse.cs
git commit -m "feat(ai-provider-health-check): add AiHealthResponse DTO"
```

---

## Task 2 — AiProviderConfigValidator (TDD)

**Test-first: yes** — tests verify `Check()` returns the correct `(bool IsConfigured, string? MissingReason)` tuple for all three providers and every missing-field combination.

**Files:**
- Create: `backend/Services/Ai/AiProviderConfigValidator.cs`
- Create: `backend.Tests/Services/Ai/AiProviderConfigValidatorTests.cs`
- Modify: `backend/Program.cs` (swap inline DIAL check)

- [ ] **Step 1: Write the 6 failing tests**

```csharp
// backend.Tests/Services/Ai/AiProviderConfigValidatorTests.cs
using backend.Services.Ai;

namespace backend.Tests.Services.Ai;

public class AiProviderConfigValidatorTests
{
    [Fact]
    public void Dial_AllFieldsSet_IsConfigured()
    {
        var opts = new AiOptions
        {
            Provider = "dial",
            Dial = new AiOptions.DialOptions
            {
                Endpoint = "https://dial.example.com/chat/completions",
                Model    = "gpt-4o",
                ApiKey   = "sk-test"
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.True(isConfigured);
        Assert.Null(reason);
    }

    [Fact]
    public void Dial_MissingApiKey_NotConfigured_ReasonMentionsKey()
    {
        var opts = new AiOptions
        {
            Provider = "dial",
            Dial = new AiOptions.DialOptions
            {
                Endpoint = "https://dial.example.com/chat/completions",
                Model    = "gpt-4o",
                ApiKey   = ""
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.False(isConfigured);
        Assert.NotNull(reason);
        Assert.Contains("Ai:Dial:ApiKey", reason);
    }

    [Fact]
    public void Dial_MissingEndpoint_NotConfigured_ReasonMentionsEndpoint()
    {
        var opts = new AiOptions
        {
            Provider = "dial",
            Dial = new AiOptions.DialOptions
            {
                Endpoint = "",
                Model    = "gpt-4o",
                ApiKey   = "sk-test"
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.False(isConfigured);
        Assert.Contains("Ai:Dial:Endpoint", reason!);
    }

    [Fact]
    public void Ollama_AllFieldsSet_IsConfigured()
    {
        var opts = new AiOptions
        {
            Provider = "ollama",
            Ollama = new AiOptions.OllamaOptions
            {
                Endpoint = "http://localhost:11434",
                Model    = "llama3"
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.True(isConfigured);
        Assert.Null(reason);
    }

    [Fact]
    public void Ollama_MissingEndpoint_NotConfigured_ReasonMentionsEndpoint()
    {
        var opts = new AiOptions
        {
            Provider = "ollama",
            Ollama = new AiOptions.OllamaOptions
            {
                Endpoint = "",
                Model    = "llama3"
            }
        };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.False(isConfigured);
        Assert.Contains("Ai:Ollama:Endpoint", reason!);
    }

    [Fact]
    public void Mock_AlwaysConfigured()
    {
        var opts = new AiOptions { Provider = "mock" };

        var (isConfigured, reason) = AiProviderConfigValidator.Check(opts);

        Assert.True(isConfigured);
        Assert.Null(reason);
    }
}
```

- [ ] **Step 2: Run tests — confirm RED**

```
cd backend.Tests
dotnet test --filter "AiProviderConfigValidatorTests" -v normal
```
Expected: build error `The type or namespace name 'AiProviderConfigValidator' could not be found`.

- [ ] **Step 3: Implement AiProviderConfigValidator**

```csharp
// backend/Services/Ai/AiProviderConfigValidator.cs
namespace backend.Services.Ai;

/// <summary>
/// Checks whether all required configuration values are present for the selected provider.
/// Returns (true, null) when fully configured; (false, reason) when something is missing.
/// The reason string references config key paths only — never their values.
/// </summary>
public static class AiProviderConfigValidator
{
    public static (bool IsConfigured, string? MissingReason) Check(AiOptions opts) =>
        opts.Provider.Trim().ToLowerInvariant() switch
        {
            "dial" when string.IsNullOrWhiteSpace(opts.Dial.Endpoint) =>
                (false, "Ai:Dial:Endpoint is not set"),
            "dial" when string.IsNullOrWhiteSpace(opts.Dial.Model) =>
                (false, "Ai:Dial:Model is not set"),
            "dial" when string.IsNullOrWhiteSpace(opts.Dial.ApiKey) =>
                (false, "Ai:Dial:ApiKey is not set"),
            "dial" =>
                (true, null),

            "ollama" when string.IsNullOrWhiteSpace(opts.Ollama.Endpoint) =>
                (false, "Ai:Ollama:Endpoint is not set"),
            "ollama" when string.IsNullOrWhiteSpace(opts.Ollama.Model) =>
                (false, "Ai:Ollama:Model is not set"),
            "ollama" =>
                (true, null),

            // "mock" and any unknown/future providers are treated as configured;
            // the switch in Program.cs handles unknown values at startup.
            _ => (true, null)
        };
}
```

- [ ] **Step 4: Run tests — confirm GREEN**

```
cd backend.Tests
dotnet test --filter "AiProviderConfigValidatorTests" -v normal
```
Expected: `Passed!  - Failed: 0, Passed: 6, Skipped: 0`.

- [ ] **Step 5: Update Program.cs — swap inline DIAL check**

Find this block in `backend/Program.cs` (around line 89):

```csharp
var aiOpts = app.Services.GetRequiredService<IOptions<AiOptions>>().Value;
if (aiOpts.Provider.Equals("dial", StringComparison.OrdinalIgnoreCase)
    && string.IsNullOrWhiteSpace(aiOpts.Dial.ApiKey))
    startupLogger.LogWarning(
        "Ai:Dial:ApiKey is not configured. " +
        "Set it via 'dotnet user-secrets set \"Ai:Dial:ApiKey\" \"sk-...\"' " +
        "or the Ai__Dial__ApiKey environment variable. " +
        "DIAL provider will fail at runtime.");
```

Replace it with:

```csharp
var aiOpts = app.Services.GetRequiredService<IOptions<AiOptions>>().Value;
var (aiConfigured, aiMissingReason) = AiProviderConfigValidator.Check(aiOpts);
if (!aiConfigured)
    startupLogger.LogWarning(
        "AI provider misconfigured at startup: {Reason}", aiMissingReason);
```

- [ ] **Step 6: Build to confirm the change compiles**

```
cd backend
dotnet build
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 7: Commit**

```
git add backend/Services/Ai/AiProviderConfigValidator.cs `
      backend.Tests/Services/Ai/AiProviderConfigValidatorTests.cs `
      backend/Program.cs
git commit -m "feat(ai-provider-health-check): add AiProviderConfigValidator; update Program.cs startup check"
```

---

## Task 3 — AiDiagnosticsService (TDD)

**Test-first: yes** — all 8 tests written and confirmed RED before any service code is written.

**Files:**
- Create: `backend/Services/Ai/AiDiagnosticsService.cs`
- Create: `backend.Tests/Services/Ai/AiDiagnosticsServiceTests.cs`

- [ ] **Step 1: Write all 8 failing tests**

```csharp
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
        var handler = new TestHttpMessageHandler(_ =>
            throw new HttpRequestException("Connection refused", inner: null, statusCode: null));

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
        var handler = new TestHttpMessageHandler(_ =>
            throw new TaskCanceledException("Simulated timeout"));

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
        var responseJson = $$"""{"choices":[{"message":{"content":"{{longContent}}"}}]}""";

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
```

- [ ] **Step 2: Run tests — confirm RED**

```
cd backend.Tests
dotnet test --filter "AiDiagnosticsServiceTests" -v normal
```
Expected: build error `The type or namespace name 'AiDiagnosticsService' could not be found`.

- [ ] **Step 3: Implement AiDiagnosticsService**

```csharp
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
```

- [ ] **Step 4: Run tests — confirm GREEN**

```
cd backend.Tests
dotnet test --filter "AiDiagnosticsServiceTests" -v normal
```
Expected: `Passed!  - Failed: 0, Passed: 8, Skipped: 0`.

- [ ] **Step 5: Commit**

```
git add backend/Services/Ai/AiDiagnosticsService.cs `
      backend.Tests/Services/Ai/AiDiagnosticsServiceTests.cs
git commit -m "feat(ai-provider-health-check): add AiDiagnosticsService with TDD (8 tests)"
```

---

## Task 4 — AiHealthController + DI registration

**Test-first: no** — the controller delegates entirely to `AiDiagnosticsService`; the dev-gate is a one-liner verified by `dotnet build` + the existing test suite.

**Files:**
- Create: `backend/Controllers/AiHealthController.cs`
- Modify: `backend/Program.cs` (register `AiDiagnosticsService`)

- [ ] **Step 1: Create AiHealthController**

```csharp
// backend/Controllers/AiHealthController.cs
using backend.Services.Ai;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/ai")]
public class AiHealthController : ControllerBase
{
    private readonly AiDiagnosticsService _diagnostics;
    private readonly IWebHostEnvironment  _env;

    public AiHealthController(AiDiagnosticsService diagnostics, IWebHostEnvironment env)
    {
        _diagnostics = diagnostics;
        _env         = env;
    }

    /// <summary>
    /// Diagnostic endpoint — probes the configured AI provider and returns a
    /// structured, secret-safe result. Only available in the Development environment;
    /// returns 404 in Production and Staging.
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthAsync(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var result = await _diagnostics.CheckAsync(ct);
        return Ok(result);
    }
}
```

- [ ] **Step 2: Register AiDiagnosticsService in Program.cs**

Add one line immediately after the existing `AddScoped<JournalExtractionService>()` line:

```csharp
builder.Services.AddScoped<JournalExtractionService>();
builder.Services.AddScoped<AiDiagnosticsService>();   // ← add this
```

- [ ] **Step 3: Build to verify wiring compiles**

```
cd backend
dotnet build
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Commit**

```
git add backend/Controllers/AiHealthController.cs backend/Program.cs
git commit -m "feat(ai-provider-health-check): add AiHealthController (dev-only) and register AiDiagnosticsService"
```

---

## Task 5 — Full test suite + clean build

- [ ] **Step 1: Run all tests**

```
cd backend.Tests
dotnet test -v normal
```
Expected: all tests pass. The count should be at least 14 higher than before this feature (6 validator + 8 service tests).

- [ ] **Step 2: Verify clean build with warnings-as-errors**

```
cd backend
dotnet build -warnaserror
```
Expected: `Build succeeded.` — no warnings promoted to errors.

---

## Acceptance checklist (from spec)

- [ ] `GET /api/ai/health` returns 404 outside Development
- [ ] `GET /api/ai/health` returns 200 with valid JSON in Development
- [ ] `Ai:Provider = mock` → `configured: true`, `success: true`
- [ ] `Ai:Provider = dial` with blank `ApiKey` → `configured: false`, `success: false`, no HTTP call
- [ ] DIAL returns HTTP 401 → `success: false`, `statusCode: 401`, message contains "Unauthorized"
- [ ] `rawResponsePreview` never exceeds 100 characters
- [ ] `rawResponsePreview` and `message` never contain the API key value
- [ ] All 14 new tests pass (6 validator + 8 service)
- [ ] `dotnet build` and `dotnet test` pass with no new warnings
- [ ] `Program.cs` startup check uses `AiProviderConfigValidator` (no inline duplicate)
