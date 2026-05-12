# Requirements

## Functional Requirements

### Journal

| ID | Requirement |
|----|-------------|
| FR-J1 | User can write free-form text in a journal entry textarea and submit it |
| FR-J2 | On submission, the backend sends the entry text to the EPAM Dial API for extraction |
| FR-J3 | The AI response is parsed and stored as: `completed_tasks`, `blockers`, `new_tasks` (title + priority), `summary` |
| FR-J4 | The journal entry and its AI extraction are persisted in SQLite |
| FR-J5 | The frontend displays the extraction result (all four fields) immediately after submission |
| FR-J6 | User can view a list of past journal entries |
| FR-J7 | Backend exposes `GET /api/journal/has-entry-today` for use by the n8n workflow |

### Tasks

| ID | Requirement |
|----|-------------|
| FR-T1 | Tasks extracted by AI (`new_tasks`) are automatically saved to the task list after journal submission |
| FR-T2 | User can manually create a task with a title and priority |
| FR-T3 | User can edit the title and priority of any task |
| FR-T4 | User can mark a task as complete (`done`) or reopen it (`pending`) |
| FR-T5 | User can delete a task |
| FR-T6 | Task list displays all tasks with title, priority badge, status indicator, and source (ai/manual) |

### Chat

| ID | Requirement |
|----|-------------|
| FR-C1 | User can type a message in a chat panel and receive an AI response |
| FR-C2 | Each chat request injects the user's current task list and latest journal entry into the system prompt before calling the AI |
| FR-C3 | Chat conversation history is shown within the current browser session |

### n8n Workflow

| ID | Requirement |
|----|-------------|
| FR-N1 | A scheduled n8n workflow fires once per day (e.g., 09:00 AM) |
| FR-N2 | The workflow calls `GET /api/journal/has-entry-today` on the backend |
| FR-N3 | If no entry exists for today, the workflow triggers a notification (webhook call, Slack message, or n8n execution log) |
| FR-N4 | The workflow demonstrates all four required primitives: schedule trigger, HTTP request, conditional (IF), notification/logging |

---

## Non-Functional Requirements

| ID | Requirement |
|----|-------------|
| NFR-1 | AI extraction must complete (including network + model latency) within 15 seconds under normal conditions |
| NFR-2 | The application runs locally in development mode — no cloud deployment is required for the PoC |
| NFR-3 | SQLite database is file-based (`productivity.db`); no database server is required |
| NFR-4 | The backend returns appropriate HTTP error codes (422 for AI parse failure, 502 for Dial API unavailability, 404 for not-found resources) |
| NFR-5 | CORS is configured to allow the React dev server origin (`http://localhost:5173`) to call the API |
| NFR-6 | No authentication is required — single-user PoC with no login flow |
| NFR-7 | The app does not need to handle concurrent users; single-threaded request handling is sufficient |

---

## AI-Specific Requirements

| ID | Requirement |
|----|-------------|
| AIR-1 | The extraction system prompt must instruct the model to return **only** a valid JSON object — no prose, no markdown, no code fences |
| AIR-2 | Model temperature must be set between 0.1 and 0.3 to ensure consistent, deterministic extraction |
| AIR-3 | The backend must validate that the AI response is parseable JSON and matches the extraction schema before saving; return HTTP 422 if validation fails |
| AIR-4 | All AI calls must go through EPAM Dial API — not OpenAI, Anthropic, or any other provider directly |
| AIR-5 | The chat system prompt must include the current task list and the latest journal entry text before forwarding the user's message to the model |
| AIR-6 | The extraction schema is fixed: `{ completed_tasks: string[], blockers: string[], new_tasks: { title: string, priority: "high" \| "medium" \| "low" }[], summary: string }` |

---

## Acceptance Criteria

### Journal Extraction

| ID | Criterion |
|----|-----------|
| AC-J1 | Given an entry containing completed work, the response includes ≥1 item in `completed_tasks` |
| AC-J2 | Given an entry mentioning a blocker, the response includes ≥1 item in `blockers` |
| AC-J3 | Given an entry mentioning future work, the response includes ≥1 item in `new_tasks`, each with a non-null `priority` value of `"high"`, `"medium"`, or `"low"` |
| AC-J4 | The `summary` field is a non-empty string of at most 3 sentences |
| AC-J5 | `GET /api/journal/{id}` returns the saved entry and extraction after submission |
| AC-J6 | If the AI returns malformed JSON, the API responds with HTTP 422 and an error message; the entry is still saved |

### Task Management

| ID | Criterion |
|----|-----------|
| AC-T1 | After journal submission, all `new_tasks` from the extraction appear in `GET /api/tasks` |
| AC-T2 | A manually created task (via `POST /api/tasks`) appears in the task list |
| AC-T3 | After editing a task title via `PUT /api/tasks/{id}`, `GET /api/tasks` reflects the updated title |
| AC-T4 | After `PATCH /api/tasks/{id}/status` with `{ "status": "done" }`, the task shows as complete |
| AC-T5 | After `DELETE /api/tasks/{id}`, the task no longer appears in `GET /api/tasks` |

### Chat

| ID | Criterion |
|----|-----------|
| AC-C1 | Asking "What are my current blockers?" returns a response that references the actual blockers stored in the system |
| AC-C2 | Chat history shows all messages sent and received within the current session |

### n8n Workflow

| ID | Criterion |
|----|-----------|
| AC-N1 | Manually triggering the workflow in n8n when no entry exists today results in a notification being sent (or logged) |
| AC-N2 | Manually triggering the workflow when an entry exists today results in no notification and a "no reminder needed" log |
