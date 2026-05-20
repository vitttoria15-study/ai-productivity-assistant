# Project Constitution

## Project

AI Productivity Assistant is a PoC application for AI-assisted productivity workflows.

The app converts journal/progress updates into structured tasks, priorities, blockers, and summaries.

## Tech Stack

- Frontend: React + Vite
- Backend: ASP.NET Core Web API
- Database: SQLite
- Automation: n8n
- AI Providers: EPAM Dial and Ollama
- External Task Storage: Todoist API

## Architecture Rules

- React frontend must communicate only with ASP.NET Core backend.
- Frontend must not call Todoist API directly.
- Frontend must not call AI providers directly.
- Todoist API token must be stored only in backend configuration.
- AI provider credentials must be stored only in backend configuration.
- Backend is responsible for orchestration between Todoist, AI providers, SQLite, and n8n.

## Spec-Driven Development Rules

- New features should start with a spec.
- Specs should define user flow, API contract, data model impact, and acceptance criteria.
- Implementation should follow existing specs.
- Do not introduce large architectural changes without updating specs.

## AI Integration Rules

- AI extraction must return structured JSON.
- Mock AI provider should remain available as fallback.
- Ollama should be supported as local fallback provider.
- EPAM Dial should be supported as primary enterprise provider.
- AI responses must be validated before saving data.

## Todoist Integration Rules

- Todoist is used as external task/project storage.
- Backend should provide simplified internal endpoints for frontend.
- Todoist-specific details should not leak into frontend UI logic.
- Backend may keep local SQLite logs and automation history.

## n8n Rules

- n8n is used for automation workflows.
- n8n should call backend API endpoints, not database directly.
- Workflow executions and important events should be logged through backend API.

## Quality Rules

- Keep PoC implementation minimal and understandable.
- Prefer small, focused changes.
- Avoid overengineering.
- Do not add authentication unless explicitly planned.
- Do not expose secrets in repository.


**Version**: 1.0.0 | **Ratified**: 2026-05-20 | **Last Amended**: 2026-05-20
<!-- Example: Version: 2.1.1 | Ratified: 2025-06-13 | Last Amended: 2025-07-16 -->
