# Data Model

## Overview

This document describes the planned data model for the AI Productivity Assistant PoC.

The application uses SQLite as a lightweight local database. The model is intentionally simple and optimized for:

* single-user usage,
* fast development,
* clear MVP behavior,
* demo readiness.

The model supports:

* journal entries,
* AI extraction results,
* tasks,
* blockers,
* contextual AI chat.

The MVP intentionally avoids:

* users/accounts,
* roles and permissions,
* multi-tenancy,
* complex workflow engines,
* enterprise-scale data modeling.

---

# Core Entities

## 1. JournalEntry

Represents a natural-language progress or journal entry submitted by the user.

### Purpose

Journal entries are the primary input for AI extraction.

### Fields

| Field     | Type     | Required | Notes                    |
| --------- | -------- | -------- | ------------------------ |
| Id        | integer  | Yes      | Primary key              |
| Text      | text     | Yes      | Raw journal entry text   |
| CreatedAt | datetime | Yes      | Entry creation timestamp |

### Example

```json
{
  "id": 1,
  "text": "Today I finished onboarding, but Dial API setup is still blocked.",
  "createdAt": "2026-05-10T18:00:00Z"
}
```

---

## 2. TaskItem

Represents a task managed by the user or extracted by AI from a journal entry.

### Purpose

Tasks are the main actionable items in the application.

### Fields

| Field          | Type     | Required | Notes                                |
| -------------- | -------- | -------- | ------------------------------------ |
| Id             | integer  | Yes      | Primary key                          |
| Title          | text     | Yes      | Task title                           |
| Description    | text     | No       | Optional details                     |
| Priority       | text     | Yes      | low, medium, high                    |
| Status         | text     | Yes      | todo, in_progress, blocked, done     |
| Source         | text     | Yes      | manual or ai                         |
| JournalEntryId | integer  | No       | Linked journal entry if AI-generated |
| CreatedAt      | datetime | Yes      | Creation timestamp                   |
| UpdatedAt      | datetime | No       | Last update timestamp                |
| CompletedAt    | datetime | No       | Completion timestamp                 |

### Example

```json
{
  "id": 1,
  "title": "Complete project presentation",
  "description": null,
  "priority": "high",
  "status": "todo",
  "source": "ai",
  "journalEntryId": 1,
  "createdAt": "2026-05-10T18:05:00Z",
  "updatedAt": null,
  "completedAt": null
}
```

---

## 3. AIExtraction

Represents the structured AI analysis result for a journal entry.

### Purpose

Stores the raw AI extraction result so the application can display and inspect what the model extracted from the journal entry.

### Fields

| Field              | Type     | Required | Notes                     |
| ------------------ | -------- | -------- | ------------------------- |
| Id                 | integer  | Yes      | Primary key               |
| JournalEntryId     | integer  | Yes      | Related journal entry     |
| Summary            | text     | No       | AI-generated summary      |
| CompletedTasksJson | text     | No       | JSON array stored as text |
| NewTasksJson       | text     | No       | JSON array stored as text |
| BlockersJson       | text     | No       | JSON array stored as text |
| PrioritiesJson     | text     | No       | JSON array stored as text |
| RawResponseJson    | text     | No       | Full raw AI response      |
| CreatedAt          | datetime | Yes      | Extraction timestamp      |

### Notes

SQLite does not require a dedicated JSON column type for this PoC. JSON arrays can be stored as text.

This keeps the schema simple and avoids unnecessary normalization for MVP.

### Example

```json
{
  "id": 1,
  "journalEntryId": 1,
  "summary": "User completed onboarding but is blocked by Dial API setup.",
  "completedTasksJson": "[\"Finished onboarding\"]",
  "newTasksJson": "[{\"title\":\"Finish project presentation\",\"priority\":\"high\"}]",
  "blockersJson": "[\"Dial API setup\"]",
  "prioritiesJson": "[\"Project presentation\"]",
  "createdAt": "2026-05-10T18:05:00Z"
}
```

---

## 4. ChatMessage

Represents an in-memory or optionally persisted chat message between the user and the AI assistant.

### Purpose

For the MVP, chat history may remain in React state only.

If persistence is needed later, this entity can be used.

### Fields

| Field     | Type     | Required | Notes             |
| --------- | -------- | -------- | ----------------- |
| Id        | integer  | Yes      | Primary key       |
| Role      | text     | Yes      | user or assistant |
| Message   | text     | Yes      | Message content   |
| CreatedAt | datetime | Yes      | Message timestamp |

### MVP Note

Persisting chat history is optional and not required for the first demo.

---

# Relationships

```text
JournalEntry 1 ─── 0..1 AIExtraction
JournalEntry 1 ─── 0..n TaskItem
TaskItem     n ─── 0..1 JournalEntry
```

Optional future relationship:

```text
ChatMessage can be stored independently if chat persistence is added later.
```

---

# SQLite Schema Draft

## journal_entries

```sql
CREATE TABLE journal_entries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    text TEXT NOT NULL,
    created_at TEXT NOT NULL
);
```

---

## ai_extractions

```sql
CREATE TABLE ai_extractions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    journal_entry_id INTEGER NOT NULL,
    summary TEXT,
    completed_tasks_json TEXT,
    new_tasks_json TEXT,
    blockers_json TEXT,
    priorities_json TEXT,
    raw_response_json TEXT,
    created_at TEXT NOT NULL,
    FOREIGN KEY (journal_entry_id) REFERENCES journal_entries(id) ON DELETE CASCADE
);
```

---

## tasks

```sql
CREATE TABLE tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    description TEXT,
    priority TEXT NOT NULL DEFAULT 'medium',
    status TEXT NOT NULL DEFAULT 'todo',
    source TEXT NOT NULL DEFAULT 'manual',
    journal_entry_id INTEGER,
    created_at TEXT NOT NULL,
    updated_at TEXT,
    completed_at TEXT,
    FOREIGN KEY (journal_entry_id) REFERENCES journal_entries(id) ON DELETE SET NULL,
    CHECK (priority IN ('low', 'medium', 'high')),
    CHECK (status IN ('todo', 'in_progress', 'blocked', 'done')),
    CHECK (source IN ('manual', 'ai'))
);
```

---

## chat_messages Optional

```sql
CREATE TABLE chat_messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    role TEXT NOT NULL,
    message TEXT NOT NULL,
    created_at TEXT NOT NULL,
    CHECK (role IN ('user', 'assistant'))
);
```

---

# Entity Usage by Feature

| Feature               | Entities Used                        |
| --------------------- | ------------------------------------ |
| Submit journal entry  | JournalEntry, AIExtraction, TaskItem |
| View journal history  | JournalEntry, AIExtraction           |
| View task list        | TaskItem                             |
| Manual task CRUD      | TaskItem                             |
| AI chat with context  | TaskItem, JournalEntry, AIExtraction |
| n8n reminder workflow | JournalEntry                         |
| Weekly summaries      | JournalEntry, AIExtraction, TaskItem |

---

# MVP Data Decisions

## Keep Task Model Simple

The MVP should not introduce complex project hierarchy, labels, dependencies, subtasks, or recurring tasks.

These can be added later if needed.

---

## Store AI Extraction as JSON Text

AI responses can vary slightly. Storing extracted arrays as JSON text allows fast iteration without over-normalizing the schema.

For the MVP, this is acceptable and easier to implement.

---

## Single-User Assumption

No `users` table is needed for the MVP.

All data is assumed to belong to one local user.

---

## Chat Persistence Is Optional

For the MVP, chat messages may remain in frontend memory.

Persisting chat history can be added later if it improves the demo or final experience.

---

# Future Data Model Extensions

Possible future entities:

## Project

Could support grouping tasks by area:

* Work,
* Learning,
* Personal,
* Health.

Example fields:

* Id,
* Name,
* Description,
* CreatedAt.

---

## Tag

Could support flexible categorization.

Example fields:

* Id,
* Name.

---

## TaskDependency

Could support dependency-aware prioritization.

Example fields:

* TaskId,
* DependsOnTaskId.

---

## JournalEmbedding

Could support future RAG/memory functionality.

Example fields:

* Id,
* JournalEntryId,
* EmbeddingVector,
* CreatedAt.

---

# Future RAG / Memory Extension

RAG is not part of the MVP data model.

If lightweight RAG is added later, the system may store embeddings for journal entries and use semantic search to answer historical questions.

Possible future questions:

* “What blockers repeated this month?”
* “What did I complete this week?”
* “What topics consumed most of my time recently?”

Potential storage options:

* SQLite vector extension,
* pgvector,
* Qdrant,
* ChromaDB.

This should be implemented only after the core MVP is stable.
