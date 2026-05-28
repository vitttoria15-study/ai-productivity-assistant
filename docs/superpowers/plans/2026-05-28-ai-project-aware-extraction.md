# AI Project-Aware Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route AI-extracted new tasks to the correct Todoist project when the journal entry names one, falling back to Inbox otherwise.

**Architecture:** Add an optional `routed_tasks` parallel field to `ExtractionResult` so the AI can return `{title, project}` objects alongside the existing `new_tasks` string array. `JournalExtractionService` fetches the Todoist project list before calling the AI, injects project names into the prompt, then resolves project names to IDs when creating tasks. The frontend contract (`new_tasks: string[]`) is untouched.

**Tech Stack:** C# 12 / .NET 8, xUnit 2.5, Moq 4.20, existing `ITodoistService` / `IAiProvider` abstractions.

> **Commit policy:** Each task ends with a commit step. **Confirm with the user before running any `git commit` command.**

---

## File map

| File | Change |
|---|---|
| `backend/Services/Ai/ExtractedNewTask.cs` | **Create** — new record type |
| `backend/Services/Ai/ExtractionResult.cs` | **Modify** — add `RoutedTasks` field |
| `backend/Services/Ai/ExtractionValidator.cs` | **Modify** — add optional `RoutedTasks` validation |
| `backend/Services/Ai/JournalExtractionService.cs` | **Modify** — fetch projects, update `BuildPrompt`, replace task-creation loop |
| `backend.Tests/Services/Ai/ExtractionValidatorTests.cs` | **Modify** — add 5 `RoutedTasks` test cases |
| `backend.Tests/Services/Ai/JournalExtractionServiceTests.cs` | **Modify** — add 7 routing test cases |

---

## Task 1: Add `ExtractedNewTask` type and extend `ExtractionResult`

**Test-first: yes — `TryParse_RoutedTasksPresent_Valid_ReturnsTrue` fails because `ExtractionResult` has no `RoutedTasks` field.**

**Files:**
- Create: `backend/Services/Ai/ExtractedNewTask.cs`
- Modify: `backend/Services/Ai/ExtractionResult.cs`
- Test: `backend.Tests/Services/Ai/ExtractionValidatorTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `ExtractionValidatorTests.cs` after the existing happy-path test:

```csharp
// ── RoutedTasks — structural deserialization ────────────────────────────

[Fact]
public void TryParse_RoutedTasksAbsent_ReturnsTrue()
{
    const string json = """
        {"completed_tasks":[],"new_tasks":["Task A"],"blockers":[],"priorities":[],"summary":""}
        """;

    var ok = ExtractionValidator.TryParse(json, out var result, out _);

    Assert.True(ok);
    Assert.Null(result!.RoutedTasks);
}

[Fact]
public void TryParse_RoutedTasksPresent_Valid_ReturnsTrue()
{
    const string json = """
        {
          "completed_tasks": [],
          "new_tasks": ["Buy milk"],
          "routed_tasks": [{"title": "Buy milk", "project": "Shopping"}],
          "blockers": [],
          "priorities": [],
          "summary": ""
        }
        """;

    var ok = ExtractionValidator.TryParse(json, out var result, out _);

    Assert.True(ok);
    Assert.NotNull(result!.RoutedTasks);
    Assert.Single(result.RoutedTasks!);
    Assert.Equal("Buy milk", result.RoutedTasks![0].Title);
    Assert.Equal("Shopping", result.RoutedTasks[0].Project);
}

[Fact]
public void TryParse_RoutedTasksProjectNull_ReturnsTrue()
{
    const string json = """
        {
          "completed_tasks": [],
          "new_tasks": ["Write report"],
          "routed_tasks": [{"title": "Write report", "project": null}],
          "blockers": [],
          "priorities": [],
          "summary": ""
        }
        """;

    var ok = ExtractionValidator.TryParse(json, out _, out _);

    Assert.True(ok);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```
dotnet test backend.Tests/ --filter "FullyQualifiedName~TryParse_RoutedTasksPresent_Valid_ReturnsTrue"
```

Expected: FAIL — `ExtractionResult` has no `RoutedTasks` property (compile error or property-not-found assertion failure).

- [ ] **Step 3: Create `ExtractedNewTask.cs`**

Create `backend/Services/Ai/ExtractedNewTask.cs`:

```csharp
using System.Text.Json.Serialization;

namespace backend.Services.Ai;

public sealed record ExtractedNewTask(
    [property: JsonPropertyName("title")]   string  Title,
    [property: JsonPropertyName("project")] string? Project);
```

- [ ] **Step 4: Update `ExtractionResult.cs`**

Replace the entire file content:

```csharp
using System.Text.Json.Serialization;

namespace backend.Services.Ai;

public sealed record ExtractionResult(
    [property: JsonPropertyName("completed_tasks")] string[]            CompletedTasks,
    [property: JsonPropertyName("new_tasks")]        string[]            NewTasks,
    [property: JsonPropertyName("routed_tasks")]     ExtractedNewTask[]? RoutedTasks,
    [property: JsonPropertyName("blockers")]         string[]            Blockers,
    [property: JsonPropertyName("priorities")]       string[]            Priorities,
    [property: JsonPropertyName("summary")]          string              Summary);
```

- [ ] **Step 5: Run all three new tests and the existing test suite**

```
dotnet test backend.Tests/ --filter "FullyQualifiedName~ExtractionValidatorTests"
```

Expected: all 8 tests pass (5 existing + 3 new).

- [ ] **Step 6: Confirm with user, then commit**

```bash
git add backend/Services/Ai/ExtractedNewTask.cs \
        backend/Services/Ai/ExtractionResult.cs \
        backend.Tests/Services/Ai/ExtractionValidatorTests.cs
git commit -m "feat(ai-project-aware-extraction): add ExtractedNewTask type and RoutedTasks field"
```

---

## Task 2: Extend `ExtractionValidator` with `RoutedTasks` length validation

**Test-first: yes — `TryParse_RoutedTasksTitleExceeds500Chars_ReturnsFalse` passes before this task (validator does not yet check RoutedTasks items), so it is currently GREEN when it should be RED.**

Wait — after Task 1 the validator does not validate RoutedTasks items at all. A title of 501 chars inside `routed_tasks` would deserialize fine and pass validation. The test below should be RED until this task is implemented.

**Files:**
- Modify: `backend/Services/Ai/ExtractionValidator.cs`
- Test: `backend.Tests/Services/Ai/ExtractionValidatorTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `ExtractionValidatorTests.cs` after the three tests from Task 1:

```csharp
// ── RoutedTasks — length validation ────────────────────────────────────

[Fact]
public void TryParse_RoutedTasksTitleExceeds500Chars_ReturnsFalse()
{
    var longTitle = new string('x', 501);
    var json = $$"""
        {
          "completed_tasks": [],
          "new_tasks": ["ok"],
          "routed_tasks": [{"title": "{{longTitle}}", "project": null}],
          "blockers": [],
          "priorities": [],
          "summary": ""
        }
        """;

    var ok = ExtractionValidator.TryParse(json, out _, out var error);

    Assert.False(ok);
    Assert.NotNull(error);
}

[Fact]
public void TryParse_RoutedTasksProjectExceeds200Chars_ReturnsFalse()
{
    var longProject = new string('x', 201);
    var json = $$"""
        {
          "completed_tasks": [],
          "new_tasks": ["ok"],
          "routed_tasks": [{"title": "ok", "project": "{{longProject}}"}],
          "blockers": [],
          "priorities": [],
          "summary": ""
        }
        """;

    var ok = ExtractionValidator.TryParse(json, out _, out var error);

    Assert.False(ok);
    Assert.NotNull(error);
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

```
dotnet test backend.Tests/ --filter "FullyQualifiedName~TryParse_RoutedTasksTitleExceeds500Chars_ReturnsFalse|FullyQualifiedName~TryParse_RoutedTasksProjectExceeds200Chars_ReturnsFalse"
```

Expected: FAIL — validator currently returns `true` for both.

- [ ] **Step 3: Add `RoutedTasks` validation to `ExtractionValidator.cs`**

Add after the `result.Summary.Length > MaxSummaryLength` block (before `return true`):

```csharp
if (result.RoutedTasks is not null)
{
    const int maxProjectLength = 200;
    foreach (var task in result.RoutedTasks)
    {
        if (task.Title is null || task.Title.Length > MaxItemLength)
        {
            error = $"AI response contains a routed_tasks title that is null or " +
                    $"exceeds {MaxItemLength} characters.";
            return false;
        }

        if (task.Project is not null && task.Project.Length > maxProjectLength)
        {
            error = $"AI response contains a routed_tasks project name exceeding " +
                    $"{maxProjectLength} characters.";
            return false;
        }
    }
}
```

- [ ] **Step 4: Run all validator tests**

```
dotnet test backend.Tests/ --filter "FullyQualifiedName~ExtractionValidatorTests"
```

Expected: all 10 tests pass.

- [ ] **Step 5: Confirm with user, then commit**

```bash
git add backend/Services/Ai/ExtractionValidator.cs \
        backend.Tests/Services/Ai/ExtractionValidatorTests.cs
git commit -m "feat(ai-project-aware-extraction): extend ExtractionValidator for RoutedTasks"
```

---

## Task 3: Update `BuildPrompt` to inject project names

**Test-first: yes — `ExtractAsync_WithProjects_InjectsProjectNamesIntoUserMessage` fails because `GetProjectsAsync` is not called and the user message contains no project block.**

**Files:**
- Modify: `backend/Services/Ai/JournalExtractionService.cs`
- Test: `backend.Tests/Services/Ai/JournalExtractionServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `JournalExtractionServiceTests.cs` after the existing four tests:

```csharp
// ── Project-aware prompt injection ─────────────────────────────────────

[Fact]
public async Task ExtractAsync_WithProjects_InjectsProjectNamesIntoUserMessage()
{
    _todoistMock
        .Setup(s => s.GetProjectsAsync(default))
        .ReturnsAsync(new List<TodoistProject>
        {
            new("work-id", "Work", "blue", 1, false),
            new("personal-id", "Personal", "green", 2, false),
        });
    _todoistMock
        .Setup(s => s.GetActiveTasksAsync(null, default))
        .ReturnsAsync(new List<TodoistTask>());

    string capturedUser = string.Empty;
    _aiMock
        .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
        .Callback<string, string, CancellationToken>((_, user, _) => capturedUser = user)
        .ReturnsAsync("""
            {"completed_tasks":[],"new_tasks":[],"blockers":[],"priorities":[],"summary":""}
            """);

    var svc = BuildService();
    await svc.ExtractAsync("Quick journal entry.");

    Assert.Contains("Available Todoist projects:", capturedUser);
    Assert.Contains("- Work", capturedUser);
    Assert.Contains("- Personal", capturedUser);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```
dotnet test backend.Tests/ --filter "FullyQualifiedName~ExtractAsync_WithProjects_InjectsProjectNamesIntoUserMessage"
```

Expected: FAIL — `capturedUser` does not contain `"Available Todoist projects:"`.

- [ ] **Step 3: Add `GetProjectsAsync` call to `ExtractAsync`**

In `JournalExtractionService.cs`, add the following block immediately before the `// ── Step 2` comment (active-tasks fetch):

```csharp
// ── Step 2a: Fetch Todoist projects for task routing (soft failure) ──────
IReadOnlyList<TodoistProject>? projects = null;
try
{
    projects = await _todoist.GetProjectsAsync(ct);
}
catch (Exception ex)
{
    _log.LogWarning(ex,
        "Failed to fetch Todoist projects. Project routing disabled for this extraction.");
}
```

- [ ] **Step 4: Update the `BuildPrompt` call in `ExtractAsync`**

Change line 63 from:

```csharp
var (system, user) = BuildPrompt(journalText, activeTasks);
```

to:

```csharp
var (system, user) = BuildPrompt(journalText, activeTasks, projects);
```

- [ ] **Step 5: Replace the `BuildPrompt` method**

Replace the entire `BuildPrompt` private static method with:

```csharp
private static (string system, string user) BuildPrompt(
    string journalText,
    IReadOnlyList<TodoistTask>?    activeTasks,
    IReadOnlyList<TodoistProject>? projects)
{
    const string systemBase = """
        You are a productivity assistant. Extract structured information from the user's journal entry.
        Return ONLY valid JSON. No markdown. No explanations. No code blocks.

        Schema:
        {
          "completed_tasks": ["string"],
          "new_tasks":       ["string"],
          "blockers":        ["string"],
          "priorities":      ["string"],
          "summary":         "string"
        }

        Rules:
        - completed_tasks: titles of tasks the user says they finished. Match exactly from the
          provided Active tasks list. If no active tasks list is provided, return [].
        - new_tasks: tasks the user mentions planning to do that are NOT in the active tasks list.
        - blockers: obstacles, blockers, or dependencies the user is waiting on.
        - priorities: anything the user flags as important or urgent.
        - summary: 1-2 sentences describing the day's work.
        - Return empty arrays if nothing is found. Never omit a field.
        """;

    var hasProjects = projects is { Count: > 0 };

    var systemPrompt = hasProjects
        ? systemBase + """

            If a list of Todoist project names is provided in the user message, you MUST also
            return a routed_tasks array. It must have the same length as new_tasks and be in the
            same order (routed_tasks[i] describes new_tasks[i]). Each item must contain:
            - "title": the task title, identical to the corresponding entry in new_tasks.
            - "project": one of the provided project names verbatim, or null if the journal
              entry does not clearly name a project for this task.
            Do NOT invent project names. Only use names from the provided list.
            Updated schema when projects are provided:
            {
              "completed_tasks": ["string"],
              "new_tasks":       ["string"],
              "routed_tasks":    [{"title": "string", "project": "string or null"}],
              "blockers":        ["string"],
              "priorities":      ["string"],
              "summary":         "string"
            }
            """
        : systemBase;

    var sb = new StringBuilder();

    if (hasProjects)
    {
        sb.AppendLine("Available Todoist projects:");
        foreach (var p in projects!)
            sb.AppendLine($"- {p.Name}");
        sb.AppendLine();
    }

    if (activeTasks is { Count: > 0 })
    {
        sb.AppendLine("Active tasks:");
        foreach (var t in activeTasks)
            sb.AppendLine($"- {t.Content}");
        sb.AppendLine();
    }

    sb.AppendLine("Journal entry:");
    sb.Append(journalText);

    return (systemPrompt, sb.ToString());
}
```

- [ ] **Step 6: Run all tests**

```
dotnet test backend.Tests/
```

Expected: all existing tests pass + new prompt-injection test passes.

- [ ] **Step 7: Confirm with user, then commit**

```bash
git add backend/Services/Ai/JournalExtractionService.cs \
        backend.Tests/Services/Ai/JournalExtractionServiceTests.cs
git commit -m "feat(ai-project-aware-extraction): update BuildPrompt to inject Todoist project names"
```

---

## Task 4: Route new-task creation through `RoutedTasks`

**Test-first: yes — `ExtractAsync_RoutedTasksWithMatchingProject_CreatesTaskInProject` fails because the service always passes `ProjectId = null`.**

**Files:**
- Modify: `backend/Services/Ai/JournalExtractionService.cs`
- Test: `backend.Tests/Services/Ai/JournalExtractionServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `JournalExtractionServiceTests.cs`:

```csharp
// ── Routing: matched project ────────────────────────────────────────────

[Fact]
public async Task ExtractAsync_RoutedTasksWithMatchingProject_CreatesTaskInProject()
{
    _todoistMock
        .Setup(s => s.GetProjectsAsync(default))
        .ReturnsAsync(new List<TodoistProject>
        {
            new("work-id", "Work", "blue", 1, false),
            new("personal-id", "Personal", "green", 2, false),
        });
    _todoistMock
        .Setup(s => s.GetActiveTasksAsync(null, default))
        .ReturnsAsync(new List<TodoistTask>());
    _aiMock
        .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
        .ReturnsAsync("""
            {
              "completed_tasks": [],
              "new_tasks": ["Write spec"],
              "routed_tasks": [{"title": "Write spec", "project": "Work"}],
              "blockers": [],
              "priorities": [],
              "summary": "Worked on spec."
            }
            """);
    _todoistMock
        .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
        .ReturnsAsync(new TodoistTask("id1", "Write spec", null, "work-id", 1, null, null));

    var svc = BuildService();
    var result = await svc.ExtractAsync("Worked on spec for Work project.");

    Assert.Equal("ok", result.ExtractionStatus);
    _todoistMock.Verify(
        s => s.CreateTaskAsync(
            It.Is<CreateTaskRequest>(r => r.Content == "Write spec" && r.ProjectId == "work-id"),
            default),
        Times.Once);
}

// ── Routing: unknown project → Inbox ────────────────────────────────────

[Fact]
public async Task ExtractAsync_RoutedTasksWithUnknownProject_FallsBackToInbox()
{
    _todoistMock
        .Setup(s => s.GetProjectsAsync(default))
        .ReturnsAsync(new List<TodoistProject>
        {
            new("work-id", "Work", "blue", 1, false),
        });
    _todoistMock
        .Setup(s => s.GetActiveTasksAsync(null, default))
        .ReturnsAsync(new List<TodoistTask>());
    _aiMock
        .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
        .ReturnsAsync("""
            {
              "completed_tasks": [],
              "new_tasks": ["Buy milk"],
              "routed_tasks": [{"title": "Buy milk", "project": "Shopping"}],
              "blockers": [],
              "priorities": [],
              "summary": "Grocery run needed."
            }
            """);
    _todoistMock
        .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
        .ReturnsAsync(new TodoistTask("id1", "Buy milk", null, "inbox-id", 1, null, null));

    var svc = BuildService();
    var result = await svc.ExtractAsync("Need to buy milk.");

    Assert.Equal("ok", result.ExtractionStatus);
    _todoistMock.Verify(
        s => s.CreateTaskAsync(
            It.Is<CreateTaskRequest>(r => r.Content == "Buy milk" && r.ProjectId == null),
            default),
        Times.Once);
}

// ── Routing: routed_tasks absent → fallback ─────────────────────────────

[Fact]
public async Task ExtractAsync_RoutedTasksAbsent_FallsBackToNewTasksInbox()
{
    _todoistMock
        .Setup(s => s.GetProjectsAsync(default))
        .ReturnsAsync(new List<TodoistProject>
        {
            new("work-id", "Work", "blue", 1, false),
        });
    _todoistMock
        .Setup(s => s.GetActiveTasksAsync(null, default))
        .ReturnsAsync(new List<TodoistTask>());
    _aiMock
        .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
        .ReturnsAsync("""
            {
              "completed_tasks": [],
              "new_tasks": ["Deploy service"],
              "blockers": [],
              "priorities": [],
              "summary": "Planning deployment."
            }
            """);
    _todoistMock
        .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
        .ReturnsAsync(new TodoistTask("id1", "Deploy service", null, "inbox-id", 1, null, null));

    var svc = BuildService();
    var result = await svc.ExtractAsync("Need to deploy service.");

    Assert.Equal("ok", result.ExtractionStatus);
    _todoistMock.Verify(
        s => s.CreateTaskAsync(
            It.Is<CreateTaskRequest>(r => r.Content == "Deploy service" && r.ProjectId == null),
            default),
        Times.Once);
}

// ── Routing: count mismatch → fallback ──────────────────────────────────

[Fact]
public async Task ExtractAsync_RoutedTasksCountMismatch_FallsBackToInbox()
{
    _todoistMock
        .Setup(s => s.GetProjectsAsync(default))
        .ReturnsAsync(new List<TodoistProject>
        {
            new("work-id", "Work", "blue", 1, false),
        });
    _todoistMock
        .Setup(s => s.GetActiveTasksAsync(null, default))
        .ReturnsAsync(new List<TodoistTask>());
    _aiMock
        .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
        .ReturnsAsync("""
            {
              "completed_tasks": [],
              "new_tasks": ["Task A", "Task B"],
              "routed_tasks": [{"title": "Task A", "project": "Work"}],
              "blockers": [],
              "priorities": [],
              "summary": "Two tasks."
            }
            """);
    _todoistMock
        .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
        .ReturnsAsync(new TodoistTask("id1", "Task A", null, "inbox-id", 1, null, null));

    var svc = BuildService();
    var result = await svc.ExtractAsync("Need to do Task A and Task B.");

    Assert.Equal("ok", result.ExtractionStatus);
    // Both tasks routed to Inbox (fallback path, count mismatch)
    _todoistMock.Verify(
        s => s.CreateTaskAsync(
            It.Is<CreateTaskRequest>(r => r.ProjectId == null),
            default),
        Times.Exactly(2));
}

// ── Routing: projects fetch fails → Inbox ───────────────────────────────

[Fact]
public async Task ExtractAsync_ProjectsFetchFails_CreatesTasksInInbox()
{
    _todoistMock
        .Setup(s => s.GetProjectsAsync(default))
        .ThrowsAsync(new HttpRequestException("Todoist unavailable"));
    _todoistMock
        .Setup(s => s.GetActiveTasksAsync(null, default))
        .ReturnsAsync(new List<TodoistTask>());
    _aiMock
        .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
        .ReturnsAsync("""
            {
              "completed_tasks": [],
              "new_tasks": ["Write report"],
              "blockers": [],
              "priorities": [],
              "summary": "Writing report."
            }
            """);
    _todoistMock
        .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
        .ReturnsAsync(new TodoistTask("id1", "Write report", null, "inbox-id", 1, null, null));

    var svc = BuildService();
    var result = await svc.ExtractAsync("Need to write report.");

    Assert.Equal("ok", result.ExtractionStatus);
    _todoistMock.Verify(
        s => s.CreateTaskAsync(
            It.Is<CreateTaskRequest>(r => r.Content == "Write report" && r.ProjectId == null),
            default),
        Times.Once);
}

// ── Routing: duplicate project names → first match wins ─────────────────

[Fact]
public async Task ExtractAsync_DuplicateProjectNames_UsesFirstMatch()
{
    _todoistMock
        .Setup(s => s.GetProjectsAsync(default))
        .ReturnsAsync(new List<TodoistProject>
        {
            new("work-id-1", "Work", "blue",  1, false),
            new("work-id-2", "WORK", "green", 2, false), // duplicate (case-insensitive)
        });
    _todoistMock
        .Setup(s => s.GetActiveTasksAsync(null, default))
        .ReturnsAsync(new List<TodoistTask>());
    _aiMock
        .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
        .ReturnsAsync("""
            {
              "completed_tasks": [],
              "new_tasks": ["Write spec"],
              "routed_tasks": [{"title": "Write spec", "project": "Work"}],
              "blockers": [],
              "priorities": [],
              "summary": "Spec work."
            }
            """);
    _todoistMock
        .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
        .ReturnsAsync(new TodoistTask("id1", "Write spec", null, "work-id-1", 1, null, null));

    var svc = BuildService();
    var result = await svc.ExtractAsync("Write spec for Work.");

    Assert.Equal("ok", result.ExtractionStatus);
    _todoistMock.Verify(
        s => s.CreateTaskAsync(
            It.Is<CreateTaskRequest>(r => r.Content == "Write spec" && r.ProjectId == "work-id-1"),
            default),
        Times.Once);
}
```

- [ ] **Step 2: Run the failing tests**

```
dotnet test backend.Tests/ --filter "FullyQualifiedName~ExtractAsync_RoutedTasksWithMatchingProject_CreatesTaskInProject"
```

Expected: FAIL — `CreateTaskAsync` is called with `ProjectId == null` instead of `"work-id"`.

- [ ] **Step 3: Replace the new-task creation block in `ExtractAsync`**

In `JournalExtractionService.cs`, replace the block starting with `foreach (var taskTitle in result!.NewTasks)` and ending before `if (closeDict != null)` with:

```csharp
// ── Build project-name → ID lookup (first match wins for duplicates) ─────
var projectLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
if (projects is not null)
{
    foreach (var proj in projects)
    {
        if (!projectLookup.TryAdd(proj.Name, proj.Id))
            _log.LogWarning(
                "Duplicate Todoist project name '{Name}' (case-insensitive). First match wins.",
                proj.Name);
    }
}

// ── Routing guard ─────────────────────────────────────────────────────────
var useRoutedPath = result!.RoutedTasks is { Length: > 0 };
if (useRoutedPath && result.RoutedTasks!.Length != result.NewTasks.Length)
{
    _log.LogWarning(
        "routed_tasks length ({RoutedCount}) != new_tasks length ({NewCount}). " +
        "Falling back to Inbox creation.",
        result.RoutedTasks.Length, result.NewTasks.Length);
    useRoutedPath = false;
}

if (useRoutedPath)
{
    for (var i = 0; i < result.RoutedTasks!.Length; i++)
    {
        var routedTask = result.RoutedTasks[i];

        if (routedTask.Title != result.NewTasks[i])
            _log.LogWarning(
                "routed_tasks[{Index}].Title '{Routed}' != new_tasks[{Index}] '{Original}'. " +
                "Using routed title.",
                i, routedTask.Title, i, result.NewTasks[i]);

        string? projectId = null;
        if (routedTask.Project is not null)
        {
            if (projectLookup.TryGetValue(routedTask.Project, out var pid))
                projectId = pid;
            else
                _log.LogWarning(
                    "AI selected project '{Name}' which is not in the Todoist project list. " +
                    "Task will go to Inbox.",
                    routedTask.Project);
        }

        try
        {
            await _todoist.CreateTaskAsync(
                new CreateTaskRequest(routedTask.Title, projectId, Priority: 1), ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Failed to create Todoist task '{Title}'. Skipping.", routedTask.Title);
        }
    }
}
else
{
    foreach (var taskTitle in result!.NewTasks)
    {
        try
        {
            await _todoist.CreateTaskAsync(
                new CreateTaskRequest(taskTitle, null, Priority: 1), ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Failed to create Todoist task '{Title}'. Skipping.", taskTitle);
        }
    }
}
```

- [ ] **Step 4: Run all tests**

```
dotnet test backend.Tests/
```

Expected: all tests pass — existing 4 service tests + 7 new routing tests + 10 validator tests + all other test classes.

- [ ] **Step 5: Confirm with user, then commit**

```bash
git add backend/Services/Ai/JournalExtractionService.cs \
        backend.Tests/Services/Ai/JournalExtractionServiceTests.cs
git commit -m "feat(ai-project-aware-extraction): route new tasks to Todoist projects via routed_tasks"
```
