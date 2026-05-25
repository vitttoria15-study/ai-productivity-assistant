using backend.Services.Todoist.Dtos;

namespace backend.Services.Todoist;

/// <summary>
/// Todoist REST v1 client. Focused solely on Todoist HTTP operations.
/// Token validation is the sole responsibility of TodoistAuthHandler —
/// this service does NOT duplicate that check.
/// </summary>
public sealed class TodoistService : ITodoistService
{
    private readonly HttpClient _client;

    public TodoistService(HttpClient client)
    {
        _client = client;
    }

    // ── Projects ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TodoistProject>> GetProjectsAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync("projects", ct);
        response.EnsureSuccessStatusCode();
        // API v1 returns a paginated envelope: { "results": [...], "next_cursor": "..." }
        // TODO: follow next_cursor for full pagination when project count exceeds one page
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<TodoistProject>>(ct);
        return paged?.Results ?? [];
    }

    // ── Active tasks ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(
        string? projectId = null, CancellationToken ct = default)
    {
        var url = projectId is not null
            ? $"tasks?project_id={Uri.EscapeDataString(projectId)}"
            : "tasks";
        var response = await _client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        // API v1 returns a paginated envelope: { "results": [...], "next_cursor": "..." }
        // TODO: follow next_cursor for full pagination when task count exceeds one page
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<TodoistTask>>(ct);
        return paged?.Results ?? [];
    }

    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<TodoistTask> CreateTaskAsync(
        CreateTaskRequest request, CancellationToken ct = default)
    {
        var response = await _client.PostAsJsonAsync("tasks", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TodoistTask>(ct)
               ?? throw new InvalidOperationException("Empty response from Todoist on CreateTask");
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    public async Task<TodoistTask> UpdateTaskAsync(
        string taskId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var response = await _client.PatchAsJsonAsync($"tasks/{taskId}", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TodoistTask>(ct)
               ?? throw new InvalidOperationException("Empty response from Todoist on UpdateTask");
    }

    // ── Close (complete) ───────────────────────────────────────────────────────

    public async Task CloseTaskAsync(string taskId, CancellationToken ct = default)
    {
        var response = await _client.PostAsync($"tasks/{taskId}/close", null, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    public async Task DeleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        var response = await _client.DeleteAsync($"tasks/{taskId}", ct);
        response.EnsureSuccessStatusCode();
    }
}
