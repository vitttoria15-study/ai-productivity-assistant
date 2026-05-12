# Demo Plan

---

## May 15 — Intermediate Demo (Minimal Prototype)

**Format:** Progress check-in + live working prototype (or Postman/Swagger fallback)

**Goal:** Show the core AI extraction pipeline is working end-to-end — the most important technical risk is de-risked by demonstrating the EPAM Dial integration.

### What to Show

1. **Architecture overview** (1 min)
   - Show the flow diagram from `docs/architecture.md`
   - State: React → ASP.NET Core → EPAM Dial API → SQLite

2. **Live demo: Journal → AI Extraction** (3–4 min)
   - Open React app in browser
   - Paste a sample entry from `datasets/sample-journal-entries.json`:
     > "Today I finished onboarding, but backend authentication is still blocked. Tomorrow I need to complete the project presentation."
   - Click "Analyze"
   - Show loading state
   - Show structured extraction result: completed tasks, blockers, new tasks + priority, summary

3. **Show the data was saved** (1 min)
   - Call `GET /api/journal/1` via Swagger UI or Postman
   - Show the stored entry + extraction JSON in the response

4. **Status update** (1 min)
   - What's done: backend, Dial integration, SQLite, basic React UI
   - What's next: task CRUD, chat, n8n, UI polish

### Minimum Required to Demo

- [ ] `POST /api/journal` returning 201 with extraction result
- [ ] React journal form submitting and displaying the extraction
- [ ] EPAM Dial API returning valid structured JSON

### Fallback Plan

If the React frontend is not ready:
- Demo via **Swagger UI** (`http://localhost:5000/swagger`) or **Postman**
- Show raw JSON response — emphasizes the API contract and Dial integration work
- State clearly: "Frontend polish is Week 2 — the pipeline is proven"

---

## Final Demo — Week 3 End (~May 30)

**Format:** Product demo + architecture walkthrough (15–20 minutes)

**Audience:** EPAM AI Upskilling Program reviewers

---

### Suggested Presentation Flow

#### 1. Problem Statement (2 min)

> "Developers write daily progress notes — stand-up updates, end-of-day logs — but rarely extract structured tasks from them. This tool does it automatically using an LLM."

Show the "before": a block of free-form text.  
Show the "after": a structured task list with priorities and a summary.

---

#### 2. Live Demo — Journal Flow (4 min)

Use the sample entry from `datasets/sample-journal-entries.json`, or write something realistic live.

Steps:
1. Open the app on the **Journal** tab
2. Paste / type a progress update
3. Click "Analyze" — show loading state
4. Show the extraction result panel:
   - **Summary:** plain-language overview
   - **Completed tasks:** what was finished
   - **Blockers:** impediments detected
   - **New tasks:** with priority labels (HIGH / MEDIUM / LOW)
5. Point out: "The new tasks were automatically added to the task list."

---

#### 3. Live Demo — Task Management (3 min)

Switch to the **Tasks** tab.

1. Show tasks extracted from the journal entry
2. Add a manual task: "Review PR #42" → priority: Medium
3. Mark "Finished onboarding" as done (checkbox) — show strikethrough
4. Edit "Complete project presentation" → change priority to HIGH
5. Delete the manual task

---

#### 4. Live Demo — Chat Assistant (3 min)

Switch to the **Chat** tab.

Suggested questions (use actual data stored in the app):
- "What should I focus on today?"
- "What are my current blockers?"
- "Summarize my progress this week."

Narrate: "The AI has access to my current task list and latest journal entry — it's not a generic chatbot, it's context-aware."

---

#### 5. n8n Workflow Demo (2 min)

Open n8n canvas.

1. Show the workflow visually: Cron → HTTP Request → IF → Notification
2. Temporarily set `has_entry` to return `false` (or delete today's entry via Postman)
3. Click "Execute workflow" (manual trigger)
4. Show the IF node branching to the notification path
5. Show execution log: "Reminder sent"
6. Explain: "This runs automatically at 9 AM daily to nudge the user if they haven't journaled yet."

---

#### 6. Architecture Overview (2 min)

Show the diagram from `docs/architecture.md`.

Key points:
- Single-responsibility layers: React only renders, ASP.NET Core orchestrates, DialService is the only AI touchpoint
- SQLite: file-based, zero infrastructure, good for PoC
- n8n is decoupled — calls the API endpoint, not wired into the app runtime

---

#### 7. Future Direction (1 min)

> "The natural next step is lightweight RAG: storing embeddings of journal entries so the AI can reference past context. That unlocks recurring blocker detection, weekly summaries, and long-term productivity insight — all additive, no breaking changes to the current architecture."

---

### Demo Data

Prepare 2–3 varied entries in the database before the demo:

| Entry | What it demonstrates |
|-------|---------------------|
| "Today I finished onboarding, but backend auth is blocked. Tomorrow: present the project." | Full extraction: completed + blocker + new task |
| "Fixed the login bug. No blockers. Need to write unit tests and update the README." | Multiple new tasks; no blockers |
| "Slow day — spent most of it in meetings. Still haven't fixed the performance regression." | Blocker-heavy; no completions; demonstrates edge case handling |

Pre-seed via `POST /api/journal` before starting the demo, or use a seed script.

---

### Demo Risk Mitigations

| Risk | Mitigation |
|------|------------|
| EPAM Dial API slow or unavailable | Prepare a pre-recorded screen recording of the extraction as backup; show it as "this is what the live version produces" |
| n8n not installed locally | Use [n8n cloud free tier](https://app.n8n.cloud) or show a pre-recorded execution log screenshot |
| Database empty after restart | Pre-seed with `datasets/sample-journal-entries.json` before demo |
| Frontend UI unpolished | Frame it: "This is a PoC focused on the AI pipeline — UI polish is deferred." Functional over beautiful. |
| AI returns wrong priorities | Use the priority mapping from `api-spec.md` to tune the system prompt before demo day |
