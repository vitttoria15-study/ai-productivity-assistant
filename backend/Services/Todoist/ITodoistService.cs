using backend.Services.Todoist.Dtos;

namespace backend.Services.Todoist;

public interface ITodoistService
{
    Task<IReadOnlyList<TodoistProject>> GetProjectsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TodoistTask>> GetActiveTasksAsync(
        string? projectId = null, CancellationToken ct = default);

    Task<TodoistTask> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default);

    Task<TodoistTask> UpdateTaskAsync(
        string taskId, UpdateTaskRequest request, CancellationToken ct = default);

    Task CloseTaskAsync(string taskId, CancellationToken ct = default);

    Task DeleteTaskAsync(string taskId, CancellationToken ct = default);
}
