using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace backend.Controllers;

[ApiController]
[Route("api/journal")]
public class JournalController : ControllerBase
{
    private static readonly MockExtractionResponse MockResponse = new()
    {
        CompletedTasks = [],
        NewTasks = ["Fix frontend validation"],
        Blockers = [],
        Priorities = ["Prepare demo"],
        Summary = "User worked on project setup."
    };

    private readonly AppDbContext _db;

    public JournalController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JournalCreateRequest request)
    {
        var entry = new JournalEntry
        {
            Content = request.JournalText,
            CreatedAt = DateTime.UtcNow,
            Summary = MockResponse.Summary
        };

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();

        foreach (var taskTitle in MockResponse.NewTasks)
        {
            _db.TaskItems.Add(new TaskItem
            {
                Title = taskTitle,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                JournalEntryId = entry.Id
            });
        }

        await _db.SaveChangesAsync();

        return Ok(MockResponse);
    }

    [HttpGet("has-entry-today")]
    public async Task<IActionResult> HasEntryToday()
    {
        var today = DateTime.UtcNow.Date;
        var hasEntryToday = await _db.JournalEntries.AnyAsync(entry => entry.CreatedAt >= today && entry.CreatedAt < today.AddDays(1));

        return Ok(new { hasEntryToday });
    }

    public sealed class JournalCreateRequest
    {
        public string JournalText { get; set; } = string.Empty;
    }

    private sealed class MockExtractionResponse
    {
        [JsonPropertyName("completed_tasks")]
        public string[] CompletedTasks { get; set; } = [];

        [JsonPropertyName("new_tasks")]
        public string[] NewTasks { get; set; } = [];

        [JsonPropertyName("blockers")]
        public string[] Blockers { get; set; } = [];

        [JsonPropertyName("priorities")]
        public string[] Priorities { get; set; } = [];

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }
}