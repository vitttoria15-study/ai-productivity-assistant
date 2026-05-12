# API Specification

**Base URL (local development):** `http://localhost:5000`

All requests and responses use `Content-Type: application/json`.  
All timestamps are ISO 8601 UTC (e.g., `"2026-05-12T09:30:00Z"`).

---

## Journal Endpoints

### POST /api/journal

Submit a journal entry. Triggers AI extraction synchronously and returns the result.

**Request body:**
```json
{
  "entry_text": "Today I finished onboarding, but backend authentication is still blocked. Tomorrow I need to complete the project presentation."
}
```

**Response — 201 Created:**
```json
{
  "journal_entry_id": 1,
  "entry_text": "Today I finished onboarding, but backend authentication is still blocked. Tomorrow I need to complete the project presentation.",
  "created_at": "2026-05-12T09:30:00Z",
  "extraction": {
    "completed_tasks": [
      "Finished onboarding"
    ],
    "blockers": [
      "Backend authentication issue"
    ],
    "new_tasks": [
      {
        "title": "Complete project presentation",
        "priority": "high"
      }
    ],
    "summary": "Made progress on onboarding today. Backend authentication remains blocked. A high-priority presentation needs to be completed tomorrow."
  }
}
```

**Response — 422 Unprocessable Entity** (AI returned malformed JSON):
```json
{
  "error": "AI extraction failed",
  "message": "Could not parse the AI response as valid JSON. The journal entry was saved; please retry extraction."
}
```

**Response — 502 Bad Gateway** (EPAM Dial API unreachable):
```json
{
  "error": "AI service unavailable",
  "message": "EPAM Dial API returned an error. Please try again later."
}
```

---

### GET /api/journal

List all journal entries (summary only — no extraction detail).

**Response — 200 OK:**
```json
[
  {
    "id": 1,
    "entry_text": "Today I finished onboarding...",
    "created_at": "2026-05-12T09:30:00Z"
  },
  {
    "id": 2,
    "entry_text": "Fixed the login bug. Still waiting on design review.",
    "created_at": "2026-05-13T10:15:00Z"
  }
]
```

---

### GET /api/journal/{id}

Get a specific journal entry including its AI extraction.

**Response — 200 OK:**
```json
{
  "id": 1,
  "entry_text": "Today I finished onboarding...",
  "created_at": "2026-05-12T09:30:00Z",
  "extraction": {
    "completed_tasks": ["Finished onboarding"],
    "blockers": ["Backend authentication issue"],
    "new_tasks": [
      { "title": "Complete project presentation", "priority": "high" }
    ],
    "summary": "Made progress on onboarding today. Backend authentication remains blocked."
  }
}
```

**Response — 404 Not Found:**
```json
{ "error": "Journal entry not found" }
```

---

### GET /api/journal/has-entry-today

Check whether the user has submitted a journal entry for today's date. Used by the n8n reminder workflow.

**Response — 200 OK (entry exists):**
```json
{
  "has_entry": true,
  "date": "2026-05-12"
}
```

**Response — 200 OK (no entry today):**
```json
{
  "has_entry": false,
  "date": "2026-05-12"
}
```

---

## Task Endpoints

### GET /api/tasks

List all tasks. Supports optional query parameter filtering.

**Query parameters (optional):**
- `?status=pending` or `?status=done`
- `?priority=high` or `?priority=medium` or `?priority=low`

**Response — 200 OK:**
```json
[
  {
    "id": 1,
    "title": "Complete project presentation",
    "priority": "high",
    "status": "pending",
    "source": "ai",
    "journal_entry_id": 1,
    "created_at": "2026-05-12T09:30:00Z",
    "updated_at": "2026-05-12T09:30:00Z"
  },
  {
    "id": 2,
    "title": "Review pull request #42",
    "priority": "medium",
    "status": "done",
    "source": "manual",
    "journal_entry_id": null,
    "created_at": "2026-05-12T11:00:00Z",
    "updated_at": "2026-05-12T14:22:00Z"
  }
]
```

---

### POST /api/tasks

Create a task manually.

**Request body:**
```json
{
  "title": "Review pull request #42",
  "priority": "medium"
}
```

**Response — 201 Created:**
```json
{
  "id": 2,
  "title": "Review pull request #42",
  "priority": "medium",
  "status": "pending",
  "source": "manual",
  "journal_entry_id": null,
  "created_at": "2026-05-12T11:00:00Z",
  "updated_at": "2026-05-12T11:00:00Z"
}
```

**Validation — 400 Bad Request** (missing or empty title):
```json
{
  "error": "Validation failed",
  "message": "Title is required."
}
```

---

### PUT /api/tasks/{id}

Update a task's title and/or priority.

**Request body:**
```json
{
  "title": "Review pull request #42 and leave feedback",
  "priority": "high"
}
```

**Response — 200 OK:** (full task object as above)

**Response — 404 Not Found:**
```json
{ "error": "Task not found" }
```

---

### PATCH /api/tasks/{id}/status

Toggle or set a task's status.

**Request body:**
```json
{ "status": "done" }
```

Allowed values: `"pending"`, `"done"`

**Response — 200 OK:** (full task object with updated `status` and `updated_at`)

---

### DELETE /api/tasks/{id}

Delete a task.

**Response — 204 No Content**

**Response — 404 Not Found:**
```json
{ "error": "Task not found" }
```

---

## Chat Endpoint

### POST /api/chat

Send a message to the AI assistant. The backend injects the current task list and latest journal entry as context before calling EPAM Dial API.

**Request body:**
```json
{
  "message": "What should I focus on today?"
}
```

**Response — 200 OK:**
```json
{
  "reply": "Based on your current tasks and latest journal entry, your highest priority is completing the project presentation — it's marked high priority and was flagged as due tomorrow. The backend authentication blocker should also be escalated if it's been unresolved for more than a day."
}
```

**Response — 502 Bad Gateway** (Dial API unavailable):
```json
{
  "error": "AI service unavailable",
  "message": "Could not reach EPAM Dial API."
}
```

---

## AI Extraction Schema

All journal extraction responses conform to this structure:

```typescript
interface AiExtraction {
  // What the user completed since the last entry
  completed_tasks: string[];

  // Issues or impediments blocking progress
  blockers: string[];

  // New tasks to add to the task list
  new_tasks: NewTask[];

  // 1–3 sentence plain-language summary of the entry
  summary: string;
}

interface NewTask {
  title: string;
  priority: "high" | "medium" | "low";
}
```

**Priority mapping guidance (for prompting):**

| Signal in text | Priority |
|----------------|----------|
| "urgent", "ASAP", "today", "deadline", "due tomorrow" | high |
| "soon", "next", "should", "need to" | medium |
| "eventually", "maybe", "would be nice", "at some point" | low |
| No explicit signal | medium (default) |
