# API Spec

## POST /api/journal

Creates journal entry and runs AI extraction.

### Request

```json
{
  "content": "Worked on authentication flow today"
}
```

### Response

```json
{
  "id": 1,
  "summary": "Worked on authentication tasks",
  "tasks": []
}
```

---

## GET /api/tasks

Fetches current tasks from the Todoist API and returns them to the frontend. Tasks are not stored in SQLite — this endpoint proxies Todoist.

### Response

```json
[
  {
    "id": "todoist-task-id",
    "title": "Fix frontend validation",
    "status": "todo",
    "priority": "medium"
  }
]
```

---

## POST /api/tasks

Creates a new task in Todoist via the Todoist API.

### Request

```json
{
  "title": "Fix frontend validation",
  "priority": "medium"
}
```

---

## PATCH /api/tasks/{id}

Updates an existing task in Todoist via the Todoist API.

---

## DELETE /api/tasks/{id}

Deletes a task in Todoist via the Todoist API.

---

## GET /api/journal/has-entry-today

### Response

```json
{
  "hasEntryToday": true
}
```

---
