# AI Productivity Assistant

AI Productivity Assistant is a lightweight AI-powered productivity application that converts daily journal entries into structured tasks and summaries.

The project is built as a Proof of Concept (PoC) for the AI Upskilling Program and demonstrates:

- ASP.NET Core Web API
- React frontend
- SQLite persistence
- AI-ready architecture
- Low-code automation integration with n8n

---

# Current Features

## Implemented

- Journal entry submission
- SQLite database persistence
- REST API endpoints
- Swagger API documentation
- EF Core + SQLite setup
- n8n workflow automation
- task extraction workflow (mock AI response)
- SQLite workflow logging
- backend/frontend integration

## Planned

- EPAM Dial API integration
- Real AI extraction
- AI-generated summaries
- Blocker detection

---

# Tech Stack

| Layer           | Technology                        |
|-----------------|-----------------------------------|
| Frontend        | React + Vite                      |
| Backend         | ASP.NET Core Web API              |
| App metadata DB | SQLite (journal, summaries, logs) |
| ORM             | Entity Framework Core             |
| AI              | EPAM Dial API (planned)           |
| Task management | Todoist API                       |
| Automation      | n8n                               |

---

# Architecture

```text
React Frontend
       ↓
ASP.NET Core Web API
       ↓              ↓              ↓
EPAM Dial API   Todoist API    SQLite Database
(LLM Gateway)  (Tasks/Projects) (App metadata)
```

```text
n8n Automation Workflow
       ↓
Backend API Endpoints
```

---

# Project Structure

```text
ai-productivity-assistant/
│
├── frontend/         # React frontend
├── backend/          # ASP.NET Core Web API
├── specs/            # Lean project specifications
├── datasets/         # Sample/mock datasets
├── docs/             # Additional documentation
├── n8n/              # n8n workflows
├── README.md
└── .gitignore
```

---

# API Endpoints

## POST /api/journal

Creates a journal entry and returns mock AI extraction data.

### Example Request

```json
{
  "content": "Finished backend setup today."
}
```

---

## GET /api/tasks

Returns all extracted tasks.

---

## GET /api/journal/has-entry-today

Checks whether a journal entry exists for the current day.

### Example Response

```json
{
  "hasEntryToday": true
}
```

## GET /api/automation/logs

Returns workflow execution logs.

## POST /api/automation/logs

Stores workflow execution events from n8n.

---

# Running the Project

## Backend

```bash
cd backend
dotnet restore
dotnet run
```

Backend runs with Swagger enabled.

---

## Frontend

```bash
cd frontend
npm install
npm run dev
```

---

# Database

The project uses SQLite with Entity Framework Core for app-specific metadata: journal entries, AI summaries, extracted blockers, and automation logs.

Database file:

```text
backend/app.db
```

Tasks and projects are not stored in SQLite. They are managed via the Todoist API.

---

# Automation Logging

The project includes workflow execution logging from n8n into SQLite.

Automation events are stored using:
- workflow name
- event type
- execution message
- timestamp

Example events:
- JournalMissing
- JournalExists

---

# Current AI Implementation

The project currently uses a mocked AI extraction response.

Planned next step:
- integrate EPAM Dial API for real LLM-powered extraction.

---

# Planned Next Steps

## Week 2

- Integrate EPAM Dial API
- Replace mock extraction with real AI processing
- Add task prioritization and status updates via Todoist API
- Introduce project/task grouping via Todoist projects
- Improve frontend task management UI

## Week 3

- AI-generated daily summaries
- Weekly and monthly productivity summaries
- Voice-to-text journal input
- Expanded n8n automation workflows
- Smarter personalized reminders

---

# Example Workflow

1. User writes a journal entry.
2. Frontend sends request to backend.
3. Backend processes journal entry.
4. Mock AI extraction generates:
   - tasks
   - priorities
   - blockers
   - summary
5. Journal entry and extracted blockers are saved to SQLite.
   Extracted tasks are created in Todoist via the Todoist API.
6. Frontend displays extracted tasks.

---

# Project Scope

This project is intentionally minimal and focused on:

- AI-assisted productivity workflows
- structured data extraction
- lightweight PoC architecture
- rapid prototyping

The project does not currently include:

- authentication
- multi-user support
- advanced UI
- production deployment

---

# Author

Viktoryia Kudrashova

AI Upskilling Program  
May 2026
