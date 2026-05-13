# API Specification

## Overview

This document describes the planned REST API for the AI Productivity Assistant PoC.

The API is designed for:

* single-user usage,
* lightweight AI-assisted workflows,
* simple task and journal management,
* integration with EPAM Dial API.

The API intentionally avoids:

* authentication,
* multi-user support,
* enterprise-level complexity,
* advanced orchestration.

---

# Base URL

```text
/api
```

---

# Architecture Context

```text
React Frontend
    ↓
ASP.NET Core Web API
    ↓
EPAM Dial API (LLM)
    ↓
SQLite Database
```

---

# Core API Goals

The backend should:

* accept journal entries,
* send prompts to the LLM,
* extract structured information,
* persist tasks/journal data,
* provide contextual AI responses,
* support lightweight productivity workflows.

---

# AI Extraction Response Shape

The AI should always return structured JSON in the following format:

```json
{
  "completed_tasks": [
    "string"
  ],
  "new_tasks": [
    {
      "title": "string",
      "priority": "low | medium | high"
    }
  ],
  "blockers": [
    "string"
  ],
  "priorities": [
    "string"
  ],
  "summary": "string"
}
```

---

# Endpoints

---

# 1. Submit Journal Entry

## POST `/api/journal`

Accepts a natural-language journal/progress entry, sends it to the AI model, extracts structured information, stores the result, and returns the AI analysis.

---

## Request

```json
{
  "text": "Today I finished onboarding, but backend authentication is still blocked. Tomorrow I need to complete the presentation."
}
```

---

## Backend Flow

```text
Receive Journal Entry
→ Send prompt to EPAM Dial API
→ Receive structured JSON
→ Save journal entry
→ Save extracted tasks/blockers
→ Return response to frontend
```

---

## Response

```json
{
  "journalEntryId": 1,
  "analysis": {
    "completed_tasks": [
      "Finished onboarding"
    ],
    "new_tasks": [
      {
        "title": "Complete presentation",
        "priority": "high"
      }
    ],
    "blockers": [
      "Backend authentication issue"
    ],
    "priorities": [
      "Presentation preparation"
    ],
    "summary": "User completed onboarding but is blocked by backend authentication issues."
  }
}
```

---

# 2. Get Journal History

## GET `/api/journal`

Returns previously submitted journal entries.

---

## Response

```json
[
  {
    "id": 1,
    "text": "Today I finished onboarding...",
    "createdAt": "2026-05-10T18:00:00Z"
  }
]
```

---

# 3. Get Tasks

## GET `/api/tasks`

Returns all current tasks.

---

## Response

```json
[
  {
    "id": 1,
    "title": "Complete presentation",
    "priority": "high",
    "status": "in_progress"
  }
]
```

---

# 4. Create Task

## POST `/api/tasks`

Allows manual task creation in addition to AI-generated tasks.

---

## Request

```json
{
  "title": "Setup React frontend",
  "priority": "medium"
}
```

---

## Response

```json
{
  "id": 2,
  "title": "Setup React frontend",
  "priority": "medium",
  "status": "todo"
}
```

---

# 5. Update Task

## PUT `/api/tasks/{id}`

Updates task fields.

---

## Request

```json
{
  "title": "Setup React frontend",
  "priority": "high",
  "status": "in_progress"
}
```

---

## Response

```json
{
  "success": true
}
```

---

# 6. Delete Task

## DELETE `/api/tasks/{id}`

Deletes a task.

---

## Response

```json
{
  "success": true
}
```

---

# 7. Update Task Status

## PATCH `/api/tasks/{id}/status`

Updates only task status.

---

## Request

```json
{
  "status": "done"
}
```

---

## Response

```json
{
  "success": true
}
```

---

# 8. AI Chat Endpoint

## POST `/api/chat`

Allows lightweight contextual AI interactions.

The backend injects:

* current tasks,
* latest journal entries,
* blockers,
* priorities

into the system prompt before sending the request to EPAM Dial API.

---

## Request

```json
{
  "message": "What should I focus on tomorrow?"
}
```

---

## Example Prompt Strategy

```text
System:
You are a productivity assistant.

Current Tasks:
- Complete presentation (High)
- Setup React frontend (Medium)

Recent Blockers:
- Backend authentication issue

User:
What should I focus on tomorrow?
```

---

## Response

```json
{
  "response": "Focus on resolving the backend authentication blocker first because it impacts multiple ongoing tasks."
}
```

---

# 9. Daily Journal Check Endpoint

## GET `/api/journal/has-entry-today`

Used by the n8n workflow.

Returns whether the user already submitted a journal entry today.

---

## Response

```json
{
  "hasEntryToday": false
}
```

---

# n8n Integration

n8n is NOT part of the core runtime path.

The n8n workflow exists as a separate automation layer for:

* scheduled reminders,
* notifications,
* logging,
* Week 1 low-code/no-code requirement.

---

# Planned n8n Workflow

```text
Schedule Trigger
→ GET /api/journal/has-entry-today
→ IF hasEntryToday == false
→ Send Reminder Notification
→ Log Execution
```

---

# Error Handling

The API should:

* return meaningful error messages,
* gracefully handle Dial API failures,
* handle malformed AI JSON responses,
* prevent application crashes on invalid input.

---

# MVP Scope Notes

The MVP intentionally excludes:

* authentication,
* multi-user collaboration,
* advanced Kanban boards,
* long-term memory systems,
* vector databases,
* full RAG pipelines,
* autonomous AI agents.

---

# Future Enhancements

Possible future API additions:

* weekly AI summaries,
* monthly productivity analytics,
* semantic search across journal entries,
* lightweight RAG memory,
* voice input support,
* gamification endpoints.

Example future endpoints:

```text
GET /api/journal/weekly-summary
GET /api/analytics/productivity
POST /api/voice/transcribe
```
