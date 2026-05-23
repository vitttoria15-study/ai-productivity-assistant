# Requirements

## Overview

This document describes the functional and non-functional requirements for the AI Productivity Assistant PoC.

The project is designed as a lightweight AI-powered productivity assistant focused on:

* natural-language productivity workflows,
* AI-assisted task extraction,
* contextual productivity support,
* simple task management.

The project intentionally prioritizes:

* realistic MVP scope,
* demo-ready functionality,
* simplicity,
* incremental delivery.

The project intentionally avoids:

* enterprise complexity,
* large-scale infrastructure,
* multi-user collaboration,
* advanced orchestration systems.

---

# Product Goals

The system should help users:

* organize tasks and priorities,
* reduce mental overload,
* track blockers and progress,
* convert free-form notes into structured information,
* receive contextual AI productivity assistance.

---

# MVP Scope

The MVP focuses on:

* journal-driven productivity workflows,
* AI extraction of structured information,
* lightweight task management,
* contextual AI chat,
* productivity summaries.

---

# Functional Requirements

## FR-1 Journal Entry Submission

The system shall allow users to submit free-form journal or progress entries.

### Input

The user provides natural-language text.

Example:

```text
Today I completed onboarding, but backend authentication is still blocked.
Tomorrow I need to finish the project presentation.
```

### Expected Behavior

The backend sends the journal entry to EPAM Dial API for analysis.

---

## FR-2 AI Extraction

The system shall extract structured information from journal entries.

The extraction should include:

* completed tasks,
* new tasks,
* blockers,
* priorities,
* summary.

### Output Destinations

After extraction:

* `new_tasks` are created in Todoist via the Todoist API.
* `completed_tasks` mark matching Todoist tasks as done.
* `blockers` and `summary` are saved to SQLite.

### Expected AI Response Shape

```json
{
  "completed_tasks": ["string"],
  "new_tasks": [
    {
      "title": "string",
      "priority": "low | medium | high"
    }
  ],
  "blockers": ["string"],
  "priorities": ["string"],
  "summary": "string"
}
```

---

## FR-3 Task Management

The system shall support basic task CRUD by proxying requests to the Todoist API.

Tasks are not stored in SQLite. All task create, read, update, and delete operations are handled by Todoist. The backend may enrich task creation with AI-extracted metadata (title, priority) from the extraction flow.

Users shall be able to:

* view tasks,
* manually create tasks,
* edit tasks,
* delete tasks,
* mark tasks as completed.

### Task Properties

Tasks should support:

* title,
* optional description,
* priority,
* status.

### Supported Priority Values

* low
* medium
* high

### Supported Status Values

* todo
* in_progress
* blocked
* done

---

## FR-4 Context-Aware AI Chat

The system shall support lightweight contextual AI interactions.

The backend should inject:

* current tasks,
* recent journal entries,
* blockers,
* priorities

into the AI system prompt before sending requests to EPAM Dial API.

### Example User Questions

```text
What should I focus on tomorrow?
```

```text
What blockers are currently active?
```

```text
Summarize my progress this week.
```

---

## FR-5 Journal History

The system shall allow users to review previous journal entries.

The journal history should support:

* chronological ordering,
* progress review,
* blocker tracking.

---

## FR-6 Productivity Summaries

The system shall support AI-generated summaries.

Possible summaries include:

* daily summaries,
* weekly summaries,
* blocker overviews,
* accomplishment summaries.

---

## FR-7 n8n Reminder Workflow

The project shall include a separate n8n workflow for Week 1 requirements.

### Planned Workflow

```text
Schedule Trigger
→ GET /api/journal/has-entry-today
→ IF hasEntryToday == false
→ Reminder Notification / Execution Log
```

### Purpose

The workflow demonstrates:

* low-code/no-code automation,
* triggers,
* conditional logic,
* integrations.

The n8n workflow shall remain separate from the main application runtime path.

---

# Non-Functional Requirements

## NFR-1 Simplicity

The architecture should remain lightweight and understandable for a 3-week PoC project.

---

## NFR-2 Demo Readiness

The system should prioritize:

* stable flows,
* visible AI functionality,
* predictable demos,
* fast iteration.

---

## NFR-3 Single-User Design

The MVP is designed as a single-user productivity assistant.

The system should not implement:

* authentication,
* multi-user collaboration,
* role management,
* tenancy.

---

## NFR-4 SQLite Compatibility

The backend should remain compatible with SQLite for app-metadata persistence.

SQLite scope is limited to:

* journal entries,
* AI summaries,
* extracted blockers,
* automation logs,
* user preferences.

Task and project queries target the Todoist API, not SQLite. The system should avoid database-specific enterprise features and complex distributed persistence.

---

## NFR-5 AI Response Handling

The system should gracefully handle:

* malformed AI responses,
* API failures,
* empty AI outputs,
* network issues.

The application should not crash because of invalid AI responses.

---

## NFR-6 Lightweight Frontend

The frontend should remain simple.

The MVP should avoid:

* heavy state management,
* enterprise frontend architecture,
* unnecessary complexity.

---

# AI-Specific Requirements

## AIR-1 Structured JSON Output

The AI extraction workflow should request strict structured JSON output.

The backend should validate AI responses before persistence.

---

## AIR-2 Lightweight Context Injection

The contextual chat system should use lightweight prompt context injection instead of full RAG.

The MVP should NOT implement:

* vector databases,
* embeddings,
* semantic retrieval,
* autonomous memory systems.

---

## AIR-3 Configurable AI Models

The backend should allow configurable model selection through EPAM Dial API.

Possible models may include:

* GPT,
* Claude,
* Gemini,
* other models exposed via Dial.

---

## AIR-4 AI Temperature Configuration

Different AI flows may use different model parameters.

Suggested defaults:

| Flow       | Suggested Temperature |
| ---------- | --------------------- |
| Extraction | Low                   |
| Chat       | Medium                |
| Summaries  | Medium                |

---

# Technical Requirements

## Backend

* ASP.NET Core Web API
* REST API architecture
* SQLite persistence for app metadata
* EF Core ORM
* HTTP client integration with EPAM Dial API
* HTTP client integration with Todoist API (task and project management)

---

## Frontend

* React
* Vite
* Lightweight component structure
* Basic responsive layout

---

## Database

* SQLite local database for app-specific metadata (journal entries, summaries, blockers, logs)
* Simple schema scoped to metadata tables
* Single-user data model
* Tasks and projects owned by Todoist — no task tables in SQLite

---

## Automation

* n8n local or cloud workflow
* Scheduled reminder workflow

---

# Out of Scope

The MVP intentionally excludes:

* authentication,
* multi-user collaboration,
* advanced Kanban systems,
* autonomous AI agents,
* enterprise workflow orchestration,
* production infrastructure,
* microservices,
* advanced analytics pipelines,
* full Retrieval-Augmented Generation (RAG).

---

# Future Enhancements

Possible future improvements:

* lightweight RAG,
* semantic search,
* voice input,
* gamification,
* productivity analytics,
* recurring blocker detection,
* long-term trend summaries.

These features are optional and should only be implemented after the MVP is stable.

---

# Acceptance Criteria

## Week 1

The system should demonstrate:

* project structure,
* backend initialization,
* React frontend,
* AI extraction workflow,
* SQLite integration,
* basic UI,
* n8n reminder workflow.

---

## Final Demo

The final PoC should demonstrate:

* end-to-end AI extraction,
* task management,
* contextual AI chat,
* journal history,
* productivity summaries,
* stable demo flows,
* clean architecture,
* realistic MVP scope.
