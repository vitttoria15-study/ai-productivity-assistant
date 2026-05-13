using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;

    public TasksController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _db.TaskItems
            .Select(task => new
            {
                task.Id,
                task.Title,
                task.Status,
                task.CreatedAt,
                task.JournalEntryId
            })
            .ToListAsync();

        return Ok(tasks);
    }
}