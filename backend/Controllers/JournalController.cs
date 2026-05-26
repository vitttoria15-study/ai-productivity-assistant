using backend.Services.Ai;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/journal")]
public class JournalController : ControllerBase
{
    private readonly JournalExtractionService _extraction;

    public JournalController(JournalExtractionService extraction)
    {
        _extraction = extraction;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] JournalCreateRequest request,
        CancellationToken ct = default)
    {
        var response = await _extraction.ExtractAsync(request.JournalText, ct);
        return Ok(response);
    }

    [HttpGet("has-entry-today")]
    public async Task<IActionResult> HasEntryToday(
        [FromServices] backend.Data.AppDbContext db)
    {
        var today = DateTime.UtcNow.Date;
        var has   = await db.JournalEntries
            .AnyAsync(e => e.CreatedAt >= today && e.CreatedAt < today.AddDays(1));
        return Ok(new { hasEntryToday = has });
    }

    public sealed class JournalCreateRequest
    {
        public string JournalText { get; set; } = string.Empty;
    }
}
