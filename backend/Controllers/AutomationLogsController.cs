using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/automation/logs")]
public class AutomationLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AutomationLogsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAutomationLogRequest request)
    {
        var log = new AutomationLog
        {
            WorkflowName = request.WorkflowName,
            EventType = request.EventType,
            Message = request.Message,
            CreatedAt = DateTime.UtcNow
        };

        _db.AutomationLogs.Add(log);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = log.Id }, log);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _db.AutomationLogs
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync();

        return Ok(logs);
    }

    public sealed class CreateAutomationLogRequest
    {
        public string WorkflowName { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
