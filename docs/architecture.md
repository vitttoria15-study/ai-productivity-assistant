# Architecture

## High-Level Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                      React Frontend                          │
│               (Vite + React, localhost:5173)                 │
│   ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│   │  Journal UI  │  │  Task List   │  │   Chat Panel     │  │
│   └──────────────┘  └──────────────┘  └──────────────────┘  │
└────────────────────────────┬─────────────────────────────────┘
                             │ REST (HTTP/JSON)
┌────────────────────────────▼─────────────────────────────────┐
│                  ASP.NET Core Web API                        │
│                (C#, .NET 9, localhost:5000)                  │
│   ┌─────────────────┐ ┌──────────────┐ ┌──────────────────┐ │
│   │JournalController│ │TaskController│ │  ChatController  │ │
│   └─────────────────┘ └──────────────┘ └──────────────────┘ │
│   ┌──────────────────────────────────────────────────────┐   │
│   │                   DialService                        │   │
│   │  (HttpClient → EPAM Dial API, OpenAI-compatible)    │   │
│   └──────────────────────────────────────────────────────┘   │
│   ┌──────────────────────────────────────────────────────┐   │
│   │              EF Core + SQLite                        │   │
│   │                (productivity.db)                     │   │
│   └──────────────────────────────────────────────────────┘   │
└─────────────────────┬────────────────────┬───────────────────┘
                      │                    │
          ┌───────────▼──────────┐ ┌───────▼───────────┐
          │   EPAM Dial API      │ │   SQLite DB        │
          │  (LLM gateway)       │ │  (productivity.db) │
          └──────────────────────┘ └───────────────────┘

──────────────────────────────────────────────────────────────
n8n (localhost:5678) — runs independently, not in the main path
  Cron Trigger (09:00 daily)
    → HTTP GET /api/journal/has-entry-today
    → IF has_entry === false → Notification / log
    → IF has_entry === true  → Log "no reminder needed"
──────────────────────────────────────────────────────────────
```

---

## Data Flow

### Journal Extraction Flow

```
1.  User types journal text in React textarea → clicks "Analyze"
2.  React POST /api/journal  { entry_text: "..." }
3.  Backend saves entry to SQLite (journal_entries)
4.  DialService builds system prompt (extraction instructions + schema)
5.  DialService sends { system_prompt, user_message } to EPAM Dial API
6.  EPAM Dial returns JSON: { completed_tasks, blockers, new_tasks, summary }
7.  Backend validates JSON structure (422 if invalid)
8.  Backend saves extraction to SQLite (ai_extractions)
9.  Backend saves each new_task to SQLite (tasks, source="ai")
10. Backend returns 201 with full extraction result
11. React renders extraction panel; task list auto-refreshes
```

### Chat Flow

```
1. User types message in chat panel → clicks Send
2. React POST /api/chat  { message: "..." }
3. Backend fetches: all tasks (GET from DB) + latest journal entry (GET from DB)
4. DialService builds system prompt injecting tasks + journal context
5. DialService sends to EPAM Dial API
6. EPAM Dial returns text response
7. Backend returns 200  { reply: "..." }
8. React appends user message + AI reply to in-memory chat history
```

### n8n Reminder Flow

```
1. n8n cron trigger fires at 09:00 AM daily
2. HTTP GET → http://localhost:5000/api/journal/has-entry-today
3. n8n IF node: response.has_entry === false?
   ├── TRUE:  HTTP POST notification  (webhook / Slack / execution log)
   │          → log: "Reminder sent at {timestamp}"
   └── FALSE: log: "Entry already written — no reminder needed"
```

---

## Backend Responsibilities

- Receive and persist journal entries (`JournalController`)
- Orchestrate AI extraction via `DialService` on journal submission
- Provide CRUD endpoints for task management (`TaskController`)
- Receive chat messages, build context-aware prompts, delegate to `DialService` (`ChatController`)
- Expose `GET /api/journal/has-entry-today` for n8n integration
- Validate AI JSON responses; surface errors as appropriate HTTP codes
- Manage all database access via EF Core repositories

### Key backend components

| Component | Responsibility |
|-----------|----------------|
| `JournalController` | Handles journal CRUD + triggers extraction |
| `TaskController` | Handles task CRUD + status toggle |
| `ChatController` | Builds context prompt + delegates to DialService |
| `DialService` | Single HTTP client wrapper for all EPAM Dial API calls |
| `AppDbContext` | EF Core DbContext for SQLite |

---

## Frontend Responsibilities

- Journal entry textarea with submit button and loading state
- Extraction result panel: completed tasks, blockers, new tasks (with priority), summary
- Task list with add/edit/delete/mark-done interactions
- Chat panel: message input, conversation history (in-memory)
- Journal history list (past entries)
- API client (`fetch` or `axios`) — no Redux or complex state management needed for MVP

---

## AI Integration (EPAM Dial API)

EPAM Dial exposes an **OpenAI-compatible API**. All requests use the same message format as `POST /v1/chat/completions`.

### Extraction system prompt

```
You are a productivity assistant. Extract structured information from the user's journal entry.
Return ONLY a valid JSON object — no explanation, no markdown, no code fences.
Use exactly this schema:
{
  "completed_tasks": ["string"],
  "blockers": ["string"],
  "new_tasks": [{"title": "string", "priority": "high|medium|low"}],
  "summary": "string"
}
If a category is empty, return an empty array []. The summary must be 1–3 sentences.
```

### Chat system prompt

```
You are a productivity assistant helping a developer manage their work.

Current tasks:
{task_list_formatted}

Latest journal entry ({date}):
{entry_text}

Answer the user's question based on this context. Be concise and practical.
If context is not available, say so and offer general guidance.
```

### Dial API configuration

| Parameter | Value |
|-----------|-------|
| Base URL | Configured via `appsettings.json` (`DialApi:BaseUrl`) |
| API Key | Configured via `appsettings.json` (`DialApi:ApiKey`) — do not hardcode |
| Model | Configured via `appsettings.json` (`DialApi:Model`) |
| Temperature (extraction) | 0.1 |
| Temperature (chat) | 0.7 |
| Max tokens | 1000 (extraction), 500 (chat) |

---

## n8n Workflow Role

n8n is a **separate automation layer** — it does not run in the main application path and has no dependency on the React frontend. Its role in this PoC is to demonstrate a low-code workflow primitive:

- Trigger type: Schedule (cron)
- External integration: HTTP request to the backend
- Conditional logic: IF node on `has_entry` value
- Output: notification or log entry

For the demo, the "notification" can be an n8n execution log, HTTP POST to a webhook.site URL, or a Slack message — whatever is simplest to demonstrate visually.

---

## Future RAG Extension

The architecture deliberately leaves room for lightweight RAG without requiring breaking changes:

1. Add vector storage: SQLite + `sqlite-vss` extension, or an embedded store (Chroma, FAISS)
2. On journal save: call EPAM Dial embeddings endpoint → store `(entry_id, text_chunk, embedding_vector)`
3. On chat/extraction: query vector store for top-k semantically similar past entries → inject as additional context in the system prompt
4. No changes needed to: REST API contracts, React frontend, or EF Core schema (journal + tasks tables unchanged)
5. Only `DialService` and storage layer expand

This keeps the RAG extension fully additive and non-breaking.
