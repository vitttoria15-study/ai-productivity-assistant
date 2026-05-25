namespace backend.Models;

/// <summary>
/// Normalised project shape returned to the frontend.
/// Maps Todoist's wire format to a clean API surface
/// consistent with the TaskResponse pattern.
/// </summary>
public record ProjectResponse(
    string Id,
    string Name,
    string Color,
    int    Order,
    bool   IsInboxProject);
