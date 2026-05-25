namespace backend.Models;

/// <summary>
/// Normalised task shape returned to the frontend.
/// Maps Todoist's wire format to the existing id/title/status contract
/// so the React task list requires no changes.
/// </summary>
public record TaskResponse(
    string  Id,
    string  Title,
    string  Status,    // "todo" | "done"
    int     Priority,
    string? DueDate);
