using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record CreateTaskRequest(
    [property: JsonPropertyName("content")]    string Content,
    [property: JsonPropertyName("project_id")] string? ProjectId,
    [property: JsonPropertyName("priority")]   int? Priority);
