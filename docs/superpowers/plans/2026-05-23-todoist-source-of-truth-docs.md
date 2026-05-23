# Todoist Source of Truth — Documentation Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update all 11 affected markdown docs so every document consistently reflects that Todoist is the primary source of truth for tasks and projects, and SQLite stores only app-specific metadata.

**Architecture:** Todoist API is the task/project store; SQLite holds journal entries, AI summaries, extracted blockers, automation logs, and user preferences. The backend acts as an AI-enriched proxy over Todoist — no task records live in SQLite.

**Tech Stack:** Markdown only. No code changes of any kind.

---

## File Map

| File | Change type |
|---|---|
| `docs/data-model.md` | Rewrite — remove TaskItem, add updated entity table, add Todoist ownership note |
| `docs/architecture.md` | Edit — diagram, Database section, both data flows, backend components table |
| `README.md` | Edit — tech stack table, architecture diagram, database section, data flow, planned steps |
| `docs/concept.md` | Edit — architecture diagram, Task Management section, Technical Direction table |
| `docs/requirements.md` | Edit — FR-2, FR-3, NFR-4, Technical Requirements → Database and Backend |
| `docs/user-flows.md` | Edit — Flow 1 success criteria, Flow 2 source, Flow 3 backend target, Flow 4 context sources, Flow 6 context sources |
| `docs/api-spec.md` | Edit — /api/tasks description |
| `docs/ai-extraction-spec.md` | Edit — add Output Destinations section |
| `docs/roadmap.md` | Edit — Week 2 Task Management, MVP Priorities table |
| `docs/demo-plan.md` | Edit — Backend Initialization, demo flow description, Demo 2 task management, demo prep checklist |
| `docs/frontend-spec.md` | Edit — Task List source note |

**Order rationale:** data-model first (establishes the canonical split), then architecture (the most-referenced doc), then README (project entry point), then concept/requirements/flows/api/extraction, then roadmap/demo/frontend.

---

## Task 1: Rewrite `docs/data-model.md`

**Files:**
- Modify: `docs/data-model.md`

`Test-first: no — documentation edit; correctness verified by confirming TaskItem is absent and all three SQLite entities are present after the edit`

- [ ] **Step 1: Verify current content**

Run: `grep -n "TaskItem\|JournalEntry\|Allowed" docs/data-model.md`
Expected: lines referencing `TaskItem`, `JournalEntry`, `Allowed Statuses`.

- [ ] **Step 2: Replace entire file content**

Replace the full content of `docs/data-model.md` with:

```markdown
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
```

- [ ] **Step 3: Verify TaskItem is gone and new entities are present**

Run: `grep -n "TaskItem" docs/data-model.md`
Expected: no output (TaskItem removed).

Run: `grep -n "ExtractedBlocker\|AutomationLog\|Todoist" docs/data-model.md`
Expected: lines for all three new sections.

- [ ] **Step 4: Commit**

```bash
git add docs/data-model.md
git commit -m "docs: update data-model — remove TaskItem, establish SQLite/Todoist split"
```

---

## Task 2: Update `docs/architecture.md`

**Files:**
- Modify: `docs/architecture.md`

`Test-first: no — documentation edit; correctness verified by confirming Todoist appears in the diagram and SQLite description no longer lists tasks`

- [ ] **Step 1: Update the architecture diagram**

Find this block in `docs/architecture.md` (lines 34–38):

```
           |                         |
           |                         |
           v                         v
+-------------------+     +----------------------+
|   EPAM Dial API   |     |     SQLite DB        |
|   (LLM Gateway)   |     |   productivity.db    |
+-------------------+     +----------------------+
```

Replace with:

```
           |                |                    |
           |                |                    |
           v                v                    v
+------------------+ +------------------+ +--------------------+
|  EPAM Dial API   | |   Todoist API    | |    SQLite DB       |
|  (LLM Gateway)   | | (Tasks/Projects) | |  (App metadata)    |
+------------------+ +------------------+ +--------------------+
```

- [ ] **Step 2: Update the Database (SQLite) section**

Find this block (around line 119–133):

```markdown
## Database (SQLite)

SQLite is used as a lightweight local database for the PoC.

The database stores:

* journal entries,
* extracted AI summaries,
* tasks,
* blockers,
* metadata.
```

Replace with:

```markdown
## Database (SQLite)

SQLite is used as a lightweight local database for app-specific metadata.

The database stores:

* journal entries,
* AI-generated summaries,
* extracted blockers,
* automation logs,
* user preferences.

Tasks and projects are not stored in SQLite. They are owned by Todoist and accessed via the Todoist API.
```

- [ ] **Step 3: Update Journal Extraction data flow**

Find this block (around lines 192–200):

```
Backend saves:
    - journal entry
    - extracted tasks
    - blockers
    - summary
```

Replace with:

```
Backend saves to SQLite:
    - journal entry
    - AI summary
    - extracted blockers
Backend pushes to Todoist API:
    - new_tasks → created as Todoist tasks
    - completed_tasks → matching Todoist tasks marked done
```

- [ ] **Step 4: Update Context-Aware Chat data flow**

Find this block (around lines 208–213):

```
Backend loads:
    - current tasks
    - recent journal entries
    - blockers
```

Replace with:

```
Backend loads from Todoist API:
    - current tasks and projects
Backend loads from SQLite:
    - recent journal entries
    - active blockers
```

- [ ] **Step 5: Update Key Backend Components table**

Find this row (around line 271):

```
| TaskService       | Task management logic                |
```

Replace with:

```
| TaskService       | Todoist API integration and task proxy |
```

Find this row:

```
| AppDbContext      | SQLite persistence via EF Core       |
```

Replace with:

```
| AppDbContext      | SQLite persistence for app metadata (journal, blockers, logs) |
```

- [ ] **Step 6: Update Backend Responsibilities list**

Find (around line 256):

```
* task CRUD operations,
```

Replace with:

```
* task CRUD via Todoist API,
```

- [ ] **Step 7: Verify**

Run: `grep -n "tasks\|blockers" docs/architecture.md | grep -i "sqlite\|database stores\|saves"`
Review output — confirm no remaining line says SQLite stores tasks.

Run: `grep -n "Todoist" docs/architecture.md`
Expected: multiple lines across diagram, Database section, and data flows.

- [ ] **Step 8: Commit**

```bash
git add docs/architecture.md
git commit -m "docs: update architecture — add Todoist API, clarify SQLite metadata scope"
```

---

## Task 3: Update `README.md`

**Files:**
- Modify: `README.md`

`Test-first: no — documentation edit; correctness verified by confirming Todoist appears in tech stack and architecture, and SQLite description is scoped to metadata`

- [ ] **Step 1: Update tech stack table**

Find (around line 42–47):

```markdown
| Layer      | Technology                |
|------------|---------------------------|
| Frontend   | React + Vite              |
| Backend    | ASP.NET Core Web API      |
| Database   | SQLite                    |
| ORM        | Entity Framework Core     |
| AI         | EPAM Dial API (planned)   |
| Automation | n8n                       |
```

Replace with:

```markdown
| Layer           | Technology                        |
|-----------------|-----------------------------------|
| Frontend        | React + Vite                      |
| Backend         | ASP.NET Core Web API              |
| App metadata DB | SQLite (journal, summaries, logs) |
| ORM             | Entity Framework Core             |
| AI              | EPAM Dial API (planned)           |
| Task management | Todoist API                       |
| Automation      | n8n                               |
```

- [ ] **Step 2: Update architecture diagram**

Find (around lines 53–65):

```markdown
```text
React Frontend
       ↓
ASP.NET Core Web API
       ↓
AI Extraction Service (Mock)
       ↓
SQLite Database
```
```

Replace with:

```markdown
```text
React Frontend
       ↓
ASP.NET Core Web API
       ↓              ↓              ↓
EPAM Dial API   Todoist API    SQLite Database
(LLM Gateway)  (Tasks/Projects) (App metadata)
```
```

- [ ] **Step 3: Update the Database section**

Find (around lines 156–163):

```markdown
# Database

The project uses SQLite with Entity Framework Core.

Database file:

```text
backend/app.db
```
```

Replace with:

```markdown
# Database

The project uses SQLite with Entity Framework Core for app-specific metadata: journal entries, AI summaries, extracted blockers, and automation logs.

Database file:

```text
backend/app.db
```

Tasks and projects are not stored in SQLite. They are managed via the Todoist API.
```

- [ ] **Step 4: Update the example workflow data flow**

Find (around lines 216–224):

```markdown
4. Mock AI extraction generates:
   - tasks
   - priorities
   - blockers
   - summary
5. Data is stored in SQLite.
```

Replace with:

```markdown
4. Mock AI extraction generates:
   - tasks
   - priorities
   - blockers
   - summary
5. Journal entry and extracted blockers are saved to SQLite.
   Extracted tasks are created in Todoist via the Todoist API.
```

- [ ] **Step 5: Update Planned Next Steps — Week 2**

Find (around lines 198–200):

```markdown
- Add task prioritization and status updates
- Introduce project/task grouping
```

Replace with:

```markdown
- Add task prioritization and status updates via Todoist API
- Introduce project/task grouping via Todoist projects
```

- [ ] **Step 6: Verify**

Run: `grep -n "Todoist" README.md`
Expected: lines in tech stack, architecture, database section, and data flow.

Run: `grep -n "SQLite" README.md`
Review — confirm no line describes SQLite as storing tasks.

- [ ] **Step 7: Commit**

```bash
git add README.md
git commit -m "docs: update README — reflect Todoist as task store, SQLite as metadata"
```

---

## Task 4: Update `docs/concept.md`

**Files:**
- Modify: `docs/concept.md`

`Test-first: no — documentation edit; correctness verified by confirming Todoist appears in architecture overview and Technical Direction table`

- [ ] **Step 1: Update the Architecture Overview diagram**

Find (around lines 155–164):

```markdown
```text
React Frontend
    ↓
ASP.NET Core Web API
    ↓
EPAM Dial API (LLM)
    ↓
SQLite Database
```
```

Replace with:

```markdown
```text
React Frontend
    ↓
ASP.NET Core Web API
    ↓              ↓              ↓
EPAM Dial API  Todoist API   SQLite Database
(LLM Gateway) (Tasks/Projects) (App metadata)
```
```

- [ ] **Step 2: Update the Technical Direction table**

Find (around lines 143–149):

```markdown
| Layer          | Technology           |
| -------------- | -------------------- |
| Frontend       | React                |
| Backend        | ASP.NET Core Web API |
| Database       | SQLite               |
| AI Integration | EPAM Dial API        |
| Automation     | n8n                  |
```

Replace with:

```markdown
| Layer           | Technology                        |
| --------------- | --------------------------------- |
| Frontend        | React                             |
| Backend         | ASP.NET Core Web API              |
| App metadata DB | SQLite (journal, summaries, logs) |
| AI Integration  | EPAM Dial API                     |
| Task management | Todoist API                       |
| Automation      | n8n                               |
```

- [ ] **Step 3: Update the Task Management section**

Find (around lines 108–115):

```markdown
## Task Management

Users can:

* view tasks,
* manually create tasks,
* edit tasks,
* delete tasks,
* mark tasks as completed.
```

Replace with:

```markdown
## Task Management

Tasks are managed via the Todoist API. The app creates, reads, updates, and deletes tasks in Todoist.

Users can:

* view tasks sourced from Todoist,
* manually create tasks in Todoist,
* edit tasks in Todoist,
* delete tasks in Todoist,
* mark tasks as completed in Todoist.

The backend proxies task operations to Todoist and can enrich requests with AI-extracted context.
```

- [ ] **Step 4: Verify**

Run: `grep -n "Todoist" docs/concept.md`
Expected: lines in Technical Direction table, architecture diagram, and Task Management section.

- [ ] **Step 5: Commit**

```bash
git add docs/concept.md
git commit -m "docs: update concept — add Todoist to architecture and task management"
```

---

## Task 5: Update `docs/requirements.md`

**Files:**
- Modify: `docs/requirements.md`

`Test-first: no — documentation edit; correctness verified by confirming FR-3 references Todoist API, NFR-4 is scoped to metadata, and Database section no longer implies task storage in SQLite`

- [ ] **Step 1: Update FR-2 AI Extraction — add output destinations**

Find (around lines 79–81):

```markdown
The extraction should include:

* completed tasks,
* new tasks,
* blockers,
* priorities,
* summary.
```

After the closing bullet list, add a new subsection:

```markdown
### Output Destinations

After extraction:

* `new_tasks` are created in Todoist via the Todoist API.
* `completed_tasks` mark matching Todoist tasks as done.
* `blockers` and `summary` are saved to SQLite.
```

- [ ] **Step 2: Update FR-3 Task Management**

Find (around line 110):

```markdown
## FR-3 Task Management

The system shall support basic task CRUD functionality.
```

Replace with:

```markdown
## FR-3 Task Management

The system shall support basic task CRUD by proxying requests to the Todoist API.

Tasks are not stored in SQLite. All task create, read, update, and delete operations are handled by Todoist. The backend may enrich task creation with AI-extracted metadata (title, priority) from the extraction flow.
```

- [ ] **Step 3: Update NFR-4 SQLite Compatibility**

Find (around lines 258–264):

```markdown
## NFR-4 SQLite Compatibility

The backend should remain compatible with SQLite.

The system should avoid:

* database-specific enterprise features,
* complex distributed persistence.
```

Replace with:

```markdown
## NFR-4 SQLite Compatibility

The backend should remain compatible with SQLite for app-metadata persistence.

SQLite scope is limited to:

* journal entries,
* AI summaries,
* extracted blockers,
* automation logs,
* user preferences.

Task and project queries target the Todoist API, not SQLite. The system should avoid database-specific enterprise features and complex distributed persistence.
```

- [ ] **Step 4: Update Technical Requirements → Database section**

Find (around lines 363–368):

```markdown
## Database

* SQLite local database
* Simple schema
* Single-user data model
```

Replace with:

```markdown
## Database

* SQLite local database for app-specific metadata (journal entries, summaries, blockers, logs)
* Simple schema scoped to metadata tables
* Single-user data model
* Tasks and projects owned by Todoist — no task tables in SQLite
```

- [ ] **Step 5: Update Technical Requirements → Backend section**

Find (around lines 347–355):

```markdown
## Backend

* ASP.NET Core Web API
* REST API architecture
* SQLite persistence
* EF Core ORM
* HTTP client integration with EPAM Dial API
```

Replace with:

```markdown
## Backend

* ASP.NET Core Web API
* REST API architecture
* SQLite persistence for app metadata
* EF Core ORM
* HTTP client integration with EPAM Dial API
* HTTP client integration with Todoist API (task and project management)
```

- [ ] **Step 6: Verify**

Run: `grep -n "Todoist" docs/requirements.md`
Expected: lines in FR-2 output destinations, FR-3, NFR-4, and Technical Requirements Backend.

Run: `grep -n "SQLite" docs/requirements.md`
Review — confirm no line implies SQLite owns task data.

- [ ] **Step 7: Commit**

```bash
git add docs/requirements.md
git commit -m "docs: update requirements — FR-2/FR-3 Todoist routing, NFR-4 SQLite scope"
```

---

## Task 6: Update `docs/user-flows.md`

**Files:**
- Modify: `docs/user-flows.md`

`Test-first: no — documentation edit; correctness verified by confirming "Backend updates SQLite" is gone from task flows and Todoist appears as the task data source`

- [ ] **Step 1: Update Flow 1 success criteria**

Find (around lines 112–116):

```markdown
## Success Criteria

* User can submit a journal entry.
* AI returns structured JSON.
* Extracted tasks and blockers are visible in the UI.
* Data is saved in SQLite.
```

Replace with:

```markdown
## Success Criteria

* User can submit a journal entry.
* AI returns structured JSON.
* Extracted tasks and blockers are visible in the UI.
* Journal entry and extracted blockers are saved to SQLite.
* Extracted tasks are created in Todoist via the Todoist API.
```

- [ ] **Step 2: Update Flow 2 — View Extracted Tasks**

Find the flow description (around lines 139–144):

```markdown
```text
User opens task list
    ↓
Frontend sends GET /api/tasks
    ↓
Backend returns current tasks
    ↓
Frontend displays tasks grouped or sorted by status/priority
```
```

Replace with:

```markdown
```text
User opens task list
    ↓
Frontend sends GET /api/tasks
    ↓
Backend fetches tasks from the Todoist API
    ↓
Backend returns tasks to frontend
    ↓
Frontend displays tasks grouped or sorted by status/priority
```
```

- [ ] **Step 3: Update Flow 3 — Manage Tasks Manually**

Find the flow description (around lines 183–190):

```markdown
```text
User opens task list
    ↓
User creates or edits a task
    ↓
Frontend sends request to task API
    ↓
Backend updates SQLite
    ↓
Frontend refreshes task list
```
```

Replace with:

```markdown
```text
User opens task list
    ↓
User creates or edits a task
    ↓
Frontend sends request to task API
    ↓
Backend calls Todoist API (create / update / delete)
    ↓
Frontend refreshes task list
```
```

- [ ] **Step 4: Update Flow 4 — Chat context sources**

Find (around lines 238–243):

```markdown
```text
Backend loads:
    - current tasks
    - recent journal entries
    - blockers
```
```

Replace with:

```markdown
```text
Backend loads from Todoist API:
    - current tasks and projects
Backend loads from SQLite:
    - recent journal entries
    - active blockers
```
```

- [ ] **Step 5: Update Flow 6 — Productivity Summary context sources**

Find (around lines 341–344):

```markdown
```text
Backend loads recent journal entries and tasks
    ↓
Backend builds summary prompt
```
```

Replace with:

```markdown
```text
Backend loads from SQLite:
    - recent journal entries
Backend loads from Todoist API:
    - current tasks and projects
    ↓
Backend builds summary prompt
```
```

- [ ] **Step 6: Verify**

Run: `grep -n "Backend updates SQLite" docs/user-flows.md`
Expected: no output.

Run: `grep -n "Todoist" docs/user-flows.md`
Expected: lines in Flow 2, Flow 3, Flow 4, and Flow 6.

- [ ] **Step 7: Commit**

```bash
git add docs/user-flows.md
git commit -m "docs: update user-flows — task CRUD and context via Todoist API"
```

---

## Task 7: Update `docs/api-spec.md`

**Files:**
- Modify: `docs/api-spec.md`

`Test-first: no — documentation edit; correctness verified by confirming /api/tasks description references Todoist`

- [ ] **Step 1: Update /api/tasks description**

Find (around lines 28–43):

```markdown
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
```

Replace with:

```markdown
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

Updates a task in Todoist via the Todoist API.

---

## DELETE /api/tasks/{id}

Deletes a task in Todoist via the Todoist API.
```

- [ ] **Step 2: Verify**

Run: `grep -n "Todoist\|proxies" docs/api-spec.md`
Expected: lines in the /api/tasks section.

- [ ] **Step 3: Commit**

```bash
git add docs/api-spec.md
git commit -m "docs: update api-spec — /api/tasks proxies Todoist, add CRUD endpoints"
```

---

## Task 8: Update `docs/ai-extraction-spec.md`

**Files:**
- Modify: `docs/ai-extraction-spec.md`

`Test-first: no — documentation edit; correctness verified by confirming Output Destinations section is present`

- [ ] **Step 1: Add Output Destinations section**

Append to the end of `docs/ai-extraction-spec.md` (after the existing `---`):

```markdown
## Output Destinations

After the AI returns structured JSON, the backend routes each field:

| Field | Destination |
| --- | --- |
| `new_tasks` | Pushed to Todoist API — created as new Todoist tasks |
| `completed_tasks` | Todoist API — matching tasks marked as done |
| `blockers` | Saved to SQLite as `ExtractedBlocker` records linked to the journal entry |
| `summary` | Saved to SQLite as `JournalEntry.Summary` |
| `priorities` | Used in prompt context for chat; not persisted separately |

Tasks are never written to SQLite.
```

- [ ] **Step 2: Verify**

Run: `grep -n "Output Destinations\|Todoist" docs/ai-extraction-spec.md`
Expected: the new section heading and Todoist references.

- [ ] **Step 3: Commit**

```bash
git add docs/ai-extraction-spec.md
git commit -m "docs: add output destinations to ai-extraction-spec"
```

---

## Task 9: Update `docs/roadmap.md`

**Files:**
- Modify: `docs/roadmap.md`

`Test-first: no — documentation edit; correctness verified by confirming Todoist appears in Week 2 deliverables and MVP Priorities table`

- [ ] **Step 1: Update Week 2 Task Management deliverable**

Find (around lines 138–149):

```markdown
### 1. Task Management

The user should be able to:

* create tasks,
* edit tasks,
* delete tasks,
* mark tasks as completed,
* organize tasks by priority/status.
```

Replace with:

```markdown
### 1. Task Management (via Todoist API)

Task management is implemented through the Todoist API. The backend acts as a proxy between the frontend and Todoist.

The user should be able to:

* create tasks in Todoist,
* edit tasks in Todoist,
* delete tasks in Todoist,
* mark tasks as completed in Todoist,
* organize tasks by priority/status as returned by Todoist.
```

- [ ] **Step 2: Update MVP Priorities table**

Find (around lines 256–269):

```markdown
| Priority | Feature                   |
| -------- | ------------------------- |
| P0       | Journal → AI extraction   |
| P0       | EPAM Dial API integration |
| P1       | Basic React UI            |
| P1       | Task management           |
| P1       | SQLite persistence        |
| P1       | n8n workflow              |
| P2       | Context-aware AI chat     |
| P2       | Journal history           |
| P3       | Weekly/monthly summaries  |
| P3       | Lightweight RAG           |
| P3       | Gamification              |
```

Replace with:

```markdown
| Priority | Feature                                          |
| -------- | ------------------------------------------------ |
| P0       | Journal → AI extraction                          |
| P0       | EPAM Dial API integration                        |
| P0       | Todoist API integration (task management)        |
| P1       | Basic React UI                                   |
| P1       | Task management via Todoist                      |
| P1       | SQLite metadata persistence (journal, logs)      |
| P1       | n8n workflow                                     |
| P2       | Context-aware AI chat                            |
| P2       | Journal history                                  |
| P3       | Weekly/monthly summaries                         |
| P3       | Lightweight RAG                                  |
| P3       | Gamification                                     |
```

- [ ] **Step 3: Verify**

Run: `grep -n "Todoist" docs/roadmap.md`
Expected: lines in Week 2 Task Management and MVP Priorities.

- [ ] **Step 4: Commit**

```bash
git add docs/roadmap.md
git commit -m "docs: update roadmap — Todoist integration in Week 2 and MVP priorities"
```

---

## Task 10: Update `docs/demo-plan.md`

**Files:**
- Modify: `docs/demo-plan.md`

`Test-first: no — documentation edit; correctness verified by confirming demo prep checklist includes Todoist and task management demo references Todoist`

- [ ] **Step 1: Update Backend Initialization section**

Find (around lines 76–81):

```markdown
Demonstrate:

* ASP.NET Core Web API initialized,
* SQLite configured,
* EPAM Dial API integration prepared or working.
```

Replace with:

```markdown
Demonstrate:

* ASP.NET Core Web API initialized,
* SQLite configured for app metadata (journal entries, logs),
* EPAM Dial API integration prepared or working,
* Todoist API integration configured.
```

- [ ] **Step 2: Update Demo 1 AI Extraction flow description**

Find (around lines 107–112):

```markdown
Backend:

* sends prompt to EPAM Dial API,
* receives structured JSON,
* stores extracted information,
* returns result to UI.
```

Replace with:

```markdown
Backend:

* sends prompt to EPAM Dial API,
* receives structured JSON,
* saves journal entry and extracted blockers to SQLite,
* creates extracted tasks in Todoist via the Todoist API,
* returns result to UI.
```

- [ ] **Step 3: Update Demo 2 Task Management section**

Find (around lines 201–210):

```markdown
## Task Management

Demonstrate:

* create task,
* edit task,
* delete task,
* mark task completed,
* task prioritization.
```

Replace with:

```markdown
## Task Management (via Todoist)

Demonstrate task management through the Todoist API:

* create task in Todoist,
* edit task in Todoist,
* delete task in Todoist,
* mark task completed in Todoist,
* task prioritization reflected from Todoist.
```

- [ ] **Step 4: Update demo preparation checklist**

Find (around lines 351–359):

```markdown
Before each demo:

* prepare demo data,
* pre-seed several journal entries,
* test AI prompts,
* verify API connectivity,
* verify SQLite persistence,
* verify n8n workflow execution,
* prepare fallback screenshots in case of API/network issues.
```

Replace with:

```markdown
Before each demo:

* prepare demo data,
* pre-seed several journal entries,
* test AI prompts,
* verify EPAM Dial API connectivity,
* verify Todoist API connectivity and demo tasks are present,
* verify SQLite persistence for journal entries and logs,
* verify n8n workflow execution,
* prepare fallback screenshots in case of API/network issues.
```

- [ ] **Step 5: Verify**

Run: `grep -n "Todoist" docs/demo-plan.md`
Expected: lines in Backend Initialization, AI Extraction flow, Task Management, and demo prep checklist.

- [ ] **Step 6: Commit**

```bash
git add docs/demo-plan.md
git commit -m "docs: update demo-plan — Todoist integration in demo flows and prep checklist"
```

---

## Task 11: Update `docs/frontend-spec.md`

**Files:**
- Modify: `docs/frontend-spec.md`

`Test-first: no — documentation edit; correctness verified by confirming Task List section references Todoist as data source`

- [ ] **Step 1: Update Task List section**

Find (around lines 20–25):

```markdown
### Task List

Simple list of extracted tasks.

No authentication.
No advanced styling.
No chat.
```

Replace with:

```markdown
### Task List

Displays tasks sourced from the Todoist API via the backend. The frontend does not read task data from SQLite.

No authentication.
No advanced styling.
No chat.
```

- [ ] **Step 2: Verify**

Run: `grep -n "Todoist" docs/frontend-spec.md`
Expected: line in Task List section.

- [ ] **Step 3: Commit**

```bash
git add docs/frontend-spec.md
git commit -m "docs: update frontend-spec — task list sources from Todoist API"
```

---

## Task 12: Final consistency check

**Files:**
- Read-only scan across all 11 docs.

`Test-first: no — verification scan only; no file edits unless a contradiction is found`

- [ ] **Step 1: Scan for any remaining SQLite-as-task-store patterns**

Run:
```bash
grep -rn "SQLite.*task\|task.*SQLite\|Backend updates SQLite\|saves.*tasks.*SQLite\|stored.*SQLite.*task" \
  README.md docs/architecture.md docs/data-model.md docs/concept.md \
  docs/requirements.md docs/user-flows.md docs/api-spec.md \
  docs/ai-extraction-spec.md docs/roadmap.md docs/demo-plan.md \
  docs/frontend-spec.md
```
Expected: no output. If any line appears, fix the owning doc and commit.

- [ ] **Step 2: Confirm Todoist is present in all architecture-level docs**

Run:
```bash
grep -l "Todoist" README.md docs/architecture.md docs/concept.md \
  docs/requirements.md docs/roadmap.md docs/demo-plan.md
```
Expected: all 6 files listed.

- [ ] **Step 3: Confirm data-model.md has no TaskItem**

Run: `grep -n "TaskItem" docs/data-model.md`
Expected: no output.

- [ ] **Step 4: Commit final verification note (if no fixes needed)**

```bash
git add -A
git commit -m "docs: final consistency check — all docs reflect Todoist-first architecture" --allow-empty
```

(Use `--allow-empty` only if no files were changed in this step; otherwise commit normally.)
