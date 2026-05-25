using backend.Models;
using backend.Services.Todoist;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ITodoistService _todoist;

    public ProjectsController(ITodoistService todoist)
    {
        _todoist = todoist;
    }

    // ── GET /api/projects ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var projects = await _todoist.GetProjectsAsync(cancellationToken);
            return Ok(projects.Select(p => new ProjectResponse(
                p.Id, p.Name, p.Color, p.Order, p.IsInboxProject)));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }
}
