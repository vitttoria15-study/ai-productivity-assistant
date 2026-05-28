using System.Text.Json.Serialization;

namespace backend.Services.Ai;

public sealed record ExtractedNewTask(
    [property: JsonPropertyName("title")]   string  Title,
    [property: JsonPropertyName("project")] string? Project);
