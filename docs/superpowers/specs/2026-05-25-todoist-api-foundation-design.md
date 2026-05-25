# Todoist API Foundation — Design Spec

**Date:** 2026-05-25  
**Slug:** todoist-api-foundation  
**Branch:** feature/todoist-api-foundation  
**Status:** approved

---

## 1. Context and Goals

The backend currently serves `GET /api/tasks` from a SQLite `TaskItems` table. The authoritative source of tasks is Todoist. This spec replaces the SQLite task-query path with a Todoist REST v1 proxy while keeping SQLite for app metadata (journal entries, automation logs) only.

### Goals

- Wire `ITodoistService` / `TodoistService` using a typed `HttpClient`.
- Expose the full Todoist task CRUD surface on `/api/tasks` (GET, POST, PUT, close, DELETE).
- Update `JournalController` so new tasks from journal extraction are created in Todoist, not SQLite.
- Maintain the existing frontend response contract (`id`, `title`, `status`) with no UI changes.
- Provide a `backend.Tests/` xUnit project with meaningful unit tests and a missing-token guard.

### Non-goals

- No OAuth — `ApiToken` is a static personal token from config.
- No database migrations — `TaskItem` table remains in the schema but is no longer written to.
- No storing Todoist tasks in SQLite.
- No frontend UI changes beyond keeping the task list functional.
- No multi-project routing — all tasks target Todoist inbox (no `projectId` required from frontend).

---

## 2. Configuration

### 2.1 POCO

```csharp
// backend/Services/Todoist/TodoistOptions.cs
public class TodoistOptions
{
    public const string SectionName = "Todoist";
    public string BaseUrl { get; set; } = "https://api.todoist.com/api/v1";
    public string ApiToken { get; set; } = string.Empty;
}
```

### 2.2 Registration (`Program.cs`)

```csharp
builder.Services.Configure<TodoistOptions>(
    builder.Configuration.GetSection(TodoistOptions.SectionName));
```

### 2.3 Missing-token behaviour

- If `ApiToken` is empty or whitespace at `TodoistService` call time, throw `InvalidOperationException("Todoist ApiToken is not configured")`.
- Controllers catch this and return `503 Service Unavailable` with a JSON body `{ "error": "Todoist integration is not configured." }`.
- A startup warning log is emitted via `ILogger` when `ApiToken` is empty (does not block startup).

### 2.4 Config files

`appsettings.Development.json` already contains the `Todoist` section. `appsettings.json` does **not** include a default token (keeps production-safe).

---

## 3. DTOs

All records are in `backend/Services/Todoist/Dtos/`.

### 3.1 Todoist wire types (inbound from Todoist REST API)

```csharp
// TodoistProject.cs
public record TodoistProject(
    [property: JsonPropertyName("id")]    string Id,
    [property: JsonPropertyName("name")]  string Name,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("is_inbox_project")] bool IsInboxProject);

// TodoistTask.cs
public record TodoistTask(
    [property: JsonPropertyName("id")]           string Id,
    [property: JsonPropertyName("content")]      string Content,
    [property: JsonPropertyName("description")]  string? Description,
    [property: JsonPropertyName("project_id")]   string ProjectId,
    [property: JsonPropertyName("priority")]     int Priority,
    [property: JsonPropertyName("is_completed")] bool IsCompleted,
    [property: JsonPropertyName("due")]          TodoistDue? Due);

// TodoistDue.cs
public record TodoistDue(
    [property: JsonPropertyName("date")]         string Date,
    [property: JsonPropertyName("is_recurring")] bool IsRecurring,
    [property: JsonPropertyName("datetime")]     string? Datetime,
    [property: JsonPropertyName("string")]       string String);
```

### 3.2 Request bodies (outbound to Todoist REST API)

```csharp
// CreateTaskRequest.cs
public record CreateTaskRequest(
    [property: JsonPropertyName("content")]    string Content,
    [property: JsonPropertyName("project_id")] string? ProjectId,
    [property: JsonPropertyName("priority")]   int? Priority);

// UpdateTaskRequest.cs
public record UpdateTaskRequest(
    [property: JsonPropertyName("content")]  string? Content,
    [property: JsonPropertyName("priority")] int? Priority);
```

### 3.3 Normalised response (outbound to frontend)

```csharp
// backend/Models/TaskResponse.cs
public record TaskResponse(
    string Id,
    string Title,
    string Status,   // "todo" | "done"
    int Priority,
    string? DueDate);
```

**Mapping rule:** `Status = task.IsCompleted ? "done" : "todo"`, `Title = task.Content`, `DueDate = task.Due?.Date`.

---

## 4. Service Layer

### 4.1 Interface

```csharp
// backend/Services/Todoist/ITodoistService.cs
public interface ITodoistService
{
    Task<IReadOnlyList<TodoistProject>> GetProjectsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TodoistTask>>    GetActiveTasksAsync(string? projectId = null, CancellationToken ct = default);
    Task<TodoistTask>                   CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<TodoistTask>                   UpdateTaskAsync(string taskId, UpdateTaskRequest request, CancellationToken ct = default);
    Task                                CloseTaskAsync(string taskId, CancellationToken ct = default);
    Task                                DeleteTaskAsync(string taskId, CancellationToken ct = default);
}
```

### 4.2 Implementation

`TodoistService : ITodoistService` receives an `HttpClient` (base address and auth header set by `DelegatingHandler`) and `IOptions<TodoistOptions>`.

**Auth handler:**
```csharp
// backend/Services/Todoist/TodoistAuthHandler.cs
internal sealed class TodoistAuthHandler(IOptions<TodoistOptions> opts) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opts.Value.ApiToken))
            throw new InvalidOperationException("Todoist ApiToken is not configured");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", opts.Value.ApiToken);
        return base.SendAsync(request, ct);
    }
}
```

**Registration (`Program.cs`):**
```csharp
builder.Services
    .AddTransient<TodoistAuthHandler>()
    .AddHttpClient<ITodoistService, TodoistService>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Todoist:BaseUrl"] ?? "https://api.todoist.com/api/v1");
    })
    .AddHttpMessageHandler<TodoistAuthHandler>();
```

### 4.3 Error handling in service

Non-2xx responses from Todoist call `response.EnsureSuccessStatusCode()`, which throws `HttpRequestException`. Controllers do not catch these — ASP.NET's default exception handler returns `500`. Callers may add retry logic in a later task.

---

## 5. Controllers

### 5.1 TasksController (full replacement)

`AppDbContext` dependency removed. `ITodoistService` injected instead.

| Verb | Route | Service call | Response |
|---|---|---|---|
| GET | `/api/tasks` | `GetActiveTasksAsync()` | `200 TaskResponse[]` |
| POST | `/api/tasks` | `CreateTaskAsync(body)` | `201 TaskResponse` |
| PUT | `/api/tasks/{id}` | `UpdateTaskAsync(id, body)` | `200 TaskResponse` |
| POST | `/api/tasks/{id}/close` | `CloseTaskAsync(id)` | `204 No Content` |
| DELETE | `/api/tasks/{id}` | `DeleteTaskAsync(id)` | `204 No Content` |

Each action maps `TodoistTask → TaskResponse` using a private static helper `ToResponse(TodoistTask t)`.

`InvalidOperationException` (missing token) → `503 { "error": "Todoist integration is not configured." }`.

### 5.2 JournalController (partial update)

- `AppDbContext` remains (still persists `JournalEntry` record — journal metadata stays in SQLite).
- `ITodoistService` added to constructor.
- The loop `foreach (var taskTitle in MockResponse.NewTasks)` now calls `await _todoistService.CreateTaskAsync(new CreateTaskRequest(taskTitle, null, 1), ct)` instead of adding to `_db.TaskItems`.
- The `_db.TaskItems.Add(...)` and second `SaveChangesAsync` are removed.

---

## 6. Testing (`backend.Tests/`)

### 6.1 Project setup

- `backend.Tests/backend.Tests.csproj`: `net8.0`, references `backend.csproj`, packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Moq`.
- No `WebApplicationFactory` for now — pure unit tests only.

### 6.2 Test coverage

| Class | Scenario |
|---|---|
| `TodoistServiceTests` | `GetActiveTasksAsync` — happy path returns deserialized list |
| `TodoistServiceTests` | `CreateTaskAsync` — happy path serialises request + deserialises response |
| `TodoistServiceTests` | Missing token → `InvalidOperationException` before HTTP call |
| `TasksControllerTests` | `GetAll` maps `TodoistTask → TaskResponse` (`Content→Title`, `is_completed→status`) |
| `TasksControllerTests` | `InvalidOperationException` → 503 result |
| `TodoistOptionsTests` | Config section binds `BaseUrl` and `ApiToken` correctly |

Tests mock `HttpMessageHandler` via a `TestHttpMessageHandler` helper (no third-party HTTP mock library needed).

---

## 7. File layout after implementation

```
backend/
├── Controllers/
│   ├── TasksController.cs        ← replaced (Todoist proxy)
│   └── JournalController.cs      ← updated (journal → Todoist tasks)
├── Models/
│   └── TaskResponse.cs           ← new
├── Services/
│   └── Todoist/
│       ├── ITodoistService.cs    ← new
│       ├── TodoistService.cs     ← new
│       ├── TodoistAuthHandler.cs ← new
│       ├── TodoistOptions.cs     ← new
│       └── Dtos/
│           ├── TodoistProject.cs ← new
│           ├── TodoistTask.cs    ← new
│           ├── TodoistDue.cs     ← new
│           ├── CreateTaskRequest.cs ← new
│           └── UpdateTaskRequest.cs ← new
├── Program.cs                    ← updated (HttpClient + Options registration)
└── appsettings.json              ← unchanged (no token in base config)

backend.Tests/
├── backend.Tests.csproj          ← new
├── Helpers/
│   └── TestHttpMessageHandler.cs ← new
├── Services/
│   └── TodoistServiceTests.cs    ← new
└── Controllers/
    └── TasksControllerTests.cs   ← new
```

---

## 8. Constraints

- No migrations. `TaskItem` EF model and `AppDbContext.TaskItems` DbSet remain but are not written to.
- SQLite connection string and `JournalEntry` persistence in `JournalController` are unchanged.
- No OAuth — static `ApiToken` only.
- No frontend changes — `TaskResponse` preserves `{id, title, status}` shape.
- Do not commit without asking.
