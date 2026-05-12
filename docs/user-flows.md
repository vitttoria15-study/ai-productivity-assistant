# User Flows

## Overview

The application has three tabs / primary views:
- **Journal** — write and submit entries; view AI extraction results; browse history
- **Tasks** — view, create, edit, complete, and delete tasks
- **Chat** — ask the AI assistant questions with task and journal context

---

## Flow 1: Journal Entry and AI Extraction

**Goal:** User writes a progress note and sees structured AI output.

```
1.  User opens the app → "Journal" tab is shown by default
2.  User types in the journal textarea:

    "Today I finished onboarding, but backend authentication is still blocked.
     Tomorrow I need to complete the project presentation."

3.  User clicks "Analyze" button
4.  Button shows loading state (spinner or disabled + "Analyzing...")
5.  React POST /api/journal  { entry_text: "..." }

6.  Backend:
      a. Saves entry to journal_entries
      b. Sends entry to EPAM Dial API via DialService
      c. Parses JSON response
      d. Saves extraction to ai_extractions
      e. Saves new_tasks to tasks (source = "ai")
      f. Returns 201 with extraction result

7.  UI displays extraction panel:

    ┌──────────────────────────────────────────────────────────┐
    │  Summary                                                 │
    │  Made progress on onboarding. Authentication remains     │
    │  blocked. Presentation is due tomorrow.                  │
    ├─────────────────┬──────────────────┬────────────────────┤
    │  ✅ Completed    │  🚧 Blockers      │  📋 New Tasks       │
    │  • Finished      │  • Backend auth  │  • Complete pres.  │
    │    onboarding    │    issue         │    (HIGH)           │
    └─────────────────┴──────────────────┴────────────────────┘

8.  Task list (Tasks tab) is updated — new tasks appear there
9.  Entry appears in the journal history list below the form
```

**Error path (AI failure):**
- If AI call returns 422 or 502, show inline error under the button:
  "AI extraction failed. Your entry was saved — you can retry or continue manually."
- Journal entry remains saved; task list is not modified.

---

## Flow 2: Task Management

**Goal:** User views, creates, edits, and manages tasks across any source.

```
1.  User clicks "Tasks" tab
2.  Task list loads: GET /api/tasks
3.  Each task row shows:
    ─────────────────────────────────────────────────
    ☐  Complete project presentation    [HIGH]  [AI]
    ☐  Review pull request #42          [MED]   [Manual]
    ✓  Finished onboarding             [LOW]   [AI]   (strikethrough)
    ─────────────────────────────────────────────────
```

**Add a task manually:**
```
4.  User clicks "+ Add Task"
5.  Inline form appears:
    [ Title input field            ] [ Priority: Medium ▼ ] [Save] [Cancel]
6.  User fills in title and priority → clicks Save
7.  POST /api/tasks  { title, priority }
8.  New task appears in list with source = "manual"
```

**Mark a task done:**
```
9.  User clicks the checkbox ☐ next to a pending task
10. PATCH /api/tasks/{id}/status  { status: "done" }
11. Task row updates: checkbox becomes ✓, title shown with strikethrough
    (Clicking again toggles back to "pending")
```

**Edit a task:**
```
12. User clicks the pencil ✏️ icon on a task
13. Title and priority become inline-editable
14. User makes changes → clicks Save (or presses Enter)
15. PUT /api/tasks/{id}  { title, priority }
16. Row updates with new values
```

**Delete a task:**
```
17. User clicks the trash 🗑️ icon on a task
18. DELETE /api/tasks/{id}
19. Task is removed from the list immediately
    (No confirmation prompt required for MVP)
```

---

## Flow 3: AI Chat Assistant

**Goal:** User asks a question and gets an AI answer that is aware of their current tasks and latest journal entry.

```
1.  User clicks "Chat" tab
2.  Chat panel shows empty conversation history
3.  System context is built server-side (not visible to user):
    - All current tasks
    - Latest journal entry (most recent by created_at)

4.  User types: "What should I focus on today?"
5.  User clicks Send (or presses Enter)
6.  React POST /api/chat  { message: "What should I focus on today?" }

7.  Backend:
      a. Fetches current tasks from DB
      b. Fetches latest journal entry from DB
      c. Builds system prompt with task list + journal text injected
      d. Sends to EPAM Dial API
      e. Returns { reply: "..." }

8.  Chat panel shows:

    ─────────────────────────────────────────────────────────
    You:  What should I focus on today?

    AI:   Based on your tasks and latest journal entry, your
          highest priority is completing the project
          presentation — it's marked HIGH and was flagged
          as due tomorrow. The backend authentication blocker
          should also be addressed; consider escalating it if
          it's been unresolved for more than a day.
    ─────────────────────────────────────────────────────────

9.  User continues conversation:
    "How many tasks do I have left?"

10. Same flow repeats — each POST /api/chat is stateless on the
    backend; conversation history is shown in the React UI only
    (in-memory for MVP)
```

**Limitations (MVP):**
- Refreshing the page clears the chat history
- The backend does not remember previous chat turns — each request is independent
- Multi-turn context (carrying prior chat messages) is a stretch goal

---

## Flow 4: n8n Daily Journal Reminder

**Goal:** n8n automatically reminds the user to write a journal entry if they haven't today.

```
1.  n8n cron trigger fires at 09:00 AM daily (configurable)

2.  HTTP Request node:
    GET http://localhost:5000/api/journal/has-entry-today

3.  n8n IF node evaluates:  response.body.has_entry === false?

    ── TRUE (no entry today):
    4a. HTTP Request / notification node sends reminder:
        "Reminder: Write your journal entry for today."
        (Target: webhook.site, Slack webhook, or n8n log)
    5a. n8n logs: "Journal reminder sent at {timestamp}"

    ── FALSE (entry exists):
    4b. n8n logs: "Journal entry already written for {date}. No reminder sent."

6.  n8n execution history shows the run result
```

**Demo mode:** The workflow can be triggered manually via n8n UI "Execute workflow" button — no need to wait for 09:00 to demonstrate it.

---

## Navigation Summary

```
┌────────────────────────────────────────────────────┐
│  AI Productivity Assistant            [Journal] [Tasks] [Chat]  │
├────────────────────────────────────────────────────┤
│                                                    │
│  [Journal tab — default]                          │
│   ┌───────────────────────────────────────────┐   │
│   │  What did you work on today?              │   │
│   │  ┌─────────────────────────────────────┐ │   │
│   │  │                                     │ │   │
│   │  │  (textarea)                         │ │   │
│   │  │                                     │ │   │
│   │  └─────────────────────────────────────┘ │   │
│   │                          [Analyze ▶]     │   │
│   └───────────────────────────────────────────┘   │
│                                                    │
│  [Extraction result panel — appears after submit] │
│  [Journal history list — past entries below]      │
│                                                    │
└────────────────────────────────────────────────────┘
```
