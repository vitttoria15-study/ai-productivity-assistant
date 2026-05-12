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

---

## Main Deliverables

### 1. Project Setup

* Initialize repository structure
* Initialize ASP.NET Core Web API
* Initialize React frontend
* Configure SQLite
* Configure EPAM Dial API access
* Prepare basic documentation

---

### 2. Minimal AI Workflow

The user should be able to:

* submit a journal entry,
* send the text to the AI,
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

Simple UI only:

* journal input field,
* submit button,
* AI response panel,
* simple task list.

No advanced styling required for Week 1.

---

### 4. n8n Workflow

Implement a simple scheduled reminder workflow.

Example:

```text
Schedule Trigger
→ Reminder Message
→ IF Condition
→ Notification / Execution Log
```

Purpose:

* demonstrate low-code/no-code automation,
* demonstrate triggers,
* demonstrate conditional logic,
* satisfy Week 1 course requirement.

---

## May 15 Demo Scope

### Must Work

* journal submission,
* AI extraction,
* visible structured output,
* basic React UI,
* simple n8n workflow.

---

### Not Required

* polished UI,
* advanced CRUD,
* long-term memory,
* RAG,
* production infrastructure,
* authentication,
* advanced architecture.

---

# Week 2 — Core Productivity Features

## Goal

Transform the prototype into a usable productivity assistant.

---

## Main Deliverables

### 1. Task Management

The user should be able to:

* create tasks,
* edit tasks,
* delete tasks,
* mark tasks as completed,
* organize tasks by priority/status.

---

### 2. Context-Aware AI Chat

The AI assistant should answer questions using:

* current tasks,
* latest journal entries,
* active blockers.

Example:

```text
"What should I focus on tomorrow?"
```

The MVP will use lightweight context injection into prompts instead of full RAG.

---

### 3. Journal History

The user should be able to:

* view previous journal entries,
* review progress history,
* track recurring blockers.

---

### 4. Better UX

Improve:

* loading states,
* error handling,
* layout consistency,
* task organization.

---

# Week 3 — Polish + AI Enhancements

## Goal

Prepare a stable and convincing final demo.

---

## Main Deliverables

### 1. Demo-Ready Experience

* stable end-to-end flows,
* reliable AI responses,
* prepared demo data,
* consistent UI behavior.

---

### 2. AI Productivity Summaries

Generate:

* weekly summaries,
* blocker overviews,
* productivity insights,
* accomplishment summaries.

Examples:

* “What did I complete this week?”
* “Which blockers repeated most often this month?”

---

### 3. Optional AI Enhancements

Possible stretch goals:

* lightweight RAG,
* semantic search,
* voice input,
* gamification,
* productivity analytics,
* long-term trend summaries.

Only implement stretch goals if the core MVP is stable.

---

# MVP Priorities

| Priority | Feature                   |
| -------- | ------------------------- |
| P0       | Journal → AI extraction   |
| P0       | EPAM Dial API integration |
| P1       | Task management           |
| P1       | Basic React UI            |
| P1       | n8n workflow              |
| P2       | Context-aware AI chat     |
| P2       | Journal history           |
| P3       | Weekly/monthly summaries  |
| P3       | Lightweight RAG           |
| P3       | Gamification              |

---

# Development Strategy

The project prioritizes:

* clean MVP implementation,
* visible AI functionality,
* realistic implementation scope,
* demo-ready workflows,
* incremental development.

The project intentionally avoids:

* enterprise-level complexity,
* overengineering,
* advanced infrastructure,
* unnecessary architectural layers,
* full Jira-like functionality.
