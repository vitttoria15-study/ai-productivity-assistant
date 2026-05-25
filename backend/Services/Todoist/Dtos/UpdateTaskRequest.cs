using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record UpdateTaskRequest(
    [property: JsonPropertyName("content")]  string? Content,
    [property: JsonPropertyName("priority")] int? Priority);
