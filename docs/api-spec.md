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

Returns extracted tasks.

### Response

```json
[
  {
    "id": 1,
    "title": "Fix frontend validation",
    "status": "todo"
  }
]
```

---

## GET /api/journal/has-entry-today

### Response

```json
{
  "hasEntryToday": true
}
```

---
