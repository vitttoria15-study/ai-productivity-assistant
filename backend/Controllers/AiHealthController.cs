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
