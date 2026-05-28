using System.Text.Json.Serialization;

namespace backend.Models;

public sealed record JournalExtractionResponse(
    [property: JsonPropertyName("extraction_status")] string   ExtractionStatus,
    [property: JsonPropertyName("extraction_error")]  string?  ExtractionError,
    [property: JsonPropertyName("provider")]          string   Provider,
    [property: JsonPropertyName("completed_tasks")]   string[] CompletedTasks,
    [property: JsonPropertyName("new_tasks")]         string[] NewTasks,
    [property: JsonPropertyName("blockers")]          string[] Blockers,
    [property: JsonPropertyName("priorities")]        string[] Priorities,
    [property: JsonPropertyName("summary")]           string   Summary
);
