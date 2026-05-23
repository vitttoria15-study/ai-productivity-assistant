# Design: Todoist as Primary Source of Truth — Documentation Update

**Date:** 2026-05-23
**Branch:** docs/todoist-concept-sync
**Scope:** Documentation only. No backend, frontend, API, model, or configuration changes.

---

## Problem

Current project documentation describes SQLite as the store for tasks, projects, and all productivity data. This contradicts the intended architecture where Todoist owns task and project data, and SQLite stores only app-specific metadata produced by the AI layer.

Every doc that mentions task persistence, task CRUD, or the data model sends the wrong signal to anyone implementing or extending the system.

---

## Goal

Update all affected documentation so that every doc tells a consistent story:

- **Todoist** is the primary source of truth for tasks and projects.
- **SQLite** stores only app-specific metadata: journal entries, AI summaries, extracted blockers, automation logs, user preferences, and Todoist entity references.
- The app adds an AI layer on top of Todoist: extraction pushes tasks to Todoist; chat and summaries read tasks from Todoist for context.

---

## Data Ownership Split

| Concern | Owner |
|---|---|
| Tasks, projects, priorities, task status | Todoist API |
| Journal entries | SQLite |
| AI-generated summaries | SQLite |
| Extracted blockers and AI insights | SQLite |
| Automation event logs | SQLite |
| User preferences | SQLite |
| References from journal entries to Todoist task IDs | SQLite (metadata only) |

---

## Architecture

The updated architecture shows Todoist API as a second external service alongside EPAM Dial API:

```
React Frontend
      ↓ REST
ASP.NET Core Web API
    ↓               ↓               ↓
EPAM Dial API   Todoist API     SQLite DB
(LLM Gateway)  (Tasks/Projects) (App metadata)
```

---

## SQLite Data Model (after update)

SQLite retains only these entities:

| Entity | Key Fields | Purpose |
|---|---|---|
| `JournalEntry` | Id, Content, CreatedAt, Summary | User journal text and AI-generated summary |
| `ExtractedBlocker` | Id, JournalEntryId, Description, CreatedAt | Blockers identified by AI from journal entries |
| `AutomationLog` | Id, WorkflowName, EventType, Message, CreatedAt | n8n workflow execution events |

`TaskItem` is removed from the SQLite schema. Tasks live in Todoist. The app may store a Todoist task ID alongside a journal entry as a reference, but it does not replicate full task records locally.

---

## Data Flows (updated)

### Journal Extraction Flow

```
User submits journal entry
    ↓
React sends POST /api/journal
    ↓
Backend builds extraction prompt
    ↓
Backend sends request to EPAM Dial API
    ↓
Dial API returns structured JSON
    ↓
Backend saves to SQLite:
    - journal entry
    - AI summary
    - extracted blockers
Backend pushes to Todoist API:
    - new_tasks → created as Todoist tasks
    - completed_tasks → matched Todoist tasks marked done
    ↓
Backend returns response to frontend
    ↓
Frontend displays extraction results
```

### Context-Aware Chat Flow

```
User sends chat message
    ↓
React sends POST /api/chat
    ↓
Backend loads from Todoist API:
    - current tasks and projects
Backend loads from SQLite:
    - recent journal entries
    - active blockers
    ↓
Backend injects context into system prompt
    ↓
Backend sends request to EPAM Dial API
    ↓
AI response returned
    ↓
Frontend displays contextual response
```

### Task Management Flow

```
User views or edits a task
    ↓
Frontend sends request to /api/tasks
    ↓
Backend proxies request to Todoist API
    ↓
Todoist returns task data
    ↓
Frontend displays tasks
```

All task CRUD (create, read, update, delete) is routed through the Todoist API. SQLite is not involved in task operations.

---

## Requirements Changes

- **FR-2 AI Extraction**: Extracted `new_tasks` are created in Todoist. Extracted `completed_tasks` mark matching Todoist tasks as done. Blockers and summaries are saved to SQLite.
- **FR-3 Task Management**: Task CRUD routes through the Todoist API. The backend acts as a proxy, optionally enriching requests with AI context. SQLite is not the task store.
- **NFR-4 SQLite Compatibility**: SQLite compatibility applies to app-metadata tables only (journal entries, blockers, logs). Task queries target Todoist.
- **Technical Requirements → Database**: SQLite schema covers journal, blocker, and automation tables only.

---

## Affected Documents

| File | Change summary |
|---|---|
| `README.md` | Tech stack table, architecture diagram, database section, data flow, planned steps |
| `docs/architecture.md` | Architecture diagram, Database section, data flows, backend components table |
| `docs/data-model.md` | Remove TaskItem; define SQLite-only entities; add Todoist ownership note |
| `docs/concept.md` | Architecture diagram, Task Management section |
| `docs/requirements.md` | FR-2, FR-3, NFR-4, Technical Requirements → Database |
| `docs/api-spec.md` | `/api/tasks` description: proxies Todoist, does not read local DB |
| `docs/roadmap.md` | Week 2 task management note; MVP priorities table |
| `docs/demo-plan.md` | Demo prep checklist; demo 2 task management description |
| `docs/frontend-spec.md` | Task List source note |
| `docs/ai-extraction-spec.md` | Add destination note for extracted fields |
| `docs/user-flows.md` | Flow 1 success criteria, Flow 2 source, Flow 3 backend target, Flow 4 context source |

---

## Out of Scope

- No backend code, controllers, services, or models.
- No frontend code or components.
- No database migrations or schema files.
- No configuration or environment variable changes.
- No API implementation changes.

The documentation update must not imply or require any code to exist that does not already exist. Descriptions should reflect intent and planned architecture, consistent with the PoC's existing "planned" framing.
