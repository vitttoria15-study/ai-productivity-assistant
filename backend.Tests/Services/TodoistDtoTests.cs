using System.Text.Json;
using backend.Services.Todoist.Dtos;

namespace backend.Tests.Services;

public class TodoistDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Todoist API v1 returns { "results": [...], "next_cursor": "..." } ────────

    [Fact]
    public void PagedResponse_Deserializes_TaskList()
    {
        var json = """
            {
                "results": [
                    {
                        "id": "8675309",
                        "content": "Buy oat milk",
                        "description": "From the big shop",
                        "project_id": "proj42",
                        "priority": 2,
                        "completed_at": null,
                        "due": {
                            "date": "2026-06-01",
                            "is_recurring": false,
                            "string": "Jun 1",
                            "timezone": null
                        }
                    }
                ],
                "next_cursor": null
            }
            """;

        var paged = JsonSerializer.Deserialize<PagedResponse<TodoistTask>>(json, JsonOptions);

        Assert.NotNull(paged);
        Assert.Single(paged.Results);
        Assert.Null(paged.NextCursor);

        var task = paged.Results[0];
        Assert.Equal("8675309",          task.Id);
        Assert.Equal("Buy oat milk",     task.Content);
        Assert.Equal("From the big shop", task.Description);
        Assert.Equal("proj42",           task.ProjectId);
        Assert.Equal(2,                  task.Priority);
        Assert.Null(task.CompletedAt);   // active task → null
        Assert.NotNull(task.Due);
        Assert.Equal("2026-06-01",       task.Due.Date);
        Assert.False(task.Due.IsRecurring);
    }

    [Fact]
    public void TodoistTask_Deserializes_WhenDueIsNull()
    {
        var json = """
            {
                "id": "1",
                "content": "No due date",
                "description": null,
                "project_id": "p1",
                "priority": 1,
                "completed_at": null,
                "due": null
            }
            """;

        var task = JsonSerializer.Deserialize<TodoistTask>(json, JsonOptions);

        Assert.NotNull(task);
        Assert.Null(task.Due);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void TodoistTask_Deserializes_CompletedAt_WhenPresent()
    {
        var json = """
            {
                "id": "2",
                "content": "Done task",
                "description": null,
                "project_id": "p1",
                "priority": 1,
                "completed_at": "2026-05-25T10:00:00Z",
                "due": null
            }
            """;

        var task = JsonSerializer.Deserialize<TodoistTask>(json, JsonOptions);

        Assert.NotNull(task);
        Assert.Equal("2026-05-25T10:00:00Z", task.CompletedAt);
    }

    [Fact]
    public void CreateTaskRequest_Serializes_WithCorrectPropertyNames()
    {
        var req = new CreateTaskRequest("Fix login bug", "proj1", 3);

        var json = JsonSerializer.Serialize(req);

        Assert.Contains("\"content\"", json);
        Assert.Contains("\"project_id\"", json);
        Assert.Contains("\"priority\"", json);
        Assert.Contains("Fix login bug", json);
    }
}
