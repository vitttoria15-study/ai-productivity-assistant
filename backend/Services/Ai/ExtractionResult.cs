using System.Text.Json.Serialization;

namespace backend.Services.Ai;

public sealed record ExtractionResult(
    [property: JsonPropertyName("completed_tasks")] string[] CompletedTasks,
    [property: JsonPropertyName("new_tasks")]       string[] NewTasks,
    [property: JsonPropertyName("blockers")]        string[] Blockers,
    [property: JsonPropertyName("priorities")]      string[] Priorities,
    [property: JsonPropertyName("summary")]         string   Summary
);
