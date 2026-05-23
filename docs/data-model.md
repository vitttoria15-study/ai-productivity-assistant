# Data Model

## SQLite (App Metadata)

SQLite stores only app-specific metadata. Tasks and projects are owned by Todoist and accessed via the Todoist API — they are not stored locally.

### JournalEntry

| Field | Type | Description |
| --- | --- | --- |
| Id | int | Primary key |
| Content | text | Raw journal entry text |
| CreatedAt | datetime | Entry timestamp |
| Summary | text | AI-generated summary |

### ExtractedBlocker

| Field | Type | Description |
| --- | --- | --- |
| Id | int | Primary key |
| JournalEntryId | int | Foreign key → JournalEntry |
| Description | text | Blocker text extracted by AI |
| CreatedAt | datetime | Extraction timestamp |

### AutomationLog

| Field | Type | Description |
| --- | --- | --- |
| Id | int | Primary key |
| WorkflowName | text | n8n workflow name |
| EventType | text | Event type (e.g. JournalMissing, JournalExists) |
| Message | text | Execution message |
| CreatedAt | datetime | Log timestamp |

---

## Todoist (Tasks and Projects)

Tasks and projects are owned by Todoist and accessed exclusively via the Todoist API.

The app does not replicate task records in SQLite. A `JournalEntry` may store a Todoist task ID as a metadata reference after extraction, but the full task record lives in Todoist.

Supported task operations (create, read, update, delete) are proxied through the backend to the Todoist API.
