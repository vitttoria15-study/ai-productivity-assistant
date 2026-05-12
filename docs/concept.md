# Concept

## Project Vision

AI Productivity Assistant is a single-user, AI-powered productivity tool that turns a developer's free-form daily notes into structured, actionable tasks. The core premise: instead of maintaining a task list by hand, the user writes a journal update and the AI does the extraction.

The project is a 3-week Proof of Concept (PoC) built as part of the EPAM AI Upskilling Program (May 2026). It demonstrates practical LLM integration in a real productivity workflow using EPAM Dial API as the AI gateway.

---

## Problem Statement

Knowledge workers write daily progress notes, stand-up updates, or end-of-day reflections in free-form text. Extracting structured tasks, identifying blockers, and tracking priorities from that text is repetitive, error-prone, and often skipped entirely. The result: lost context, poor follow-through, and no record of what was accomplished.

The insight: this extraction is exactly what LLMs are good at. A well-prompted model can reliably parse "Today I finished onboarding, but auth is blocked; tomorrow I need to prep the presentation" into a machine-readable structure with zero manual tagging.

---

## User Persona

**Solo developer / knowledge worker (single user)**

- Works on several tasks simultaneously across multiple contexts
- Writes informal daily progress notes but doesn't always process them into a task list
- Wants clarity on what's done, what's blocked, and what needs attention next
- Needs a tool lightweight enough to use without discipline or setup overhead
- Does not want to learn another complex productivity system

---

## MVP Scope

The MVP delivers these capabilities end-to-end, in priority order:

| Priority | Capability |
|----------|------------|
| P0 | Journal entry form → AI extraction (completed tasks, blockers, new tasks, summary) |
| P0 | EPAM Dial API integration for all AI features |
| P1 | Task list: view, create, edit, mark done, delete |
| P1 | n8n scheduled reminder workflow (Week 1 program requirement) |
| P2 | Basic chat assistant with task + journal context awareness |
| P3 | Journal history (view past entries) |

The May 15 intermediate demo requires only P0: working journal submission + AI extraction result displayed in the UI.

---

## Out of Scope (MVP)

- User authentication, login, or sessions
- Multi-user or team collaboration
- Role-based access control (RBAC) or tenancy
- Cloud hosting or production deployment
- Real-time updates / WebSockets
- Mobile-responsive design
- Integrations with external tools (Jira, Notion, GitHub, Slack)
- Email delivery (n8n notification can use execution log or webhook)
- Advanced analytics, dashboards, or charts
- Task dependencies or project hierarchy

---

## Planned Future Enhancements

### Lightweight RAG / AI Memory

When built, this adds semantic search over historical journal entries:

- Recurring blocker detection ("this issue appeared 3 times this month")
- Long-term productivity pattern analysis
- AI-generated weekly/monthly summaries from historical entries
- Context injection: top-k similar past entries included in chat and extraction prompts

Potential implementation path: SQLite with `sqlite-vss` extension, or an embedded vector store such as Chroma or FAISS. No architecture change to the API or frontend is required — only the backend's `DialService` and storage layer expand.

### Smart Prioritization

AI recommends which task to focus on next, factoring in deadline signals, recurrence, and blocker history.

### Summary Reports

Automated weekly/monthly summaries generated from the journal corpus, surfaced as a dedicated view.
