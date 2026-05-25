# Spec: Todoist Projects Dashboard

**Slug:** todoist-projects-dashboard  
**Branch:** feature/todoist-projects-dashboard  
**Date:** 2026-05-25  

---

## Goal

Extend the AI Productivity Assistant with a polished, demo-ready dashboard shell and a fully functional Todoist Projects & Tasks panel. The Todoist integration is the primary demo feature and should be visually dominant. Secondary dashboard sections (AI Assistant, Daily Summary, Recent Activity) are implemented as realistic-looking placeholder cards, ready to be wired in future tasks.

---

## Scope

### Fully Implemented
- Dashboard shell layout (sidebar + main content grid)
- Sidebar navigation (app branding, nav links, Todoist status badge, quick actions)
- `GET /api/projects` backend endpoint
- `?projectId=` filter on `GET /api/tasks`
- Projects & Tasks panel: project list, task list, All Tasks / Inbox support, loading skeletons, error states, empty states
- Restructure existing Journal Entry and AI Extraction Result sections into the new dashboard layout
- Backend tests: `ProjectsControllerTests`, additions to `TasksControllerTests`

### Placeholder Only (demo-ready, non-functional)
- AI Assistant panel — mock recommendation/suggestion card with sample content
- Daily Summary panel — today's productivity snapshot with static demo metrics
- Recent Activity panel — sample activity timeline entries

---

## Backend Changes

### New: `GET /api/projects`

**Controller:** `backend/Controllers/ProjectsController.cs`

```
GET /api/projects
→ calls ITodoistService.GetProjectsAsync()
→ returns ProjectResponse[]
→ on InvalidOperationException → HTTP 503 { "error": message }
```

**New model:** `backend/Models/ProjectResponse.cs`

```csharp
public record ProjectResponse(
    string Id,
    string Name,
    string Color,
    int Order,
    bool IsInboxProject);
```

Mapping: `TodoistProject → ProjectResponse` (field-for-field). Consistent with the existing `TaskResponse` pattern.

### Updated: `GET /api/tasks`

**File:** `backend/Controllers/TasksController.cs`

Add `[FromQuery] string? projectId = null` to `GetAll`. Pass it to `_todoist.GetActiveTasksAsync(projectId, ct)`. No other logic changes — the service method already handles `null` (returns all tasks) and non-null (filters by project).

No changes to `ITodoistService`, `TodoistService`, or any DTO.

---

## Frontend

### Architecture

Approach: component split, no routing library. `useState` in `App.jsx` for `activeSection`; all project/task state lives inside `ProjectsTasksPanel`.

### File Structure

```
frontend/src/
├── App.jsx                         ← layout shell, activeSection state
├── main.jsx                        ← unchanged
├── styles.css                      ← full rewrite: CSS grid dashboard layout
└── components/
    ├── Sidebar.jsx                 ← nav, branding, Todoist status, quick actions
    ├── JournalCard.jsx             ← extracted journal entry form
    ├── SummaryCard.jsx             ← extracted AI extraction result display
    ├── ProjectsTasksPanel.jsx      ← project list + task list + state
    └── PlaceholderCard.jsx         ← reusable demo stub with realistic content
```

### Dashboard Layout

Two-column CSS grid: fixed 260px sidebar + flex-grow main content area.

Main content has three rows:
1. **Top row** (two equal columns): `JournalCard` | `SummaryCard`
2. **Center row** (full width, visually dominant): `ProjectsTasksPanel`
3. **Bottom row** (three equal columns): `PlaceholderCard × 3`

### Sidebar

- App name / logo at top
- Nav items: Dashboard, Journal, Tasks, Projects, AI Assistant, Summaries, Automation, Settings
- Navigation is **UI state only** — clicking a nav item sets `activeSection` in `App.jsx` and highlights the item; it does not change the URL or render different page content in this task. All dashboard content remains visible on the single dashboard view. Real routing is deferred to a future task.
- Active item: indigo left-border accent + light background tint
- Todoist status badge: static green "Connected" indicator for demo purposes. If the Todoist API is unreachable (e.g. missing token), the Projects panel's error state already surfaces this contextually. A dedicated health-check endpoint can be wired in a future task.
- Quick actions: "+ New Task", "+ Journal Entry" buttons (no-op for now; wired in a future task)

**Visual:** `#1a1d23` background, white text, `#6366f1` accent

### ProjectsTasksPanel

The largest and most prominent content area. Two-column internal layout:

**Left column — Project list** (fixed ~180px):
- "All Tasks" item (default selected, no projectId filter)
- "Inbox" item (selects the project where `IsInboxProject === true`)
- Divider
- Remaining projects sorted by `order`
- Active project: indigo left-border + tinted background
- Loading state: 4 skeleton rows (grey animated shimmer)
- Error state: inline error message + "Retry" link

**Right column — Task list** (flex-grow):
- Header: "Tasks — {selected project name}" or "All Tasks"
- "View in Todoist →" link in header (opens `https://todoist.com` in new tab)
- Task items: content text + priority badge (P1=red, P2=orange, P3=blue, P4=grey) + due date if present
- Loading state: 5 skeleton task rows
- Error state: inline error banner + retry button
- Empty state: "No active tasks in this project." message

**Data flow:**
- Projects fetched once on `ProjectsTasksPanel` mount (`GET /api/projects`)
- Tasks fetched on mount (all tasks) and re-fetched whenever `selectedProjectId` changes
- `projectId` is `null` for All Tasks; the Inbox project's `id` for Inbox; specific project `id` otherwise

### JournalCard

Extracted from current `App.jsx`. Retains all existing logic (textarea, submit, loading, POST `/api/journal`). Styled as a dashboard card.

**Refresh mechanism:** `App.jsx` holds a `refreshKey` integer (incremented on each successful journal submission). `refreshKey` is passed as a prop to `ProjectsTasksPanel`, which includes it in its task-fetch `useEffect` dependency array — triggering a task reload whenever a journal entry is processed. No callback threading through intermediate components.

### SummaryCard

Extracted from current `App.jsx`. Displays `summary` text returned by the journal endpoint. Styled as a dashboard card.

### PlaceholderCard

Props: `title`, `icon`, `description`, `demoContent` (JSX or string).

The three instances:
- **AI Assistant**: shows a mock "Top recommendation: Focus on high-priority tasks first" card with a suggestion list
- **Daily Summary**: shows static metrics — tasks completed (3), focus score (72%), a simple progress bar
- **Recent Activity**: shows 4–5 sample timeline entries ("Journal entry processed", "Task 'Deploy hotfix' completed", etc.)

All three look visually real. None make API calls. Each has a small subtle "Demo" badge in the corner.

---

## Visual Design

| Token | Value |
|---|---|
| Sidebar bg | `#1a1d23` |
| Main bg | `#f8f9fc` |
| Card bg | `#ffffff` |
| Card border-radius | `12px` |
| Card shadow | `0 1px 3px rgba(0,0,0,0.08)` |
| Accent | `#6366f1` (indigo) |
| Text primary | `#111827` |
| Text muted | `#6b7280` |
| P1 badge | red `#ef4444` |
| P2 badge | orange `#f97316` |
| P3 badge | blue `#3b82f6` |
| P4 badge | grey `#9ca3af` |

Skeleton shimmer: CSS animation `@keyframes shimmer` with `background: linear-gradient(90deg, #f0f0f0, #e0e0e0, #f0f0f0)`.

No responsive breakpoints. Minimum viewport: ~1280px.

---

## Loading / Error / Empty States

| Surface | Loading | Error | Empty |
|---|---|---|---|
| Projects list | 4 skeleton rows (shimmer) | Inline error + Retry | "No projects found." |
| Task list | 5 skeleton rows (shimmer) | Inline error banner + Retry button | "No active tasks in this project." |
| Journal submit | Button shows "Submitting…" (existing) | Error message below form | — |
| Summary | — | — | "No summary yet." (existing) |

---

## Tests

### New: `backend.Tests/Controllers/ProjectsControllerTests.cs`

1. `GetAll_ReturnsMappedProjectResponses` — mock returns two projects; asserts Id, Name, IsInboxProject mapped correctly
2. `GetAll_WhenTokenMissing_Returns503` — mock throws `InvalidOperationException`; asserts HTTP 503

### Additions to `backend.Tests/Controllers/TasksControllerTests.cs`

3. `GetAll_WithProjectId_PassesProjectIdToService` — calls `GetAll(projectId: "proj42")`; asserts mock called with `projectId = "proj42"`
4. `GetAll_WithNullProjectId_CallsServiceWithNull` — calls `GetAll(projectId: null)`; asserts mock called with `null`

No changes to `TodoistServiceTests` — `GetActiveTasksAsync_PassesProjectIdAsQueryParam` already covers the service layer.

---

## Constraints

- Todoist is the source of truth — no tasks or projects stored in SQLite
- No OAuth, no token UI — API token is read from environment variables or .NET user-secrets (`Todoist:ApiToken`). It must **not** be committed to `appsettings.json` or any tracked file. Use `appsettings.Development.json` (git-ignored) or `dotnet user-secrets` for local development.
- No database migrations
- No new npm packages (use existing React + Vite stack)
- Do not commit without asking
