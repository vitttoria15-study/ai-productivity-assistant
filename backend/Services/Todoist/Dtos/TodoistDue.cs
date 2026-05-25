using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record TodoistDue(
    [property: JsonPropertyName("date")]         string Date,
    [property: JsonPropertyName("is_recurring")] bool IsRecurring,
    [property: JsonPropertyName("datetime")]     string? Datetime,
    [property: JsonPropertyName("string")]       string String);
