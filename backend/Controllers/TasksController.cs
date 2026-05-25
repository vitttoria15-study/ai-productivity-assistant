using backend.Models;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITodoistService _todoist;

    public TasksController(ITodoistService todoist)
    {
        _todoist = todoist;
    }

    // ── GET /api/tasks ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = await _todoist.GetActiveTasksAsync(projectId, ct: cancellationToken);
            return Ok(tasks.Select(ToResponse));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── POST /api/tasks ───────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var task = await _todoist.CreateTaskAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetAll), ToResponse(task));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── PUT /api/tasks/{id} ───────────────────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var task = await _todoist.UpdateTaskAsync(id, request, cancellationToken);
            return Ok(ToResponse(task));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── POST /api/tasks/{id}/close ────────────────────────────────────────────

    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _todoist.CloseTaskAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── DELETE /api/tasks/{id} ────────────────────────────────────────────────

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _todoist.DeleteTaskAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TaskResponse ToResponse(TodoistTask task) => new(
        task.Id,
        task.Content,
        task.CompletedAt is not null ? "done" : "todo",
        task.Priority,
        task.Due?.Date);
}