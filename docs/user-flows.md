# User Flows

## Overview

This document describes the main user flows for the AI Productivity Assistant PoC.

The flows are designed for a single-user MVP and focus on:

* journal-driven productivity management,
* AI task extraction,
* lightweight task management,
* contextual AI assistance,
* productivity summaries.

The MVP intentionally avoids:

* multi-user collaboration,
* authentication flows,
* complex Kanban workflows,
* autonomous AI agents,
* full RAG-based memory.

---

# Primary User Journey

## Goal

The user wants to unload thoughts, progress, blockers, and plans into the application using natural language.

The system should help transform that unstructured input into organized tasks, priorities, blockers, and summaries.

---

# Flow 1 — Submit Journal Entry and Extract Tasks

## Purpose

This is the core MVP flow.

It demonstrates the main AI value of the application: converting free-form journal text into structured productivity data.

---

## User Story

As a user, I want to write a natural-language journal entry so that the AI assistant can extract tasks, blockers, and priorities from it.

---

## Flow

```text
User opens the application
    ↓
User enters a journal/progress update
    ↓
User clicks Submit
    ↓
Frontend sends POST /api/journal
    ↓
Backend sends prompt to EPAM Dial API
    ↓
AI returns structured extraction
    ↓
Backend saves journal entry and extracted data
    ↓
Frontend displays AI extraction result
```

---

## Example Input

```text
Today I finished onboarding, but Dial API setup is still blocked.
Tomorrow I need to prepare the demo presentation and continue the React setup.
```

---

## Expected AI Output

```json
{
  "completed_tasks": [
    "Finished onboarding"
  ],
  "new_tasks": [
    {
      "title": "Prepare demo presentation",
      "priority": "high"
    },
    {
      "title": "Continue React setup",
      "priority": "medium"
    }
  ],
  "blockers": [
    "Dial API setup"
  ],
  "priorities": [
    "Prepare demo presentation"
  ],
  "summary": "User completed onboarding, is blocked by Dial API setup, and plans to prepare the demo presentation and continue React setup."
}
```

---

## Success Criteria

* User can submit a journal entry.
* AI returns structured JSON.
* Extracted tasks and blockers are visible in the UI.
* Data is saved in SQLite.

---

# Flow 2 — View Extracted Tasks

## Purpose

The user can review tasks created manually or extracted by AI.

---

## User Story

As a user, I want to see my current tasks so that I can understand what needs to be done next.

---

## Flow

```text
User opens task list
    ↓
Frontend sends GET /api/tasks
    ↓
Backend returns current tasks
    ↓
Frontend displays tasks grouped or sorted by status/priority
```

---

## Task Display Should Include

* title,
* priority,
* status,
* source: manual or AI,
* optional description.

---

## Success Criteria

* User can view the list of current tasks.
* AI-generated tasks are visible.
* Task priority and status are clear.

---

# Flow 3 — Manage Tasks Manually

## Purpose

The application should not rely only on AI extraction. The user should also be able to manage tasks manually.

---

## User Story

As a user, I want to create, update, and complete tasks manually so that I can keep my task list accurate.

---

## Flow

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

---

## Supported Actions

* create task,
* edit task title/description/priority/status,
* delete task,
* mark task as completed,
* reopen task if needed.

---

## Success Criteria

* User can create tasks manually.
* User can edit existing tasks.
* User can mark tasks as done.
* User can delete tasks.

---

# Flow 4 — Ask Context-Aware AI Chat Questions

## Purpose

The user can ask productivity-related questions and receive answers based on current tasks and recent journal context.

This flow provides an assistant-like experience without implementing full long-term memory or RAG.

---

## User Story

As a user, I want to ask the AI what I should focus on next so that I can make better planning decisions.

---

## Flow

```text
User opens chat panel
    ↓
User asks a question
    ↓
Frontend sends POST /api/chat
    ↓
Backend loads current tasks, recent journal entries, and blockers
    ↓
Backend injects this context into the prompt
    ↓
Backend sends request to EPAM Dial API
    ↓
AI returns contextual response
    ↓
Frontend displays the answer
```

---

## Example Questions

```text
What should I focus on tomorrow?
```

```text
Which tasks are currently blocked?
```

```text
Summarize my current priorities.
```

---

## Example Response

```text
You should focus on resolving the Dial API setup first because it blocks the AI extraction workflow. After that, prepare the demo presentation and continue the React setup.
```

---

## Success Criteria

* User can send a chat message.
* AI response uses current task/journal context.
* The response is practical and productivity-focused.

---

# Flow 5 — Review Journal History

## Purpose

The user can review past journal entries and track progress over time.

---

## User Story

As a user, I want to see previous journal entries so that I can review what I worked on and what blockers appeared.

---

## Flow

```text
User opens journal history
    ↓
Frontend sends GET /api/journal
    ↓
Backend returns previous journal entries
    ↓
Frontend displays entries chronologically
```

---

## Display Should Include

* journal text,
* created date,
* optional AI summary,
* optional extracted blockers/tasks.

---

## Success Criteria

* User can see previous journal entries.
* Entries are ordered by date.
* User can review progress and blockers.

---

# Flow 6 — Generate Productivity Summary

## Purpose

The user can ask the AI to summarize progress and blockers over a selected time period.

For MVP, this can be implemented using recent journal entries and current tasks without full RAG.

---

## User Story

As a user, I want to generate a weekly summary so that I can understand what I completed, what blocked me, and what to focus on next.

---

## Flow

```text
User requests summary
    ↓
Frontend sends request to backend
    ↓
Backend loads recent journal entries and tasks
    ↓
Backend builds summary prompt
    ↓
Backend sends request to EPAM Dial API
    ↓
AI returns productivity summary
    ↓
Frontend displays summary
```

---

## Example Questions

```text
What did I complete this week?
```

```text
Which blockers repeated most often this month?
```

```text
Give me a summary of my progress for the last 7 days.
```

---

## Expected Summary Content

* completed work,
* active blockers,
* repeated blockers,
* unfinished high-priority tasks,
* suggested next focus areas.

---

## Success Criteria

* User can request a productivity summary.
* AI uses available journal/task data.
* Summary is concise and actionable.

---

# Flow 7 — n8n Daily Journal Reminder

## Purpose

The n8n workflow supports the Week 1 low-code/no-code requirement and provides a practical reminder use case.

The workflow is separate from the main application runtime path.

---

## User Story

As a user, I want to be reminded to submit a daily journal entry so that my productivity history stays consistent.

---

## Flow

```text
Scheduled n8n trigger runs daily
    ↓
n8n calls GET /api/journal/has-entry-today
    ↓
IF hasEntryToday == false
    ↓
Send reminder notification or show execution log
    ↓
Log workflow execution
```

---

## Success Criteria

* n8n workflow runs on a schedule.
* n8n checks whether a journal entry exists today.
* n8n uses IF logic.
* Reminder/logging step is visible during demo.

---

# Out-of-Scope Flows

The following flows are intentionally excluded from MVP:

* user registration/login,
* multi-user collaboration,
* shared projects,
* role-based permissions,
* advanced Kanban board with drag-and-drop,
* autonomous AI task execution,
* full conversation memory,
* full RAG pipeline,
* production notifications infrastructure.

---

# MVP Flow Priorities

| Priority | Flow                                   |
| -------- | -------------------------------------- |
| P0       | Submit journal entry and extract tasks |
| P0       | Display AI extraction result           |
| P1       | View and manage tasks                  |
| P1       | n8n daily reminder workflow            |
| P2       | Context-aware AI chat                  |
| P2       | Journal history                        |
| P3       | Productivity summaries                 |
| P3       | Future RAG/memory flows                |

---

# Future Flow Enhancements

Possible future flows include:

* voice input for journal entries,
* semantic search across journal history,
* long-term productivity analytics,
* monthly and quarterly summaries,
* gamified task rewards,
* recurring blocker detection,
* project/category-level reporting.

These should only be implemented after the core MVP flows are stable.
