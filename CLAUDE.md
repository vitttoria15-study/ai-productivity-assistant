# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository status

This is a Proof of Concept in early scaffolding — the `backend/` and `frontend/` directories exist but are **empty**, and most files under `docs/` are single-line placeholders. There is no build/lint/test tooling yet. When asked to "run typecheck", "build", or "run tests", verify the relevant project has actually been initialized before attempting commands; if not, surface that to the user instead of inventing tooling.

The authoritative description of intent (features, planned flow, scope boundaries) lives in `README.md`. Treat `docs/*.md` as TODO stubs to be filled in as work progresses, not as specs.

## Planned architecture

The flow described in `README.md` is:

```
React frontend → ASP.NET Core Web API → EPAM Dial API (LLM) → SQLite
```

Key constraints to respect when implementing:

- **EPAM Dial API** is the LLM gateway (not OpenAI/Anthropic directly). Backend code should call Dial, not other providers.
- **SQLite** is the database — keep schema and queries portable to that engine.
- **Single-user PoC**: the README explicitly excludes multi-user collaboration, enterprise complexity, and production-scale infrastructure. Don't add auth/tenancy/RBAC layers unless asked.
- **n8n** under `n8n/workflows/` is for a separate low-code automation deliverable (Week 1 requirement), not the main app's runtime path.

## Core domain shape

The AI's job is to turn free-form journal text into structured fields: `completed_tasks`, `blockers`, `new_tasks` (with `title` + `priority`). See the example in `README.md`. Any prompt/schema work should produce that shape so the frontend and DB layer can rely on it.

Sample inputs live in `datasets/sample-journal-entries.json` and `datasets/sample-tasks.json` — use these as fixtures rather than fabricating new test data.

## When initializing a sub-project

Once `backend/` (ASP.NET Core Web API) or `frontend/` (React) is created, add the actual build/test/run commands to this file. Until then, do not list commands here that don't work.
