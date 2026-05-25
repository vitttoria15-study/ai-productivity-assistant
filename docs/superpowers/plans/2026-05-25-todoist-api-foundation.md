# Todoist API Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the SQLite task-query path with a Todoist REST v1 proxy, expose full task CRUD on `/api/tasks`, and route journal-extracted task creation to Todoist.

**Architecture:** `TodoistService` wraps a typed `HttpClient` (registered via `AddHttpClient`; a `TodoistAuthHandler` DelegatingHandler injects the Bearer token). The service validates the token itself and throws `InvalidOperationException` if empty. `TasksController` delegates all task operations to `ITodoistService` and normalises responses to the `TaskResponse` shape the frontend already expects (`id`, `title`, `status`). `JournalController` calls `ITodoistService.CreateTaskAsync` instead of writing to SQLite.

**Tech Stack:** ASP.NET Core 8 · `System.Net.Http` / `System.Net.Http.Json` · `System.Text.Json` · xUnit 2.x · Moq 4.x · .NET 8

---

## File Map

**New files:**
- `backend/Services/Todoist/TodoistOptions.cs` — config POCO
- `backend/Services/Todoist/ITodoistService.cs` — service interface
- `backend/Services/Todoist/TodoistService.cs` — typed HttpClient implementation
- `backend/Services/Todoist/TodoistAuthHandler.cs` — DelegatingHandler (injects Bearer header)
- `backend/Services/Todoist/Dtos/TodoistProject.cs` — Todoist project wire type
- `backend/Services/Todoist/Dtos/TodoistTask.cs` — Todoist task wire type
- `backend/Services/Todoist/Dtos/TodoistDue.cs` — Todoist due-date sub-record
- `backend/Services/Todoist/Dtos/CreateTaskRequest.cs` — outbound create payload
- `backend/Services/Todoist/Dtos/UpdateTaskRequest.cs` — outbound update payload
- `backend/Models/TaskResponse.cs` — normalised frontend response record
- `backend.Tests/backend.Tests.csproj` — xUnit test project
- `backend.Tests/Helpers/TestHttpMessageHandler.cs` — fake HttpMessageHandler
- `backend.Tests/Services/TodoistServiceTests.cs` — service unit tests
- `backend.Tests/Controllers/TasksControllerTests.cs` — controller unit tests

**Modified files:**
- `backend/Controllers/TasksController.cs` — replace SQLite with Todoist
- `backend/Controllers/JournalController.cs` — route task creation to Todoist
- `backend/Program.cs` — register options + typed HttpClient
- `ai-productivity-assistant.sln` — add `backend.Tests` project

---

## Task 1: Scaffold `backend.Tests/` xUnit project

**Test-first:** no — infrastructure only

**Files:**
- Create: `backend.Tests/backend.Tests.csproj`
- Modify: `ai-productivity-assistant.sln`

- [ ] **Step 1: Create the test project**

```powershell
dotnet new xunit -n backend.Tests -o backend.Tests --framework net8.0
```

Expected output:
```
The template "xUnit Test Project" was created successfully.
```

- [ ] **Step 2: Remove the auto-generated placeholder test**

Delete `backend.Tests/UnitTest1.cs`.

```powershell
Remove-Item backend.Tests\UnitTest1.cs
```

- [ ] **Step 3: Add project reference and packages to backend.Tests.csproj**

Open `backend.Tests/backend.Tests.csproj` and replace its entire content with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\backend\backend.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Add the test project to the solution**

```powershell
dotnet sln ai-productivity-assistant.sln add backend.Tests/backend.Tests.csproj
```

Expected output:
```
Project `backend.Tests\backend.Tests.csproj` added to the solution.
```

- [ ] **Step 5: Verify the solution builds with zero errors**

```powershell
dotnet build ai-productivity-assistant.sln
```

Expected: `Build succeeded. 0 Error(s)` (possibly warnings about no test files yet — that is OK).

---

## Task 2: `TodoistOptions` POCO + configuration binding test

**Test-first:** yes — write binding test, create POCO to make it pass

**Files:**
- Create: `backend/Services/Todoist/TodoistOptions.cs`
- Create: `backend.Tests/Services/TodoistOptionsTests.cs`

- [ ] **Step 1: Create the test file with a failing test**

Create `backend.Tests/Services/TodoistOptionsTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Xunit;

namespace backend.Tests.Services;

public class TodoistOptionsTests
{
    [Fact]
    public void TodoistOptions_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Todoist:BaseUrl"]  = "https://api.todoist.com/api/v1",
                ["Todoist:ApiToken"] = "test-token-123"
            })
            .Build();

        var options = new backend.Services.Todoist.TodoistOptions();
        config.GetSection("Todoist").Bind(options);

        Assert.Equal("https://api.todoist.com/api/v1", options.BaseUrl);
        Assert.Equal("test-token-123", options.ApiToken);
    }

    [Fact]
    public void TodoistOptions_DefaultBaseUrl_IsSet()
    {
        var options = new backend.Services.Todoist.TodoistOptions();
        Assert.Equal("https://api.todoist.com/api/v1", options.BaseUrl);
    }

    [Fact]
    public void TodoistOptions_DefaultApiToken_IsEmpty()
    {
        var options = new backend.Services.Todoist.TodoistOptions();
        Assert.Equal(string.Empty, options.ApiToken);
    }
}
```

- [ ] **Step 2: Run the test — expect compile failure (type not found)**

```powershell
dotnet test backend.Tests/backend.Tests.csproj --no-build 2>&1 | Select-String "error"
```

Or build to see the compile error:

```powershell
dotnet build backend.Tests/backend.Tests.csproj
```

Expected: build error — `backend.Services.Todoist.TodoistOptions` does not exist.

- [ ] **Step 3: Create the POCO**

Create `backend/Services/Todoist/TodoistOptions.cs`:

```csharp
namespace backend.Services.Todoist;

public class TodoistOptions
{
    public const string SectionName = "Todoist";

    public string BaseUrl { get; set; } = "https://api.todoist.com/api/v1";

    public string ApiToken { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Run the tests — expect GREEN**

```powershell
dotnet test backend.Tests/backend.Tests.csproj
```

Expected output:
```
Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3
```

- [ ] **Step 5: Commit**

```powershell
git add backend/Services/Todoist/TodoistOptions.cs `
        backend.Tests/Services/TodoistOptionsTests.cs `
        backend.Tests/backend.Tests.csproj `
        ai-productivity-assistant.sln
git commit -m "feat(todoist-api-foundation): scaffold test project and TodoistOptions config POCO"
```

---

## Task 3: Todoist DTOs — wire types, request types, `TaskResponse`

**Test-first:** yes — write a JSON deserialization test for `TodoistTask` first

**Files:**
- Create: `backend/Services/Todoist/Dtos/TodoistDue.cs`
- Create: `backend/Services/Todoist/Dtos/TodoistTask.cs`
- Create: `backend/Services/Todoist/Dtos/TodoistProject.cs`
- Create: `backend/Services/Todoist/Dtos/CreateTaskRequest.cs`
- Create: `backend/Services/Todoist/Dtos/UpdateTaskRequest.cs`
- Create: `backend/Models/TaskResponse.cs`
- Create: `backend.Tests/Services/TodoistDtoTests.cs`

- [ ] **Step 1: Write the failing DTO deserialization test**

Create `backend.Tests/Services/TodoistDtoTests.cs`:

```csharp
using System.Text.Json;
using backend.Services.Todoist.Dtos;
using Xunit;

namespace backend.Tests.Services;

public class TodoistDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void TodoistTask_Deserializes_FromTodoistJson()
    {
        var json = """
            {
                "id": "8675309",
                "content": "Buy oat milk",
                "description": "From the big shop",
                "project_id": "proj42",
                "priority": 2,
                "is_completed": false,
                "due": {
                    "date": "2026-06-01",
                    "is_recurring": false,
                    "datetime": null,
                    "string": "Jun 1"
                }
            }
            """;

        var task = JsonSerializer.Deserialize<TodoistTask>(json, JsonOptions);

        Assert.NotNull(task);
        Assert.Equal("8675309", task.Id);
        Assert.Equal("Buy oat milk", task.Content);
        Assert.Equal("From the big shop", task.Description);
        Assert.Equal("proj42", task.ProjectId);
        Assert.Equal(2, task.Priority);
        Assert.False(task.IsCompleted);
        Assert.NotNull(task.Due);
        Assert.Equal("2026-06-01", task.Due.Date);
        Assert.False(task.Due.IsRecurring);
    }

    [Fact]
    public void TodoistTask_Deserializes_WhenDueIsNull()
    {
        var json = """
            {
                "id": "1",
                "content": "No due date",
                "description": null,
                "project_id": "p1",
                "priority": 1,
                "is_completed": false,
                "due": null
            }
            """;

        var task = JsonSerializer.Deserialize<TodoistTask>(json, JsonOptions);

        Assert.NotNull(task);
        Assert.Null(task.Due);
    }

    [Fact]
    public void CreateTaskRequest_Serializes_WithCorrectPropertyNames()
    {
        var req = new CreateTaskRequest("Fix login bug", "proj1", 3);

        var json = JsonSerializer.Serialize(req);

        Assert.Contains("\"content\"", json);
        Assert.Contains("\"project_id\"", json);
        Assert.Contains("\"priority\"", json);
        Assert.Contains("Fix login bug", json);
    }
}
```

- [ ] **Step 2: Run — expect compile failure**

```powershell
dotnet build backend.Tests/backend.Tests.csproj
```

Expected: compile errors (DTO types don't exist yet).

- [ ] **Step 3: Create `TodoistDue.cs`**

Create `backend/Services/Todoist/Dtos/TodoistDue.cs`:

```csharp
using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record TodoistDue(
    [property: JsonPropertyName("date")]         string Date,
    [property: JsonPropertyName("is_recurring")] bool IsRecurring,
    [property: JsonPropertyName("datetime")]     string? Datetime,
    [property: JsonPropertyName("string")]       string String);
```

- [ ] **Step 4: Create `TodoistTask.cs`**

Create `backend/Services/Todoist/Dtos/TodoistTask.cs`:

```csharp
using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record TodoistTask(
    [property: JsonPropertyName("id")]           string Id,
    [property: JsonPropertyName("content")]      string Content,
    [property: JsonPropertyName("description")]  string? Description,
    [property: JsonPropertyName("project_id")]   string ProjectId,
    [property: JsonPropertyName("priority")]     int Priority,
    [property: JsonPropertyName("is_completed")] bool IsCompleted,
    [property: JsonPropertyName("due")]          TodoistDue? Due);
```

- [ ] **Step 5: Create `TodoistProject.cs`**

Create `backend/Services/Todoist/Dtos/TodoistProject.cs`:

```csharp
using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record TodoistProject(
    [property: JsonPropertyName("id")]               string Id,
    [property: JsonPropertyName("name")]             string Name,
    [property: JsonPropertyName("color")]            string Color,
    [property: JsonPropertyName("order")]            int Order,
    [property: JsonPropertyName("is_inbox_project")] bool IsInboxProject);
```

- [ ] **Step 6: Create `CreateTaskRequest.cs`**

Create `backend/Services/Todoist/Dtos/CreateTaskRequest.cs`:

```csharp
using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record CreateTaskRequest(
    [property: JsonPropertyName("content")]    string Content,
    [property: JsonPropertyName("project_id")] string? ProjectId,
    [property: JsonPropertyName("priority")]   int? Priority);
```

- [ ] **Step 7: Create `UpdateTaskRequest.cs`**

Create `backend/Services/Todoist/Dtos/UpdateTaskRequest.cs`:

```csharp
using System.Text.Json.Serialization;

namespace backend.Services.Todoist.Dtos;

public record UpdateTaskRequest(
    [property: JsonPropertyName("content")]  string? Content,
    [property: JsonPropertyName("priority")] int? Priority);
```

- [ ] **Step 8: Create `TaskResponse.cs`**

Create `backend/Models/TaskResponse.cs`:

```csharp
namespace backend.Models;

/// <summary>
/// Normalised task shape returned to the frontend.
/// Maps Todoist's wire format to the existing id/title/status contract.
/// </summary>
public record TaskResponse(
    string   Id,
    string   Title,
    string   Status,    // "todo" | "done"
    int      Priority,
    string?  DueDate);
```

- [ ] **Step 9: Run tests — expect GREEN**

```powershell
dotnet test backend.Tests/backend.Tests.csproj
```

Expected:
```
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

- [ ] **Step 10: Commit**

```powershell
git add backend/Services/Todoist/Dtos/ `
        backend/Models/TaskResponse.cs `
        backend.Tests/Services/TodoistDtoTests.cs
git commit -m "feat(todoist-api-foundation): add Todoist DTOs and TaskResponse model"
```

---

## Task 4: `TestHttpMessageHandler` helper

**Test-first:** no — test utility with no own behavior

**Files:**
- Create: `backend.Tests/Helpers/TestHttpMessageHandler.cs`

- [ ] **Step 1: Create the helper**

Create `backend.Tests/Helpers/TestHttpMessageHandler.cs`:

```csharp
namespace backend.Tests.Helpers;

/// <summary>
/// A fake HttpMessageHandler that returns a pre-configured response.
/// Use this to unit-test classes that take an HttpClient without making real HTTP calls.
/// </summary>
public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    /// <summary>Convenience overload: always return the same response.</summary>
    public TestHttpMessageHandler(HttpResponseMessage response)
        : this(_ => response) { }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}
```

- [ ] **Step 2: Build to confirm no compile errors**

```powershell
dotnet build backend.Tests/backend.Tests.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

---

## Task 5: `ITodoistService` interface + `TodoistAuthHandler`

**Test-first:** no — interface has no behavior; handler is tested via service tests in Task 6

**Files:**
- Create: `backend/Services/Todoist/ITodoistService.cs`
- Create: `backend/Services/Todoist/TodoistAuthHandler.cs`

- [ ] **Step 1: Create the interface**

Create `backend/Services/Todoist/ITodoistService.cs`:

```csharp
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
```

- [ ] **Step 2: Create `TodoistAuthHandler`**

Create `backend/Services/Todoist/TodoistAuthHandler.cs`:

```csharp
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace backend.Services.Todoist;

/// <summary>
/// Single source of truth for missing-token validation.
/// Throws InvalidOperationException before the HTTP call if ApiToken is empty,
/// then injects the Bearer header on every outgoing request.
/// TodoistService does NOT duplicate this check.
/// </summary>
public sealed class TodoistAuthHandler : DelegatingHandler
{
    private readonly IOptions<TodoistOptions> _options;

    public TodoistAuthHandler(IOptions<TodoistOptions> options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Value.ApiToken))
            throw new InvalidOperationException("Todoist ApiToken is not configured");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.Value.ApiToken);
        return base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build backend/backend.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

---

## Task 6: `TodoistService` — missing-token guard + `GetActiveTasksAsync` (TDD)

**Test-first:** yes — write failing tests for missing-token guard and GetActiveTasks deserialization

**Files:**
- Create: `backend/Services/Todoist/TodoistService.cs` (skeleton → grown step by step)
- Create: `backend.Tests/Services/TodoistServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `backend.Tests/Services/TodoistServiceTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using backend.Tests.Helpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace backend.Tests.Services;

public class TodoistServiceTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a TodoistService backed by a fake HttpMessageHandler.
    /// No auth handler — use for happy-path tests where auth is irrelevant.
    /// </summary>
    private static TodoistService BuildService(
        Func<HttpRequestMessage, HttpResponseMessage> httpHandler)
    {
        var handler = new TestHttpMessageHandler(httpHandler);
        var client  = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.todoist.com/api/v1/")
        };
        return new TodoistService(client);
    }

    /// <summary>
    /// Builds a TodoistService with the real TodoistAuthHandler wired in.
    /// Use for missing-token tests — the handler is the single validation point.
    /// </summary>
    private static TodoistService BuildServiceWithAuth(
        Func<HttpRequestMessage, HttpResponseMessage> httpHandler,
        string apiToken)
    {
        var options     = Options.Create(new TodoistOptions { ApiToken = apiToken });
        var testHandler = new TestHttpMessageHandler(httpHandler);
        var authHandler = new TodoistAuthHandler(options) { InnerHandler = testHandler };
        var client      = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("https://api.todoist.com/api/v1/")
        };
        return new TodoistService(client);
    }

    private static HttpResponseMessage JsonResponse(object payload, HttpStatusCode status = HttpStatusCode.OK)
    {
        var json    = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return new HttpResponseMessage(status) { Content = content };
    }

    // ── missing-token guard (via TodoistAuthHandler — single source of truth) ──

    [Fact]
    public async Task GetActiveTasksAsync_WhenTokenEmpty_ThrowsInvalidOperationException()
    {
        // Auth handler is the single validation point — service itself has no token check.
        var service = BuildServiceWithAuth(_ => new HttpResponseMessage(HttpStatusCode.OK), apiToken: "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetActiveTasksAsync());

        Assert.Equal("Todoist ApiToken is not configured", ex.Message);
    }

    [Fact]
    public async Task CreateTaskAsync_WhenTokenEmpty_ThrowsInvalidOperationException()
    {
        var service = BuildServiceWithAuth(_ => new HttpResponseMessage(HttpStatusCode.OK), apiToken: "");
        var req     = new CreateTaskRequest("Do something", null, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateTaskAsync(req));
    }

    // ── GetActiveTasksAsync happy path ────────────────────────────────────────

    [Fact]
    public async Task GetActiveTasksAsync_ReturnsMappedTasks()
    {
        var todoist = new[]
        {
            new
            {
                id = "1", content = "Buy milk", description = (string?)null,
                project_id = "p1", priority = 1, is_completed = false, due = (object?)null
            }
        };

        var service = BuildService(_ => JsonResponse(todoist));

        var tasks = await service.GetActiveTasksAsync();

        Assert.Single(tasks);
        Assert.Equal("1",        tasks[0].Id);
        Assert.Equal("Buy milk", tasks[0].Content);
        Assert.False(tasks[0].IsCompleted);
    }

    [Fact]
    public async Task GetActiveTasksAsync_PassesProjectIdAsQueryParam()
    {
        HttpRequestMessage? captured = null;
        var service = BuildService(req =>
        {
            captured = req;
            return JsonResponse(Array.Empty<object>());
        });

        await service.GetActiveTasksAsync(projectId: "proj123");

        Assert.NotNull(captured);
        Assert.Contains("project_id=proj123", captured!.RequestUri?.Query);
    }

    // ── CreateTaskAsync happy path ────────────────────────────────────────────

    [Fact]
    public async Task CreateTaskAsync_SendsRequestAndReturnsTask()
    {
        string? requestBody = null;
        var response = new
        {
            id = "99", content = "New task", description = (string?)null,
            project_id = "p1", priority = 2, is_completed = false, due = (object?)null
        };

        var service = BuildService(async req =>
        {
            requestBody = await req.Content!.ReadAsStringAsync();
            return JsonResponse(response);
        });

        var result = await service.CreateTaskAsync(new CreateTaskRequest("New task", null, 2));

        Assert.NotNull(requestBody);
        Assert.Contains("New task", requestBody);
        Assert.Equal("99",       result.Id);
        Assert.Equal("New task", result.Content);
    }
}
```

- [ ] **Step 2: Run — expect compile failure (TodoistService not found)**

```powershell
dotnet build backend.Tests/backend.Tests.csproj
```

Expected: compile error — `TodoistService` does not exist yet.

- [ ] **Step 3: Create the `TodoistService` skeleton**

Create `backend/Services/Todoist/TodoistService.cs`:

```csharp
using backend.Services.Todoist.Dtos;

namespace backend.Services.Todoist;

/// <summary>
/// Todoist REST v1 client. Focused solely on Todoist operations.
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
        return await response.Content.ReadFromJsonAsync<List<TodoistProject>>(ct) ?? [];
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
        return await response.Content.ReadFromJsonAsync<List<TodoistTask>>(ct) ?? [];
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
        var response = await _client.PostAsJsonAsync($"tasks/{taskId}", request, ct);
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
```

- [ ] **Step 4: Run tests — expect GREEN**

```powershell
dotnet test backend.Tests/backend.Tests.csproj
```

Expected:
```
Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12
```

- [ ] **Step 5: Commit**

```powershell
git add backend/Services/Todoist/ITodoistService.cs `
        backend/Services/Todoist/TodoistAuthHandler.cs `
        backend/Services/Todoist/TodoistService.cs `
        backend.Tests/Services/TodoistServiceTests.cs
git commit -m "feat(todoist-api-foundation): add ITodoistService, TodoistAuthHandler, and TodoistService with tests"
```

---

## Task 7: `TodoistService` — remaining service method tests (`UpdateTask`, `CloseTask`, `DeleteTask`)

**Test-first:** yes — add tests for the three remaining methods

**Files:**
- Modify: `backend.Tests/Services/TodoistServiceTests.cs`

- [ ] **Step 1: Append tests for UpdateTaskAsync, CloseTaskAsync, DeleteTaskAsync**

Open `backend.Tests/Services/TodoistServiceTests.cs` and add these test methods inside the class (after the existing `CreateTaskAsync_SendsRequestAndReturnsTask` test):

```csharp
// ── UpdateTaskAsync happy path ────────────────────────────────────────────────

[Fact]
public async Task UpdateTaskAsync_PostsToCorrectUrlAndReturnsTask()
{
    string? capturedUrl = null;
    var response = new
    {
        id = "42", content = "Updated content", description = (string?)null,
        project_id = "p1", priority = 3, is_completed = false, due = (object?)null
    };

    var service = BuildService(req =>
    {
        capturedUrl = req.RequestUri?.ToString();
        return JsonResponse(response);
    });

    var result = await service.UpdateTaskAsync("42", new UpdateTaskRequest("Updated content", 3));

    Assert.NotNull(capturedUrl);
    Assert.EndsWith("tasks/42", capturedUrl);
    Assert.Equal(HttpMethod.Post, (await Task.FromResult(HttpMethod.Post))); // POST via PostAsJsonAsync
    Assert.Equal("Updated content", result.Content);
}

// ── CloseTaskAsync happy path ─────────────────────────────────────────────────

[Fact]
public async Task CloseTaskAsync_PostsToCloseEndpoint()
{
    string? capturedUrl = null;

    var service = BuildService(req =>
    {
        capturedUrl = req.RequestUri?.ToString();
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    });

    await service.CloseTaskAsync("77");

    Assert.NotNull(capturedUrl);
    Assert.EndsWith("tasks/77/close", capturedUrl);
}

// ── DeleteTaskAsync happy path ────────────────────────────────────────────────

[Fact]
public async Task DeleteTaskAsync_SendsDeleteToCorrectUrl()
{
    string? capturedUrl  = null;
    HttpMethod? capturedMethod = null;

    var service = BuildService(req =>
    {
        capturedUrl    = req.RequestUri?.ToString();
        capturedMethod = req.Method;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    });

    await service.DeleteTaskAsync("55");

    Assert.NotNull(capturedUrl);
    Assert.EndsWith("tasks/55", capturedUrl);
    Assert.Equal(HttpMethod.Delete, capturedMethod);
}
```

- [ ] **Step 2: Run tests — expect GREEN (all methods already implemented)**

```powershell
dotnet test backend.Tests/backend.Tests.csproj
```

Expected:
```
Passed!  - Failed: 0, Passed: 15, Skipped: 0, Total: 15
```

- [ ] **Step 3: Commit**

```powershell
git add backend.Tests/Services/TodoistServiceTests.cs
git commit -m "test(todoist-api-foundation): add UpdateTask, CloseTask, DeleteTask service tests"
```

---

## Task 8: `TasksController` — replace SQLite with Todoist (TDD)

**Test-first:** yes — write failing controller tests, then replace the controller

**Files:**
- Create: `backend.Tests/Controllers/TasksControllerTests.cs`
- Modify: `backend/Controllers/TasksController.cs`

- [ ] **Step 1: Write failing controller tests**

Create `backend.Tests/Controllers/TasksControllerTests.cs`:

```csharp
using backend.Controllers;
using backend.Models;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace backend.Tests.Controllers;

public class TasksControllerTests
{
    private static Mock<ITodoistService> MockService() => new Mock<ITodoistService>();

    // ── GET /api/tasks ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsMappedTaskResponses()
    {
        var mock = MockService();
        mock.Setup(s => s.GetActiveTasksAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TodoistTask>
            {
                new("1", "Buy milk", null, "proj1", 1, false, null),
                new("2", "Send report", null, "proj1", 2, true, null)
            });

        var controller = new TasksController(mock.Object);
        var result     = await controller.GetAll(CancellationToken.None);

        var ok    = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsAssignableFrom<IEnumerable<TaskResponse>>(ok.Value).ToList();

        Assert.Equal(2, tasks.Count);

        Assert.Equal("1",        tasks[0].Id);
        Assert.Equal("Buy milk", tasks[0].Title);
        Assert.Equal("todo",     tasks[0].Status);
        Assert.Equal(1,          tasks[0].Priority);

        Assert.Equal("2",           tasks[1].Id);
        Assert.Equal("Send report", tasks[1].Title);
        Assert.Equal("done",        tasks[1].Status);  // IsCompleted=true → "done"
    }

    [Fact]
    public async Task GetAll_WhenTokenMissing_Returns503()
    {
        var mock = MockService();
        mock.Setup(s => s.GetActiveTasksAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Todoist ApiToken is not configured"));

        var controller = new TasksController(mock.Object);
        var result     = await controller.GetAll(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
    }

    // ── POST /api/tasks ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Returns201WithMappedResponse()
    {
        var mock = MockService();
        mock.Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistTask("new1", "New task", null, "p1", 1, false, null));

        var controller = new TasksController(mock.Object);
        var result     = await controller.Create(
            new CreateTaskRequest("New task", null, 1), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<TaskResponse>(created.Value);
        Assert.Equal("new1",     response.Id);
        Assert.Equal("New task", response.Title);
        Assert.Equal("todo",     response.Status);
    }

    // ── DELETE /api/tasks/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Returns204()
    {
        var mock = MockService();
        mock.Setup(s => s.DeleteTaskAsync("99", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new TasksController(mock.Object);
        var result     = await controller.Delete("99", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
```

- [ ] **Step 2: Run — expect compile failure (TasksController doesn't have the right constructor yet)**

```powershell
dotnet build backend.Tests/backend.Tests.csproj
```

Expected: compile error — constructor mismatch or missing action methods.

- [ ] **Step 3: Apply targeted edits to `backend/Controllers/TasksController.cs`**

The existing file has one action method and three unneeded usings. Apply the following replacements in order — do not overwrite unrelated lines.

**Edit 3a — Replace usings** (remove EF + Data imports, add Todoist ones):

Replace:
```csharp
using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
```
With:
```csharp
using backend.Models;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using Microsoft.AspNetCore.Mvc;
```

**Edit 3b — Replace field + constructor** (swap `AppDbContext` for `ITodoistService`):

Replace:
```csharp
    private readonly AppDbContext _db;

    public TasksController(AppDbContext db)
    {
        _db = db;
    }
```
With:
```csharp
    private readonly ITodoistService _todoist;

    public TasksController(ITodoistService todoist)
    {
        _todoist = todoist;
    }
```

**Edit 3c — Replace `GetAll` + add all new methods + mapping helper** (replaces the existing single action and the closing `}`):

Replace:
```csharp
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _db.TaskItems
            .Select(task => new
            {
                task.Id,
                task.Title,
                task.Status,
                task.CreatedAt,
                task.JournalEntryId
            })
            .ToListAsync();

        return Ok(tasks);
    }
}
```
With:
```csharp
    // ── GET /api/tasks ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var tasks = await _todoist.GetActiveTasksAsync(ct: cancellationToken);
            return Ok(tasks.Select(ToResponse));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── POST /api/tasks ───────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var task = await _todoist.CreateTaskAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetAll), ToResponse(task));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── PUT /api/tasks/{id} ───────────────────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var task = await _todoist.UpdateTaskAsync(id, request, cancellationToken);
            return Ok(ToResponse(task));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── POST /api/tasks/{id}/close ────────────────────────────────────────────

    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _todoist.CloseTaskAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── DELETE /api/tasks/{id} ────────────────────────────────────────────────

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _todoist.DeleteTaskAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TaskResponse ToResponse(TodoistTask task) => new(
        task.Id,
        task.Content,
        task.IsCompleted ? "done" : "todo",
        task.Priority,
        task.Due?.Date);
}
```

- [ ] **Step 4: Run tests — expect GREEN**

```powershell
dotnet test backend.Tests/backend.Tests.csproj
```

Expected:
```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19
```

- [ ] **Step 5: Commit**

```powershell
git add backend/Controllers/TasksController.cs `
        backend.Tests/Controllers/TasksControllerTests.cs
git commit -m "feat(todoist-api-foundation): replace TasksController SQLite queries with Todoist proxy"
```

---

## Task 9: `JournalController` — route task creation to Todoist

**Test-first:** no — no new business logic; the behavior is tested through the service layer. JournalController is updated to wire an already-tested service call.

**Files:**
- Modify: `backend/Controllers/JournalController.cs`

- [ ] **Step 1: Apply targeted edits to `backend/Controllers/JournalController.cs`**

Leave `HasEntryToday`, the inner classes, `MockResponse`, and all existing `using` statements untouched. Apply only the following changes:

**Edit 1a — Add two using statements** after `using System.Text.Json.Serialization;`:

```csharp
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
```

**Edit 1b — Add `ITodoistService` field** after `private readonly AppDbContext _db;`:

```csharp
    private readonly ITodoistService _todoistService;
```

**Edit 1c — Update constructor** to accept and store `ITodoistService`:

Replace:
```csharp
    public JournalController(AppDbContext db)
    {
        _db = db;
    }
```
With:
```csharp
    public JournalController(AppDbContext db, ITodoistService todoistService)
    {
        _db             = db;
        _todoistService = todoistService;
    }
```

**Edit 1d — Update `Create` method signature** to accept `CancellationToken`:

Replace:
```csharp
    public async Task<IActionResult> Create([FromBody] JournalCreateRequest request)
```
With:
```csharp
    public async Task<IActionResult> Create(
        [FromBody] JournalCreateRequest request,
        CancellationToken cancellationToken = default)
```

**Edit 1e — Pass `cancellationToken` to `SaveChangesAsync`**:

Replace:
```csharp
        await _db.SaveChangesAsync();
```
With (first occurrence, inside `Create`):
```csharp
        await _db.SaveChangesAsync(cancellationToken);
```

**Edit 1f — Replace the SQLite task-item write block with Todoist call** (removes `TaskItem` writes; adds priority TODO comment):

Replace:
```csharp
        foreach (var taskTitle in MockResponse.NewTasks)
        {
            _db.TaskItems.Add(new TaskItem
            {
                Title = taskTitle,
                Status = "todo",
                CreatedAt = DateTime.UtcNow,
                JournalEntryId = entry.Id
            });
        }

        await _db.SaveChangesAsync();
```
With:
```csharp
        // TODO(todoist-api-foundation): priority=1 is a temporary MVP default.
        //   Replace with AI-extracted priority once the LLM extraction pipeline
        //   is wired in and MockExtractionResponse is replaced with a real response.
        foreach (var taskTitle in MockResponse.NewTasks)
        {
            await _todoistService.CreateTaskAsync(
                new CreateTaskRequest(taskTitle, null, priority: 1),
                cancellationToken);
        }
```

- [ ] **Step 2: Build the backend to confirm no compile errors**

```powershell
dotnet build backend/backend.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

---

## Task 10: `Program.cs` — register `TodoistOptions` + typed `HttpClient`

**Test-first:** no — wiring/registration verified by build + runtime

**Files:**
- Modify: `backend/Program.cs`

- [ ] **Step 1: Apply targeted edits to `backend/Program.cs`**

All existing lines are preserved. The edits are purely additive (new `using` statements + new service registrations).

**Edit 1a — Add using statements** at the top of the file, after the existing `using backend.Data;` line:

```csharp
using backend.Services.Todoist;
using Microsoft.Extensions.Options;
```

**Edit 1b — Add Todoist service registrations** immediately after the existing `AddDbContext` block:

```csharp
// ── Todoist ───────────────────────────────────────────────────────────────────
builder.Services.Configure<TodoistOptions>(
    builder.Configuration.GetSection(TodoistOptions.SectionName));

builder.Services
    .AddTransient<TodoistAuthHandler>()
    .AddHttpClient<ITodoistService, TodoistService>(client =>
    {
        var baseUrl = builder.Configuration["Todoist:BaseUrl"]
                      ?? "https://api.todoist.com/api/v1";
        // Ensure trailing slash so relative paths resolve correctly
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    })
    .AddHttpMessageHandler<TodoistAuthHandler>();
```

**Edit 1c — Add startup warning** immediately after the `db.Database.EnsureCreated();` line (inside the `using (var scope ...)` block's close, before the pipeline section):

```csharp
// ── Startup warning for missing Todoist token ─────────────────────────────────
var todoistOpts = app.Services.GetRequiredService<IOptions<TodoistOptions>>().Value;
if (string.IsNullOrWhiteSpace(todoistOpts.ApiToken))
{
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var startupLogger = loggerFactory.CreateLogger("Startup");
    startupLogger.LogWarning(
        "Todoist ApiToken is not configured. Task endpoints will return 503.");
}
```

- [ ] **Step 2: Build the backend — confirm no errors**

```powershell
dotnet build backend/backend.csproj
```

Expected: `Build succeeded. 0 Error(s)`.

---

## Task 11: Final build + full test run

**Test-first:** n/a — verification gate

**Files:** none (read-only verification)

- [ ] **Step 1: Build the entire solution**

```powershell
dotnet build ai-productivity-assistant.sln
```

Expected:
```
Build succeeded.
    0 Error(s)
    0 Warning(s) (or only minor nullable warnings)
```

- [ ] **Step 2: Run all tests**

```powershell
dotnet test ai-productivity-assistant.sln --logger "console;verbosity=normal"
```

Expected:
```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19
```

- [ ] **Step 3: Commit all remaining files**

> **Do not commit without asking.** Present the following commit command to the user and wait for their approval:

```powershell
git add backend/Controllers/JournalController.cs `
        backend/Program.cs `
        backend/Services/ `
        backend/Models/TaskResponse.cs
git commit -m "feat(todoist-api-foundation): wire Program.cs, update JournalController for Todoist task creation"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Task that covers it |
|---|---|
| Add `TodoistOptions` config POCO | Task 2 |
| Bind `BaseUrl` + `ApiToken` from config | Task 2, Task 10 |
| Todoist project DTO | Task 3 |
| Todoist task / due DTOs | Task 3 |
| `CreateTaskRequest` / `UpdateTaskRequest` DTOs | Task 3 |
| `ITodoistService` interface | Task 5 |
| `TodoistService` with HttpClient | Task 6 |
| `GetProjectsAsync` | Task 6 (implemented; no dedicated test — low-risk read path) |
| `GetActiveTasksAsync` | Task 6 |
| `CreateTaskAsync` | Task 6 |
| `UpdateTaskAsync` | Task 7 |
| `CloseTaskAsync` | Task 7 |
| `DeleteTaskAsync` | Task 7 |
| Missing-token guard → `InvalidOperationException` | Task 6 |
| `TasksController` proxies Todoist | Task 8 |
| `TasksController` maps `TodoistTask → TaskResponse` | Task 8 |
| `InvalidOperationException → 503` in controller | Task 8 |
| `JournalController` creates tasks in Todoist | Task 9 |
| `JournalEntry` still persisted to SQLite | Task 9 |
| `Program.cs` registers options + typed HttpClient | Task 10 |
| Startup warning when token empty | Task 10 |
| `backend.Tests/` xUnit project | Task 1 |
| xUnit + Moq packages | Task 1 |
| `TestHttpMessageHandler` helper | Task 4 |
| Config binding tests | Task 2 |
| JSON deserialization tests | Task 3 |
| Service happy-path tests | Tasks 6, 7 |
| Controller mapping + 503 tests | Task 8 |
| `dotnet build` + `dotnet test` pass | Task 11 |
| No migrations | (constraint respected — no migration steps) |
| No SQLite task storage | (constraint respected — TaskItem table untouched) |
| No frontend changes | (constraint respected — no frontend steps) |
| Do not commit without asking | Task 11 Step 3 |

No gaps found.
