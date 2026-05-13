# AI Productivity Assistant

AI Productivity Assistant is an AI-powered personal productivity and planning application designed to help users manage tasks, projects, priorities, and progress using natural-language interaction.

The application combines:

* task management,
* conversational AI,
* AI-based summarization,
* smart prioritization,
* productivity tracking.

The goal of the project is to demonstrate practical integration of Large Language Models (LLMs) into a real-world productivity workflow.

---

# Features

## Core Features (MVP)

* Project and task management
* Task prioritization
* Journal/progress logging
* Conversational AI assistant
* AI-based task extraction
* AI-generated summaries
* AI blocker detection
* Natural-language task updates

---

## Planned AI Capabilities

The AI assistant will be able to:

* extract structured tasks from natural-language input,
* identify blockers and priorities,
* generate daily/weekly summaries,
* recommend next focus areas,
* analyze productivity patterns.

Example:

Input:

```text id="jvdgw3"
Today I finished onboarding, but backend authentication is still blocked.
Tomorrow I need to complete the project presentation.
```

Expected AI Output:

```json id="90n4x8"
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

# Tech Stack

| Layer          | Technology           |
| -------------- | -------------------- |
| Frontend       | React                |
| Backend        | ASP.NET Core Web API |
| Database       | SQLite               |
| AI Integration | EPAM Dial API        |
| Automation     | n8n                  |

---

# Architecture Overview

```text id="qgsv3q"
React Frontend
       ↓
ASP.NET Core Web API
       ↓
EPAM Dial API (LLM)
       ↓
SQLite Database
```

---

# Planned Project Structure

```text id="0bsq8v"
ai-productivity-assistant/
│
├── frontend/         # React frontend
├── backend/          # ASP.NET Core Web API
├── docs/             # Specifications and documentation
├── n8n/              # n8n workflows
├── datasets/         # Sample/mock datasets
├── README.md
└── .gitignore
```

---

# Planned User Flow

1. User writes a journal/progress update.
2. Backend sends the text to the LLM.
3. AI extracts:

   * completed tasks,
   * blockers,
   * priorities,
   * new tasks.
4. Data is stored in SQLite.
5. Frontend displays organized tasks and AI-generated insights.

---

# Example Use Cases

* Daily progress tracking
* Personal productivity management
* Learning/study planning
* Work task organization
* AI-assisted prioritization
* Weekly/monthly productivity summaries

---

# Optional Future Enhancements

## AI Memory / Lightweight RAG

Possible future features:

* semantic search across historical journal entries,
* recurring blocker detection,
* long-term productivity insights,
* AI-generated weekly/monthly/quarterly reports.

Potential future technologies:

* vector embeddings,
* semantic retrieval,
* lightweight RAG architecture.

---

# n8n Workflow

As part of the Week 1 requirements, the project includes a low-code/no-code automation workflow built with n8n.

The workflow demonstrates:

* triggers,
* API integrations,
* conditional logic,
* notifications/logging.

Example flow:

```text id="k9zybh"
Schedule Trigger
→ HTTP Request
→ IF Condition
→ Notification
→ Logging
```

---

# Current Project Status

## Week 1 Goals

* [x] Project idea defined
* [x] Repository initialized
* [x] Environment setup
* [ ] EPAM Dial API integration
* [ ] Initial AI prototype
* [ ] n8n workflow
* [x] React frontend skeleton
* [x] SQLite integration

---

# Project Scope

This project is a Proof of Concept (PoC) focused on demonstrating:

* AI-assisted workflows,
* conversational task management,
* structured data extraction,
* productivity intelligence.

The project intentionally avoids:

* enterprise-level complexity,
* multi-user collaboration,
* advanced infrastructure,
* production-scale deployment.

---

# Author
Viktoryia Kudrashova

AI Upskilling Program Project
May 2026
