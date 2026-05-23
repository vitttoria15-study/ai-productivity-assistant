# Roadmap

## Timeline Overview

| Week   | Dates     | Focus                      | Demo                  |
| ------ | --------- | -------------------------- | --------------------- |
| Week 1 | May 7–15  | Foundation + First AI Flow | May 15 demo           |
| Week 2 | May 16–22 | Core Productivity Features | Intermediate progress |
| Week 3 | May 23–29 | Polish + AI Enhancements   | Final demo            |

---

# Week 1 — Foundation + First AI Flow

## Goal

Show a minimal working AI-powered productivity assistant.

The primary objective of Week 1 is to demonstrate:

* working project structure,
* AI integration,
* a minimal end-to-end workflow,
* a realistic MVP direction.

---

## Main Deliverables

### 1. Project Setup

* Initialize repository structure
* Initialize ASP.NET Core Web API
* Initialize React frontend
* Configure SQLite
* Configure EPAM Dial API access
* Prepare documentation/specifications

---

### 2. Minimal AI Workflow

The user should be able to:

* submit a journal entry,
* send the entry to the AI,
* receive structured AI extraction results,
* view extracted tasks/blockers in the UI.

Example flow:

```text
Journal Entry
→ ASP.NET API
→ EPAM Dial API
→ Structured JSON
→ SQLite
→ React UI
```

---

### 3. Basic UI

Simple MVP UI only:

* journal input field,
* submit button,
* AI extraction result panel,
* simple task list.

No advanced styling is required for Week 1.

---

### 4. n8n Workflow

Implement a simple scheduled reminder workflow.

Example:

```text
Schedule Trigger
→ GET /api/journal/has-entry-today
→ IF hasEntryToday == false
→ Reminder Notification / Execution Log
```

Purpose:

* demonstrate low-code/no-code automation,
* demonstrate triggers,
* demonstrate conditional logic,
* satisfy Week 1 course requirement.

The n8n workflow should remain separate from the core application runtime path.

---

# May 15 Demo Scope

## Must Work

* journal submission,
* AI extraction,
* visible structured output,
* basic React UI,
* SQLite persistence,
* simple n8n workflow.

---

## Not Required

* polished UI,
* advanced CRUD,
* RAG,
* vector databases,
* long-term memory,
* authentication,
* production infrastructure,
* advanced frontend architecture.

---

# Week 2 — Core Productivity Features

## Goal

Transform the prototype into a usable AI productivity assistant.

The focus of Week 2 is usability and contextual AI behavior.

---

## Main Deliverables

### 1. Task Management (via Todoist API)

Task management is implemented through the Todoist API. The backend acts as a proxy between the frontend and Todoist.

The user should be able to:

* create tasks in Todoist,
* edit tasks in Todoist,
* delete tasks in Todoist,
* mark tasks as completed in Todoist,
* organize tasks by priority/status as returned by Todoist.

---

### 2. Context-Aware AI Chat

The AI assistant should answer questions using:

* current tasks,
* recent journal entries,
* active blockers,
* current priorities.

Example:

```text
What should I focus on tomorrow?
```

The MVP should use lightweight prompt context injection instead of full RAG.

---

### 3. Journal History

The user should be able to:

* view previous journal entries,
* review progress history,
* identify recurring blockers.

---

### 4. Better UX

Improve:

* loading states,
* error handling,
* layout consistency,
* task organization,
* basic responsiveness.

---

# Week 3 — Polish + AI Enhancements

## Goal

Prepare a stable and convincing final demo.

The focus of Week 3 is:

* demo stability,
* AI experience,
* polish,
* productivity insights.

---

## Main Deliverables

### 1. Demo-Ready Experience

* stable end-to-end flows,
* reliable AI responses,
* prepared demo data,
* consistent UI behavior,
* improved demo presentation.

---

### 2. AI Productivity Summaries

Generate:

* daily summaries,
* weekly summaries,
* blocker overviews,
* accomplishment summaries,
* productivity insights.

Example questions:

```text
What did I complete this week?
```

```text
Which blockers repeated most often this month?
```

---

### 3. Optional AI Enhancements

Possible stretch goals:

* lightweight RAG,
* semantic search,
* voice input,
* gamification,
* productivity analytics,
* long-term trend summaries.

Stretch goals should only be implemented if the core MVP is stable.

---

# MVP Priorities

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

---

# Development Strategy

The project prioritizes:

* clean MVP implementation,
* visible AI functionality,
* realistic implementation scope,
* stable demo workflows,
* incremental development.

The project intentionally avoids:

* enterprise-level complexity,
* overengineering,
* unnecessary architectural layers,
* premature optimization,
* full Jira-like functionality.

---

# Risk Management

| Risk                       | Mitigation                                    |
| -------------------------- | --------------------------------------------- |
| EPAM Dial API setup delays | Mock extraction responses temporarily         |
| AI malformed JSON          | Add response validation and fallback handling |
| Limited development time   | Prioritize P0/P1 only                         |
| Frontend complexity        | Keep UI intentionally minimal                 |
| Scope creep                | Defer stretch goals until MVP is stable       |
| Demo instability           | Prepare fallback screenshots/demo data        |

---

# Success Criteria

The project is considered successful if:

* AI extraction works end-to-end,
* users can manage tasks,
* contextual AI responses work,
* summaries are visible,
* the architecture remains understandable,
* the MVP remains realistic and demo-ready.
