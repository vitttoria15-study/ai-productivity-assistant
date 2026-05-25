using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

/// <summary>
/// Envelope returned by Todoist API v1 list endpoints.
/// Results are paged; follow NextCursor until null to retrieve all items.
/// </summary>
public record PagedResponse<T>(
    [property: JsonPropertyName("results")]     IReadOnlyList<T> Results,
    [property: JsonPropertyName("next_cursor")] string?          NextCursor);
