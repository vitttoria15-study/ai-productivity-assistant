# Demo Plan

## Overview

This document describes the planned demo strategy for the AI Productivity Assistant PoC.

The project includes:

* weekly progress demos,
* an intermediate prototype demo,
* a final PoC presentation.

The demo strategy focuses on:

* visible AI functionality,
* incremental progress,
* realistic MVP delivery,
* practical productivity workflows.

The demos intentionally prioritize:

* working flows,
* AI integration,
* meaningful progress,
* clear product direction.

The demos do NOT prioritize:

* polished UI,
* enterprise-level architecture,
* production deployment,
* feature completeness.

---

# Demo Timeline

| Demo       | Date    | Goal                               |
| ---------- | ------- | ---------------------------------- |
| Demo 1     | May 15  | Minimal working AI prototype       |
| Demo 2     | ~May 22 | Usable productivity assistant      |
| Final Demo | May 29  | Stable AI-powered productivity PoC |

---

# Demo 1 — May 15

## Goal

Demonstrate a minimal end-to-end AI workflow.

The focus of Demo 1 is proving that:

* the architecture is working,
* AI integration is functional,
* the project direction is clear,
* the MVP scope is realistic.

---

# Required Deliverables

## 1. Repository & Architecture

Show:

* GitHub repository,
* project structure,
* documentation/specifications,
* high-level architecture.

---

## 2. Backend Initialization

Demonstrate:

* ASP.NET Core Web API initialized,
* SQLite configured,
* EPAM Dial API integration prepared or working.

---

## 3. Minimal React UI

The UI may remain simple.

Required screens/components:

* journal input textarea,
* submit button,
* AI extraction result display,
* simple task list.

Polished styling is NOT required.

---

## 4. AI Extraction Workflow

This is the most important Demo 1 feature.

### Demo Flow

User submits:

```text
Today I finished onboarding, but backend authentication is still blocked.
Tomorrow I need to complete the project presentation.
```

Backend:

* sends prompt to EPAM Dial API,
* receives structured JSON,
* stores extracted information,
* returns result to UI.

Example output:

```json
{
  "completed_tasks": [
    "Finished onboarding"
  ],
  "blockers": [
    "Backend authentication issue"
  ],
  "new_tasks": [
    {
      "title": "Complete project presentation",
      "priority": "high"
    }
  ]
}
```

---

## 5. n8n Workflow Demo

A separate low-code/no-code workflow must be demonstrated.

### Planned Workflow

```text
Schedule Trigger
→ GET /api/journal/has-entry-today
→ IF hasEntryToday == false
→ Reminder Notification / Execution Log
```

Purpose:

* demonstrate automation,
* demonstrate conditional logic,
* satisfy Week 1 course requirement.

The n8n workflow remains independent from the main runtime path.

---

# Demo 1 Scope Limitations

The following are NOT required for May 15:

* polished UI,
* full CRUD functionality,
* RAG,
* vector databases,
* long-term memory,
* authentication,
* advanced frontend architecture,
* production deployment,
* advanced AI orchestration.

---

# Suggested Demo 1 Flow (5 Minutes)

| Time  | Topic                            |
| ----- | -------------------------------- |
| 1 min | Project overview + architecture  |
| 1 min | Repository + tech stack          |
| 2 min | Journal → AI extraction workflow |
| 1 min | n8n workflow demonstration       |

---

# Demo 2 — Intermediate Progress (~May 22)

## Goal

Transform the prototype into a usable productivity assistant.

---

# Planned Features

## Task Management

Demonstrate:

* create task,
* edit task,
* delete task,
* mark task completed,
* task prioritization.

---

## Context-Aware AI Chat

Demonstrate contextual AI responses using:

* current tasks,
* blockers,
* recent journal entries.

Example:

```text
What should I focus on tomorrow?
```

---

## Journal History

Demonstrate:

* previous entries,
* progress tracking,
* recurring blockers.

---

## Improved UX

Show:

* loading states,
* better organization,
* improved layout,
* basic error handling.

---

# Suggested Demo 2 Flow

| Time  | Topic                             |
| ----- | --------------------------------- |
| 1 min | Progress since Demo 1             |
| 2 min | Task management                   |
| 1 min | AI chat                           |
| 1 min | Journal history + UX improvements |

---

# Final Demo — May 29

## Goal

Present a stable and convincing AI-powered productivity assistant PoC.

The focus is:

* end-to-end functionality,
* AI-assisted workflows,
* practical productivity use cases,
* demo stability.

---

# Planned Final Demo Features

## Core Features

* journal entry submission,
* AI task extraction,
* task management,
* contextual AI chat,
* journal history,
* productivity summaries.

---

## AI Productivity Insights

Examples:

* weekly summaries,
* blocker analysis,
* recurring task detection,
* accomplishment overview.

Example:

```text
"What blockers repeated most often this month?"
```

---

## Optional Stretch Features

Possible stretch goals:

* lightweight RAG,
* semantic search,
* voice input,
* gamification,
* productivity analytics.

Stretch goals should only be implemented if the MVP is stable.

---

# Suggested Final Demo Flow (5 Minutes)

| Time  | Topic                                |
| ----- | ------------------------------------ |
| 1 min | Project overview + problem statement |
| 1 min | Architecture + tech stack            |
| 2 min | End-to-end AI workflow demonstration |
| 1 min | AI summaries + future enhancements   |

---

# Demo Strategy Principles

The project demos prioritize:

* stable working flows,
* visible AI functionality,
* realistic scope,
* incremental progress,
* demo reliability.

The demos intentionally avoid:

* overengineering,
* feature overload,
* unnecessary complexity,
* unstable experimental functionality.

---

# Demo Preparation Recommendations

Before each demo:

* prepare demo data,
* pre-seed several journal entries,
* test AI prompts,
* verify API connectivity,
* verify SQLite persistence,
* verify n8n workflow execution,
* prepare fallback screenshots in case of API/network issues.

---

# Success Criteria

The project demo is considered successful if:

* AI extraction works end-to-end,
* users can manage tasks,
* AI-generated summaries are visible,
* contextual AI responses work,
* the architecture is understandable,
* the MVP scope remains realistic and stable.
