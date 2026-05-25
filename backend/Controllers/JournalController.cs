using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;

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

    private readonly AppDbContext    _db;
    private readonly ITodoistService _todoistService;

    public JournalController(AppDbContext db, ITodoistService todoistService)
    {
        _db             = db;
        _todoistService = todoistService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] JournalCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = new JournalEntry
        {
            Content = request.JournalText,
            CreatedAt = DateTime.UtcNow,
            Summary = MockResponse.Summary
        };

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        // TODO(todoist-api-foundation): priority=1 is a temporary MVP default.
        //   Replace with AI-extracted priority once the LLM extraction pipeline
        //   is wired in and MockExtractionResponse is replaced with a real response.
        foreach (var taskTitle in MockResponse.NewTasks)
        {
            await _todoistService.CreateTaskAsync(
                new CreateTaskRequest(taskTitle, null, Priority: 1),
                cancellationToken);
        }

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