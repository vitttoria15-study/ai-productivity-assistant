using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record TodoistProject(
    [property: JsonPropertyName("id")]               string Id,
    [property: JsonPropertyName("name")]             string Name,
    [property: JsonPropertyName("color")]            string Color,
    [property: JsonPropertyName("order")]            int Order,
    [property: JsonPropertyName("is_inbox_project")] bool IsInboxProject);
