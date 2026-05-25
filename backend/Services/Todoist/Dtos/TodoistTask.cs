using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record TodoistTask(
    [property: JsonPropertyName("id")]           string   Id,
    [property: JsonPropertyName("content")]      string   Content,
    [property: JsonPropertyName("description")]  string?  Description,
    [property: JsonPropertyName("project_id")]   string   ProjectId,
    [property: JsonPropertyName("priority")]     int      Priority,
    /// <summary>Non-null ISO timestamp when the task was completed; null = active.</summary>
    [property: JsonPropertyName("completed_at")] string?  CompletedAt,
    [property: JsonPropertyName("due")]          TodoistDue? Due);
