# Architecture

## High-Level Architecture

```text
+--------------------------------------------------+
|                  React Frontend                  |
|              (Vite + React, localhost:5173)      |
|                                                  |
|  [ Journal UI ]  [ Task List ]  [ Chat Panel ]  |
+--------------------------------------------------+
                      |
                      | REST (HTTP/JSON)
                      v
+--------------------------------------------------+
|            ASP.NET Core Web API Backend          |
|                                                  |
|  Controllers:                                    |
|   - JournalController                            |
|   - TaskController                               |
|   - ChatController                               |
|                                                  |
|  Services:                                       |
|   - DialService                                  |
|   - JournalService                               |
|   - TaskService                                  |
|                                                  |
|  Persistence:                                    |
|   - EF Core                                      |
|   - SQLite                                       |
+--------------------------------------------------+
           |                |                    |
           |                |                    |
           v                v                    v
+------------------+ +------------------+ +--------------------+
|  EPAM Dial API   | |   Todoist API    | |    SQLite DB       |
|  (LLM Gateway)   | | (Tasks/Projects) | |  (App metadata)    |
+------------------+ +------------------+ +--------------------+

+--------------------------------------------------+
|                     n8n Workflow                 |
|                                                  |
|  Runs independently from the main runtime path   |
|                                                  |
|  Example flow:                                   |
|  Cron Trigger                                    |
|   → GET /api/journal/has-entry-today             |
|   → IF hasEntryToday == false                    |
|   → Reminder Notification / Execution Log        |
+--------------------------------------------------+
```

---

# System Overview

The application is designed as a lightweight AI-powered productivity assistant.

The system allows users to:

* submit natural-language journal entries,
* extract structured tasks using AI,
* manage tasks and priorities,
* interact with a contextual AI assistant,
* review progress and blockers over time.

The architecture intentionally prioritizes:

* simplicity,
* fast iteration,
* demo-readiness,
* realistic MVP scope.

The architecture intentionally avoids:

* enterprise complexity,
* microservices,
* authentication,
* multi-user infrastructure,
* advanced orchestration layers.

---

# Main Components

## Frontend (React)

The React frontend is responsible for:

* journal entry submission,
* task display and CRUD,
* AI chat interactions,
* displaying AI-generated summaries,
* lightweight UI state management.

---

## Backend (ASP.NET Core Web API)

The backend is responsible for:

* REST API endpoints,
* orchestrating AI calls,
* prompt construction,
* SQLite persistence,
* business logic,
* contextual AI interactions.

The backend acts as the single integration layer between:

* frontend,
* EPAM Dial API,
* database,
* optional automation workflows.

---

## Database (SQLite)

SQLite is used as a lightweight local database for app-specific metadata.

The database stores:

* journal entries,
* AI-generated summaries,
* extracted blockers,
* automation logs,
* user preferences.

Tasks and projects are not stored in SQLite. They are owned by Todoist and accessed via the Todoist API.

SQLite was selected because:

* it requires no separate server,
* it is simple to configure,
* it is ideal for a single-user MVP,
* it reduces operational complexity.

---

## AI Integration (EPAM Dial API)

EPAM Dial API is used as the LLM gateway.

The backend sends:

* prompts,
* contextual task data,
* recent journal entries

to the Dial API and receives:

* structured JSON,
* summaries,
* AI chat responses.

The system should always request structured JSON output for extraction flows.

---

## n8n Workflow

n8n is intentionally separated from the main application runtime path.

n8n exists only as:

* a low-code/no-code automation layer,
* a Week 1 deliverable,
* a lightweight reminder/notification workflow.

n8n is NOT responsible for:

* core AI orchestration,
* task extraction,
* application state,
* business logic.

---

# Data Flow

## 1. Journal Extraction Flow

```text
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
    - completed_tasks → matching Todoist tasks marked done
    ↓
Backend returns response to frontend
    ↓
Frontend displays extraction results
```

---

## 2. Context-Aware Chat Flow

```text
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

The MVP uses lightweight context injection instead of full RAG.

---

## 3. n8n Reminder Flow

```text
Cron Trigger (daily)
    ↓
GET /api/journal/has-entry-today
    ↓
IF hasEntryToday == false
    ↓
Send Reminder Notification
    ↓
Log Workflow Execution
```

---

# Backend Responsibilities

The backend is responsible for:

* receiving and validating requests,
* orchestrating AI interactions,
* prompt construction,
* persistence logic,
* task CRUD via Todoist API,
* lightweight contextual memory,
* error handling,
* API contracts.

---

# Key Backend Components

| Component         | Responsibility                       |
| ----------------- | ------------------------------------ |
| JournalController | Journal submission and history       |
| TaskController    | Task CRUD operations                 |
| ChatController    | Context-aware AI chat                |
| DialService       | Communication with EPAM Dial API     |
| JournalService    | Journal orchestration/business logic |
| TaskService       | Todoist API integration and task proxy |
| AppDbContext      | SQLite persistence for app metadata (journal, blockers, logs) |

---

# Frontend Responsibilities

The frontend is responsible for:

* rendering journal input,
* displaying extracted tasks,
* task management interactions,
* AI chat UI,
* loading/error states,
* lightweight local UI state.

The MVP intentionally avoids:

* Redux,
* complex state management,
* advanced frontend architecture.

---

# AI Integration Details

## Extraction Prompt Strategy

The extraction flow should request strict structured JSON.

Example prompt:

```text
You are a productivity assistant.

Extract structured information from the user's journal entry.

Return ONLY valid JSON.

Required fields:
- completed_tasks
- new_tasks
- blockers
- priorities
- summary

Each new task must contain:
- title
- priority

Priority values:
- low
- medium
- high
```

---

## Context-Aware Chat Prompt

Example strategy:

```text
System:
You are a productivity assistant helping a user organize work and priorities.

Current tasks:
{{task_list}}

Recent journal entries:
{{recent_entries}}

Current blockers:
{{blockers}}

Answer concisely and practically.
```

---

# Dial API Configuration

The backend should configure:

| Setting     | Description                                    |
| ----------- | ---------------------------------------------- |
| Base URL    | EPAM Dial API endpoint                         |
| API Key     | Stored in appsettings or environment variables |
| Model       | Configurable model name                        |
| Temperature | Low for extraction, medium for chat            |
| Max Tokens  | Configurable request limits                    |

Sensitive values must NOT be hardcoded.

---

# Error Handling

The system should gracefully handle:

* Dial API failures,
* malformed JSON,
* timeouts,
* invalid requests,
* empty responses.

The UI should display:

* loading states,
* validation errors,
* AI failure messages.

---

# Future RAG Extension (Optional)

RAG is NOT part of the MVP.

However, the architecture intentionally leaves room for future lightweight RAG capabilities.

Possible future enhancements:

* embeddings for journal entries,
* semantic search,
* recurring blocker analysis,
* long-term productivity summaries,
* vector search over historical data.

Potential future technologies:

* SQLite vector extension,
* pgvector,
* Qdrant,
* ChromaDB.

The future RAG layer should remain additive and should not require major architectural rewrites.
