# AI Project-Aware Extraction — Design Spec

**Date:** 2026-05-28  
**Branch:** feature/ai-project-aware-extraction  
**Slug:** ai-project-aware-extraction

## Goal

When a journal entry explicitly refers to an existing Todoist project, AI-created tasks are
placed in that project. When it does not, tasks continue to go to Inbox. The frontend contract
and database schema are unchanged.

---

## Scope

**In scope:**
- Fetching Todoist projects before AI extraction and injecting their names into the prompt
- New `routed_tasks` field in the AI response carrying `{ title, project }` objects
- Backend project-name → project-ID resolution with fallback to Inbox
- Unit tests for the validator and service changes

**Out of scope:**
- Creating new Todoist projects
- Storing project/task records in SQLite
- Frontend changes
- Database migrations

---

## Types

### `ExtractedNewTask` (new file: `Services/Ai/ExtractedNewTask.cs`)

```csharp
sealed record ExtractedNewTask(
    [property: JsonPropertyName("title")]   string  Title,
    [property: JsonPropertyName("project")] string? Project);
```

### `ExtractionResult` (updated: `Services/Ai/ExtractionResult.cs`)

One field added; all existing fields unchanged.

| JSON key         | C# property    | Type                   | Required |
|------------------|----------------|------------------------|----------|
| `completed_tasks`| CompletedTasks | `string[]`             | yes      |
| `new_tasks`      | NewTasks       | `string[]`             | yes      |
| `routed_tasks`   | RoutedTasks    | `ExtractedNewTask[]?`  | **no**   |
| `blockers`       | Blockers       | `string[]`             | yes      |
| `priorities`     | Priorities     | `string[]`             | yes      |
| `summary`        | Summary        | `string`               | yes      |

`JournalExtractionResponse` is **not changed**. The frontend continues to receive
`new_tasks: string[]`.

---

## Validation (`ExtractionValidator`)

Existing rules are unchanged. One optional rule added:

- If `RoutedTasks` is non-null:
  - Each `Title` must be non-null and ≤ 500 characters.
  - Each `Project`, when non-null, must be ≤ 200 characters.
  - Whether a project name exists in Todoist is **not** checked here — that is the
    service's responsibility.
- If `RoutedTasks` is null, validation passes without further checks.

---

## Prompt changes (`JournalExtractionService.BuildPrompt`)

### Signature

```csharp
private static (string system, string user) BuildPrompt(
    string journalText,
    IReadOnlyList<TodoistTask>?   activeTasks,
    IReadOnlyList<TodoistProject>? projects)   // new
```

### System prompt addition

Appended to the existing system prompt verbatim string:

> If a list of Todoist project names is provided below, you MUST also return a
> `routed_tasks` array. It must have the **same length** as `new_tasks` and be in the
> **same order** (`routed_tasks[i]` describes `new_tasks[i]`). Each item must contain:
> - `"title"`: the task title, identical to the corresponding entry in `new_tasks`.
> - `"project"`: one of the provided project names **verbatim**, or `null` if the
>   journal entry does not clearly name a project for this task.
>
> Do **not** invent project names. Only use names from the provided list.  
> If no project list is provided, omit `routed_tasks` entirely.

Updated example JSON schema in the prompt:

```json
{
  "completed_tasks": [],
  "new_tasks": ["Buy milk", "Write report"],
  "routed_tasks": [
    {"title": "Buy milk",     "project": "Shopping"},
    {"title": "Write report", "project": null}
  ],
  "blockers":   [],
  "priorities": [],
  "summary":    ""
}
```

### User message addition

When `projects` is non-null and non-empty, prepend before the active-tasks block:

```
Available Todoist projects:
- Shopping
- Work
- Personal
```

---

## Service changes (`JournalExtractionService.ExtractAsync`)

### New Step: fetch projects (before active-tasks fetch)

```
projects = await _todoist.GetProjectsAsync(ct)
```

On failure: log warning; set `projects = null`. Extraction continues — all new tasks will
go to Inbox this request.

### Updated: BuildPrompt call

Pass `projects` as the third argument.

### Updated: task creation (Step 5a)

**Routing guard — run before creating any task:**

1. If `RoutedTasks` is null or empty → use fallback path.
2. If `RoutedTasks.Length != NewTasks.Length` → log warning, use fallback path.
3. Otherwise → use routed path.

**Routed path:**

Build a project-name lookup dictionary (OrdinalIgnoreCase):

```
foreach project in projects:
    if name already in dict → log warning ("duplicate project name: {name}, first match wins")
    else → dict[project.Name] = project.Id
```

For each `RoutedTask` at index `i`:
- If `RoutedTask.Title != NewTasks[i]` (case-sensitive) → log warning. Do not fail; continue
  using `RoutedTask.Title` for Todoist creation (prompt requires equality but the validator
  does not enforce it).
- Look up `RoutedTask.Project` in the dictionary.
- If found → `projectId = dict[name]`
- If not found (unknown name or null) → `projectId = null` (Inbox); log warning if name was
  non-null.
- Call `CreateTaskAsync(new CreateTaskRequest(RoutedTask.Title, projectId, Priority: 1), ct)`.

**Fallback path (unchanged behaviour):**

For each title in `NewTasks`:
- Call `CreateTaskAsync(new CreateTaskRequest(title, null, Priority: 1), ct)`.

### `JournalExtractionResponse` mapping

Unchanged. `response.NewTasks` is always populated from `result.NewTasks` (string titles).

---

## Error handling summary

| Scenario | Behaviour |
|---|---|
| `GetProjectsAsync` throws | Log warning; `projects = null`; all new tasks → Inbox |
| AI omits `routed_tasks` | `RoutedTasks == null`; fallback path |
| `RoutedTasks.Length != NewTasks.Length` | Log warning; ignore `RoutedTasks`; fallback path |
| `RoutedTasks[i].Title != NewTasks[i]` | Log warning; use `RoutedTask.Title`; do not fail |
| `RoutedTasks[i].Project` not in project list | Log warning; `projectId = null`; task → Inbox |
| Duplicate project names (case-insensitive) | Log warning; first match wins |
| Validator rejects `RoutedTasks` item | Extraction returns `"invalid_response"` (same as today) |

---

## Tests

### `ExtractionValidatorTests`

| Test | Expected |
|---|---|
| `RoutedTasks` absent | passes |
| `RoutedTasks` present, all valid | passes |
| `RoutedTasks[i].Title` over 500 chars | fails |
| `RoutedTasks[i].Project` over 200 chars | fails |
| `RoutedTasks[i].Project` is null | passes |

### `JournalExtractionServiceTests`

| Test | Expected |
|---|---|
| Projects fetched; AI returns matching project name | `CreateTaskAsync` called with correct `projectId` |
| AI returns unknown project name | `CreateTaskAsync` called with `null` |
| AI omits `routed_tasks` | fallback path; `CreateTaskAsync` called with `null` |
| `RoutedTasks.Length != NewTasks.Length` | fallback path; `CreateTaskAsync` called with `null` |
| `GetProjectsAsync` throws | extraction completes; all tasks → Inbox |
| Duplicate project names in Todoist response | first match used; warning logged |

---

## Files changed

| File | Change |
|---|---|
| `Services/Ai/ExtractedNewTask.cs` | **new** |
| `Services/Ai/ExtractionResult.cs` | add `RoutedTasks` field |
| `Services/Ai/ExtractionValidator.cs` | add optional `RoutedTasks` validation |
| `Services/Ai/JournalExtractionService.cs` | fetch projects, update prompt, update task creation |
| `Tests/...ExtractionValidatorTests.cs` | add `RoutedTasks` test cases |
| `Tests/...JournalExtractionServiceTests.cs` | add routing test cases |

No controller, DTO, migration, or frontend changes.
