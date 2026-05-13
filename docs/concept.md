# Concept

## Project Vision

AI Productivity Assistant is a lightweight AI-powered productivity companion designed to help users organize tasks, priorities, blockers, and progress through natural-language interaction.

The application focuses on reducing mental overload by allowing users to write free-form journal or progress updates while the AI extracts structured information and helps organize work.

The project is designed as a practical Proof of Concept (PoC) demonstrating how Large Language Models (LLMs) can support everyday productivity workflows.

---

# Problem Statement

People often manage tasks, learning goals, work items, reminders, and personal plans across multiple disconnected places:

* notes,
* chats,
* sticky notes,
* spreadsheets,
* mental checklists.

This creates several problems:

* tasks become difficult to prioritize,
* blockers are forgotten,
* progress is hard to track,
* context switching increases cognitive load,
* journal notes remain unstructured and difficult to revisit.

Traditional task management tools require users to manually structure and maintain information.

The goal of this project is to explore how AI can simplify productivity management by transforming natural-language journal entries into structured and actionable information.

---

# Proposed Solution

The application allows users to write free-form journal or progress updates.

Example:

```text
Today I completed onboarding, but backend authentication is still blocked.
Tomorrow I need to finish the project presentation.
```

The AI assistant analyzes the text and extracts:

* completed tasks,
* blockers,
* new tasks,
* priorities,
* summaries.

The extracted information is then organized into a lightweight productivity dashboard.

The system also supports contextual AI interactions, allowing users to ask questions such as:

* “What should I focus on tomorrow?”
* “What blockers are currently active?”
* “Summarize my progress this week.”

---

# Target Users

The MVP is designed primarily for:

* developers,
* students,
* learners,
* individual contributors,
* knowledge workers.

The application is intentionally designed as a single-user productivity assistant.

---

# MVP Scope

The MVP focuses on journal-driven productivity assistance.

The core MVP features include:

## Journal Input

Users can submit natural-language journal/progress entries.

---

## AI Extraction

The AI extracts structured information from journal entries:

* completed tasks,
* blockers,
* new tasks,
* priorities,
* summaries.

---

## Task Management

Users can:

* view tasks,
* manually create tasks,
* edit tasks,
* delete tasks,
* mark tasks as completed.

---

## Context-Aware AI Chat

The AI assistant can answer lightweight contextual questions using:

* current tasks,
* recent journal entries,
* blockers,
* priorities.

The MVP uses lightweight prompt context injection instead of full RAG.

---

## Productivity Summaries

The system can generate:

* daily summaries,
* weekly summaries,
* blocker overviews,
* progress insights.

---

# Technical Direction

The planned technology stack:

| Layer          | Technology           |
| -------------- | -------------------- |
| Frontend       | React                |
| Backend        | ASP.NET Core Web API |
| Database       | SQLite               |
| AI Integration | EPAM Dial API        |
| Automation     | n8n                  |

---

# Architecture Overview

```text
React Frontend
    ↓
ASP.NET Core Web API
    ↓
EPAM Dial API (LLM)
    ↓
SQLite Database
```

n8n is used separately for low-code automation workflows and reminder flows.

---

# Out of Scope

The MVP intentionally excludes:

* authentication,
* multi-user collaboration,
* enterprise planning features,
* advanced Kanban boards,
* autonomous AI agents,
* complex orchestration systems,
* production-scale infrastructure,
* full Retrieval-Augmented Generation (RAG) pipelines.

The goal is to maintain a realistic and achievable MVP scope for a 3-week PoC project.

---

# Future Enhancements

Possible future enhancements include:

* lightweight RAG/memory,
* semantic search across journal history,
* recurring blocker analysis,
* long-term productivity analytics,
* voice input,
* gamification,
* productivity scoring,
* habit tracking.

These features are considered optional post-MVP improvements.

---

# Project Goals

The project aims to demonstrate:

* practical LLM integration,
* AI-assisted productivity workflows,
* structured information extraction,
* contextual AI assistance,
* lightweight AI-powered user experiences.

The project prioritizes:

* simplicity,
* clean architecture,
* realistic implementation scope,
* demo-ready functionality,
* incremental delivery.
