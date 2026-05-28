# Spec: AI Provider Abstraction

**Slug:** `ai-provider-abstraction`
**Date:** 2026-05-27
**Branch:** `feature/ai-provider-abstraction`

---

## Goal

Replace the hardcoded `MockExtractionResponse` in `JournalController` with a real AI extraction
pipeline. The pipeline must support three providers (EPAM DIAL, Ollama, Mock), be selectable via
configuration, validate AI output before touching Todoist, and degrade gracefully when the
provider is unavailable.

---

## Context

- `JournalController.Create` currently uses an inline `MockExtractionResponse`. This is the seam
  to replace.
- `ITodoistService` already exposes `CreateTaskAsync` and `CloseTaskAsync`. The AI layer must
  never call these directly — only `JournalExtractionService` does.
- SQLite stores only app metadata. Todoist is the source of truth for tasks and projects.
- `ExtractedBlocker` is documented in `data-model.md` and `ai-extraction-spec.md` but is missing
  from `AppDbContext`. This task adds it.
- `TaskItem` in SQLite conflicts with the "no task records in SQLite" architecture rule. It is
  left untouched; cleanup is a separate ticket.

---

## Architecture Overview

```
POST /api/journal
        ↓
JournalController
        ↓
JournalExtractionService
    ├── 1. Save JournalEntry (Content, CreatedAt) to SQLite — always first
    ├── 2. Fetch active Todoist task titles (for prompt context)
    │       └── On failure → soft failure: log warning, disable auto-close,
    │           omit task list from prompt, continue to step 3
    ├── 3. Call IAiProvider.CompleteAsync(systemPrompt, userMessage)
    │       └── On failure → extraction_status: "failed", skip steps 4–7
    ├── 4. Parse + validate ExtractionResult (hard and soft rules)
    ├── 5. Route to ITodoistService — create new tasks, close matched tasks
    ├── 6. Save ExtractedBlockers to SQLite
    └── 7. Update JournalEntry.Summary in SQLite (second SaveChanges)
        ↓
Response: JournalExtractionResponse
```

Step 2 failure and step 3 failure have different outcomes:

- **Step 2 fails** (active task fetch): soft failure — extraction continues without task context.
  See "Active Task Fetch Failure" for full behaviour.
- **Step 3 fails** (provider call): `extraction_status: "failed"` is returned. The journal row
  committed in step 1 is preserved. Steps 4–7 are skipped. `JournalEntry.Summary` remains `""`.

---

## IAiProvider Interface

```csharp
// backend/Services/Ai/IAiProvider.cs
namespace backend.Services.Ai;

public interface IAiProvider
{
    Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct = default);
}
```

Three implementations:

| Class | Path | Description |
|---|---|---|
| `DialAiProvider` | `Services/Ai/Providers/DialAiProvider.cs` | EPAM DIAL via OpenAI chat completions format |
| `OllamaProvider` | `Services/Ai/Providers/OllamaProvider.cs` | Local Ollama `/api/chat` endpoint |
| `MockAiProvider` | `Services/Ai/Providers/MockAiProvider.cs` | Returns the existing hardcoded mock JSON |

DI selects the concrete implementation at startup based on `"Ai:Provider"` config value:
`"dial"` / `"ollama"` / `"mock"`. An unrecognised value throws at startup with a message listing
valid options. `MockAiProvider` is never used as an automatic runtime fallback — it is only
active when explicitly configured.

---

## Configuration and Secret Handling

### Non-secret settings (safe to commit)

```json
// appsettings.json
"Ai": {
  "Provider": "mock",
  "Dial": {
    "Endpoint": "https://your-dial-endpoint/openai/deployments/gpt-4o/chat/completions",
    "Model": "gpt-4o"
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "llama3"
  }
}
```

### Secret handling

`Ai:Dial:ApiKey` is loaded exclusively from .NET user-secrets or the environment variable
`Ai__Dial__ApiKey`. It must **never** appear in `appsettings.json` or
`appsettings.Development.json`.

Setup: `dotnet user-secrets set "Ai:Dial:ApiKey" "sk-..."`

Startup behaviour when `Provider=dial` and `ApiKey` is empty: log a warning (same pattern as
the existing Todoist token warning) and continue. The first extraction attempt will fail with
`extraction_status: "failed"`.

### AiOptions class

```csharp
// backend/Services/Ai/AiOptions.cs
public class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "mock";

    public DialOptions Dial   { get; set; } = new();
    public OllamaOptions Ollama { get; set; } = new();

    public class DialOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Model    { get; set; } = "gpt-4o";
        public string ApiKey   { get; set; } = string.Empty;  // populated from user-secrets or Ai__Dial__ApiKey env var; never from appsettings
    }

    public class OllamaOptions
    {
        public string Endpoint { get; set; } = "http://localhost:11434";
        public string Model    { get; set; } = "llama3";
    }
}
```

---

## Prompt Template

The system prompt is static. The user message is assembled by `JournalExtractionService` before
each provider call — the provider receives fully-formed strings and knows nothing about the
domain.

### System prompt (static)

```
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
- completed_tasks: titles of tasks the user says they finished. Match exactly from the provided
  Active tasks list. If no active tasks list is provided, return [].
- new_tasks: tasks the user mentions planning to do that are NOT in the active tasks list.
- blockers: obstacles, blockers, or dependencies the user is waiting on.
- priorities: anything the user flags as important or urgent.
- summary: 1–2 sentences describing the day's work.
- Return empty arrays if nothing is found. Never omit a field.
```

### User message (dynamic)

```
Active tasks:
- Fix frontend validation
- Prepare demo

Journal entry:
{journal_text}
```

The "Active tasks:" section is omitted entirely when the Todoist task fetch fails (see
"Active Task Fetch Failure" below).

---

## ExtractionResult JSON Schema

Matches `docs/ai-extraction-spec.md` exactly:

```json
{
  "completed_tasks": ["string"],
  "new_tasks":       ["string"],
  "blockers":        ["string"],
  "priorities":      ["string"],
  "summary":         "string"
}
```

---

## Validation: Hard vs Soft Failures

### Hard failures → `extraction_status: "invalid_response"`

Stops all further processing. No Todoist mutations. No SQLite updates for blockers or summary.
Journal entry (step 1) remains saved.

| Rule | Condition |
|---|---|
| JSON parse | Response is not valid JSON |
| Required fields | Any of the five fields is missing |
| Type check | `completed_tasks`, `new_tasks`, `blockers`, `priorities` are not arrays of strings |
| String length | Any string element exceeds 500 characters |
| Summary length | `summary` exceeds 1000 characters |

### Soft failures → warning logged, processing continues

| Rule | Behaviour |
|---|---|
| `completed_tasks` item not found in active task list | Log warning, skip that item only |
| `CloseTaskAsync` call fails for a matched task | Log warning, skip that task, continue with remaining |
| `CreateTaskAsync` call fails for a new task | Log warning, skip that task, continue with remaining |
| Active task fetch fails (step 2) | Log warning; auto-close disabled; `new_tasks` creation proceeds normally |

Soft failures do not change `extraction_status`. The response still returns `"ok"` with whatever
was successfully processed.

---

## Active Task Fetch Failure

When the Todoist task fetch in step 2 fails (network error, 4xx/5xx response):

- Log a warning: `"Failed to fetch active Todoist tasks for prompt context. Auto-close disabled for this extraction."`
- Omit the "Active tasks:" section from the user message.
- Continue with extraction. The "Active tasks:" section is omitted from the prompt.
- Regardless of what the AI returns in `completed_tasks`, **all completed-task mutations are
  skipped** — the close dictionary is not built, and no `CloseTaskAsync` calls are made.
- `new_tasks` creation proceeds normally.
- `extraction_status` is not affected by this condition alone.

---

## Todoist Routing

### New tasks (`new_tasks[]`)

For each item: call `ITodoistService.CreateTaskAsync(new CreateTaskRequest(title, null, Priority: 1))`.

Priority defaults to 1 for all extracted tasks. The `priorities[]` array from the AI response is
returned in the response body for frontend display but is not mapped to individual task priorities
in this iteration.

A `CreateTaskAsync` failure is a soft failure (logged, skipped, processing continues).

### Completed tasks (`completed_tasks[]`)

The active task list from step 2 is built into a `Dictionary<string, string>` (case-insensitive
title → Todoist task ID).

For each item in `completed_tasks`:
- Look up in the dictionary (case-insensitive).
- If found: call `ITodoistService.CloseTaskAsync(taskId)`. Failure is a soft failure.
- If not found: log warning, skip (soft failure — processing continues with remaining items).

If step 2 failed and no dictionary exists: skip all `completed_tasks` items (auto-close disabled).

---

## SQLite Changes

### ExtractedBlocker model (new)

`ExtractedBlocker` is documented in `data-model.md` and `ai-extraction-spec.md` but is absent
from `AppDbContext`. This task adds it.

```csharp
// backend/Models/ExtractedBlocker.cs
namespace backend.Models;

public class ExtractedBlocker
{
    public int Id             { get; set; }
    public int JournalEntryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;
}
```

Added to `AppDbContext`:

```csharp
public DbSet<ExtractedBlocker> ExtractedBlockers => Set<ExtractedBlocker>();
```

**No explicit migration file is required.** The app calls `db.Database.EnsureCreated()` at
startup (already in `Program.cs`), which creates the table on the next run. This is the only
schema change — a new table is added; no existing columns are altered.

> **PoC note:** `EnsureCreated` is acceptable here because the project is a single-developer
> proof of concept with a local SQLite file. It is not a long-term migration strategy — a
> production deployment would use `dotnet ef migrations` to manage schema changes safely and
> incrementally.

### JournalEntry persistence timing

1. **Step 1 (first `SaveChangesAsync`):** Save `JournalEntry` with `Content` and `CreatedAt`.
   `Summary` is left as the default empty string at this point. This commit is unconditional.

2. **Step 7 (second `SaveChangesAsync`):** After a successful extraction, update
   `JournalEntry.Summary` with the AI-generated summary string and save `ExtractedBlocker`
   records for the same `SaveChanges` call.

If extraction fails (steps 3–6), step 7 is skipped. `JournalEntry.Summary` remains `""`.

---

## Response Shape

```csharp
// backend/Controllers/JournalController.cs (response record)
public sealed record JournalExtractionResponse(
    string   ExtractionStatus,   // "ok" | "failed" | "invalid_response"
    string?  ExtractionError,    // null on "ok"; user-safe message otherwise
    string   Provider,           // "dial" | "ollama" | "mock"
    string[] CompletedTasks,
    string[] NewTasks,
    string[] Blockers,
    string[] Priorities,
    string   Summary
);
```

Example on success:

```json
{
  "extraction_status": "ok",
  "extraction_error": null,
  "provider": "dial",
  "completed_tasks": ["Fix frontend validation"],
  "new_tasks": ["Write unit tests"],
  "blockers": ["Missing environment variables"],
  "priorities": ["Prepare demo"],
  "summary": "User completed frontend validation fix and identified a missing env var blocker."
}
```

Example on provider failure:

```json
{
  "extraction_status": "failed",
  "extraction_error": "AI extraction is temporarily unavailable. Your journal entry was saved.",
  "provider": "dial",
  "completed_tasks": [],
  "new_tasks": [],
  "blockers": [],
  "priorities": [],
  "summary": ""
}
```

HTTP status is always `200 OK` (journal was saved regardless of extraction outcome).

---

## Minimal UI Changes

`JournalCard.jsx` already renders the extraction response. Two targeted additions:

1. **Degraded-state banner**: if `extraction_status !== "ok"`, render
   `"Journal saved, but AI extraction is temporarily unavailable."` in place of the results.

2. **Summary display**: add a summary section that renders `response.summary` when
   `extraction_status === "ok"` and `summary` is non-empty.

3. **Provider badge** (optional): small indicator showing which provider responded (useful during
   demo/testing). Can be hidden in production config.

No new React components. No new npm dependencies.

---

## Daily Summary MVP Scope

For this task, "daily summary" means:

- `JournalEntry.Summary` in SQLite stores the AI-generated `summary` string.
- The summary is returned in the `POST /api/journal` response and displayed in the frontend.
- No separate `/api/summary` endpoint in this iteration.
- No aggregation across multiple journal entries.

Cross-day and weekly/monthly summaries are explicitly future scope.

---

## New Files

| Path | Purpose |
|---|---|
| `backend/Services/Ai/IAiProvider.cs` | Provider interface |
| `backend/Services/Ai/AiOptions.cs` | Strongly-typed config |
| `backend/Services/Ai/Providers/DialAiProvider.cs` | EPAM DIAL implementation |
| `backend/Services/Ai/Providers/OllamaProvider.cs` | Ollama implementation |
| `backend/Services/Ai/Providers/MockAiProvider.cs` | Mock implementation |
| `backend/Services/Ai/JournalExtractionService.cs` | Orchestrator |
| `backend/Models/ExtractedBlocker.cs` | New SQLite model |

### Modified files

| Path | Change |
|---|---|
| `backend/Data/AppDbContext.cs` | Add `DbSet<ExtractedBlocker>` |
| `backend/Controllers/JournalController.cs` | Inject `JournalExtractionService`, remove inline mock |
| `backend/Program.cs` | Register `AiOptions`, DI-select `IAiProvider` implementation |
| `backend/appsettings.json` | Add `"Ai"` config section (non-secret fields only) |
| `frontend/src/components/JournalCard.jsx` | Degraded-state banner + summary display |

---

## Out of Scope

| Item | Reason |
|---|---|
| OAuth / authentication | Not in scope for PoC |
| Autonomous extraction loop | Future iteration |
| RAG / vector DB | Future iteration |
| Voice-to-text input | Week 3 per roadmap |
| Weekly/monthly cross-day summaries | Future scope |
| `TaskItem` SQLite model cleanup | Separate cleanup ticket |
| Per-task priority mapping from `priorities[]` | UI display only in this iteration |
| Retry UI for failed extractions | Future UX improvement |
| Streaming AI responses | Not needed for single extraction call |
| n8n trigger for AI extraction | Separate automation workflow |

---

## Acceptance Criteria

1. `POST /api/journal` with `provider=dial` calls the EPAM DIAL endpoint and returns real
   extraction results.
2. `POST /api/journal` with `provider=ollama` calls the local Ollama endpoint and returns real
   extraction results.
3. `POST /api/journal` with `provider=mock` returns the existing mock response unchanged.
4. If the configured provider is unavailable, `extraction_status: "failed"` is returned. The
   journal entry is saved to SQLite. No Todoist mutations occur.
5. `new_tasks` items are created in Todoist via `CreateTaskAsync`.
6. `completed_tasks` items that exactly match an active Todoist task title are closed via
   `CloseTaskAsync`.
7. `blockers[]` items are saved as `ExtractedBlocker` rows in SQLite linked to the journal entry.
8. `JournalEntry.Summary` is populated after successful extraction.
9. `Ai:Dial:ApiKey` is never present in `appsettings.json` or `appsettings.Development.json`.
10. Frontend shows the degraded-state banner when `extraction_status !== "ok"`.
11. Frontend shows the AI-generated summary when `extraction_status === "ok"`.
12. `dotnet build` completes with zero errors and zero warnings. `npm run dev` starts without
    errors. No new packages are added to `package.json` or `backend.csproj` beyond what the
    implementation plan identifies as necessary for HTTP client wiring.
