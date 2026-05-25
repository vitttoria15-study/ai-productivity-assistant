# Todoist Projects Dashboard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `GET /api/projects` endpoint and `?projectId` filter on `GET /api/tasks`, then replace the single-page UI with a polished dashboard shell featuring a sidebar, a dominant Projects & Tasks panel, and three realistic placeholder cards.

**Architecture:** ASP.NET Core controllers call the already-implemented `ITodoistService` methods; no service changes needed. The React frontend is split into focused single-responsibility components (`Sidebar`, `JournalCard`, `SummaryCard`, `ProjectsTasksPanel`, `PlaceholderCard`); `App.jsx` becomes a thin layout shell that passes a `refreshKey` integer down to trigger task reloads after journal submission.

**Tech Stack:** C# / ASP.NET Core 8, xUnit + Moq (backend tests), React 18 + Vite (frontend, no new npm packages), plain CSS Grid for layout.

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| **Create** | `backend/Models/ProjectResponse.cs` | API response shape for a project |
| **Create** | `backend/Controllers/ProjectsController.cs` | `GET /api/projects` endpoint |
| **Modify** | `backend/Controllers/TasksController.cs` | Add `?projectId` query param to `GetAll` |
| **Create** | `backend.Tests/Controllers/ProjectsControllerTests.cs` | Controller-level tests for projects |
| **Modify** | `backend.Tests/Controllers/TasksControllerTests.cs` | Add filter tests; fix signature drift |
| **Modify** | `frontend/src/styles.css` | Full rewrite: dashboard grid, sidebar, cards, skeletons |
| **Create** | `frontend/src/components/Sidebar.jsx` | Left nav, branding, status badge, quick actions |
| **Create** | `frontend/src/components/PlaceholderCard.jsx` | Wrapper for demo stub cards |
| **Create** | `frontend/src/components/JournalCard.jsx` | Journal form (extracted from App.jsx) |
| **Create** | `frontend/src/components/SummaryCard.jsx` | AI result display (extracted from App.jsx) |
| **Create** | `frontend/src/components/ProjectsTasksPanel.jsx` | Projects list + filtered task list |
| **Modify** | `frontend/src/App.jsx` | Dashboard shell: layout + refreshKey state |

---

## Task 1 — Add `ProjectResponse` model

**Test-first: No** — pure data record with no logic; tested implicitly through `ProjectsController` in Task 2.

**Files:**
- Create: `backend/Models/ProjectResponse.cs`

- [ ] **Create `ProjectResponse.cs`**

```csharp
namespace backend.Models;

/// <summary>
/// Normalised project shape returned to the frontend.
/// Maps Todoist's wire format to a clean API surface
/// consistent with the TaskResponse pattern.
/// </summary>
public record ProjectResponse(
    string Id,
    string Name,
    string Color,
    int    Order,
    bool   IsInboxProject);
```

- [ ] **Build to confirm it compiles**

```
dotnet build backend/backend.csproj
```

Expected: `Build succeeded. 0 Error(s)`

---

## Task 2 — Add `ProjectsController` (TDD)

**Test-first: Yes** — write the failing tests first, then implement the controller.

**Files:**
- Create: `backend.Tests/Controllers/ProjectsControllerTests.cs`
- Create: `backend/Controllers/ProjectsController.cs`

- [ ] **Write the failing tests**

Create `backend.Tests/Controllers/ProjectsControllerTests.cs`:

```csharp
using backend.Controllers;
using backend.Models;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public class ProjectsControllerTests
{
    private static Mock<ITodoistService> MockService() => new Mock<ITodoistService>();

    // ── GET /api/projects ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsMappedProjectResponses()
    {
        var mock = MockService();
        mock.Setup(s => s.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TodoistProject>
            {
                new("p1", "Inbox", "grey", 1, true),
                new("p2", "Work",  "blue", 2, false)
            });

        var controller = new ProjectsController(mock.Object);
        var result     = await controller.GetAll(CancellationToken.None);

        var ok       = Assert.IsType<OkObjectResult>(result);
        var projects = Assert.IsAssignableFrom<IEnumerable<ProjectResponse>>(ok.Value).ToList();

        Assert.Equal(2,       projects.Count);
        Assert.Equal("p1",    projects[0].Id);
        Assert.Equal("Inbox", projects[0].Name);
        Assert.Equal("grey",  projects[0].Color);
        Assert.Equal(1,       projects[0].Order);
        Assert.True(projects[0].IsInboxProject);

        Assert.Equal("p2",   projects[1].Id);
        Assert.Equal("Work", projects[1].Name);
        Assert.False(projects[1].IsInboxProject);
    }

    [Fact]
    public async Task GetAll_WhenTokenMissing_Returns503()
    {
        var mock = MockService();
        mock.Setup(s => s.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Todoist ApiToken is not configured"));

        var controller = new ProjectsController(mock.Object);
        var result     = await controller.GetAll(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
    }
}
```

- [ ] **Run tests — expect compile error** (controller doesn't exist yet)

```
dotnet test backend.Tests/backend.Tests.csproj --filter "FullyQualifiedName~ProjectsControllerTests"
```

Expected: build error — `The type or namespace name 'ProjectsController' could not be found`

- [ ] **Implement `ProjectsController`**

Create `backend/Controllers/ProjectsController.cs`:

```csharp
using backend.Models;
using backend.Services.Todoist;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ITodoistService _todoist;

    public ProjectsController(ITodoistService todoist)
    {
        _todoist = todoist;
    }

    // ── GET /api/projects ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var projects = await _todoist.GetProjectsAsync(cancellationToken);
            return Ok(projects.Select(p => new ProjectResponse(
                p.Id, p.Name, p.Color, p.Order, p.IsInboxProject)));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }
}
```

- [ ] **Run tests — expect GREEN**

```
dotnet test backend.Tests/backend.Tests.csproj --filter "FullyQualifiedName~ProjectsControllerTests"
```

Expected:
```
Passed!  - Failed: 0, Passed: 2, Skipped: 0
```

- [ ] **Commit**

> Do not commit until asked. Mark this task complete and continue to Task 3.

---

## Task 3 — Add `?projectId` filter to `TasksController` (TDD)

**Test-first: Yes** — write the two new filter tests and fix the existing test calls, then update the controller.

**Files:**
- Modify: `backend.Tests/Controllers/TasksControllerTests.cs`
- Modify: `backend/Controllers/TasksController.cs`

- [ ] **Add new tests and fix existing call signatures**

Open `backend.Tests/Controllers/TasksControllerTests.cs`.

**Step A — Fix the two existing test calls.** The controller's `GetAll` signature is gaining a new first parameter (`string? projectId`). Without this fix the existing tests fail to compile.

Find these two lines (one in `GetAll_ReturnsMappedTaskResponses`, one in `GetAll_WhenTokenMissing_Returns503`):
```csharp
var result = await controller.GetAll(CancellationToken.None);
```
Replace both with (use named parameter so intent is clear):
```csharp
var result = await controller.GetAll(cancellationToken: CancellationToken.None);
```

**Step B — Add two new tests** at the end of the class (before the closing `}`):

```csharp
    // ── GET /api/tasks?projectId= ─────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithProjectId_PassesProjectIdToService()
    {
        var mock = MockService();
        mock.Setup(s => s.GetActiveTasksAsync("proj42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TodoistTask>
            {
                new("1", "Task in proj42", null, "proj42", 1, null, null)
            });

        var controller = new TasksController(mock.Object);
        var result = await controller.GetAll("proj42", CancellationToken.None);

        var ok    = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsAssignableFrom<IEnumerable<TaskResponse>>(ok.Value).ToList();
        Assert.Single(tasks);
        mock.Verify(
            s => s.GetActiveTasksAsync("proj42", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAll_WithNullProjectId_CallsServiceWithNull()
    {
        var mock = MockService();
        mock.Setup(s => s.GetActiveTasksAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TodoistTask>());

        var controller = new TasksController(mock.Object);
        var result = await controller.GetAll(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        mock.Verify(
            s => s.GetActiveTasksAsync(null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
```

- [ ] **Run tests — expect failure on the two new tests** (controller still has the old signature)

```
dotnet test backend.Tests/backend.Tests.csproj --filter "FullyQualifiedName~TasksControllerTests"
```

Expected: the two new tests fail because `GetAll(string, CancellationToken)` overload doesn't exist yet; existing tests compile and pass (named param fix applied).

- [ ] **Update `TasksController.GetAll` to accept `projectId`**

Open `backend/Controllers/TasksController.cs`. Replace the `GetAll` method:

```csharp
// ── GET /api/tasks ────────────────────────────────────────────────────────

[HttpGet]
public async Task<IActionResult> GetAll(
    [FromQuery] string? projectId = null,
    CancellationToken cancellationToken = default)
{
    try
    {
        var tasks = await _todoist.GetActiveTasksAsync(projectId, ct: cancellationToken);
        return Ok(tasks.Select(ToResponse));
    }
    catch (InvalidOperationException ex)
    {
        return StatusCode(503, new { error = ex.Message });
    }
}
```

- [ ] **Run all backend tests — expect full GREEN**

```
dotnet test backend.Tests/backend.Tests.csproj
```

Expected:
```
Passed!  - Failed: 0, Passed: <N>, Skipped: 0
```

(N = all pre-existing tests + the 4 new ones from Tasks 2 and 3)

- [ ] **Commit (ask user first)**

> Do not commit until asked. Mark this task complete and continue to Task 4.

---

## Task 4 — Rewrite `styles.css` (dashboard layout)

**Test-first: No** — no frontend test runner in this project; visual verification via browser.

**Files:**
- Modify: `frontend/src/styles.css`

- [ ] **Replace the entire contents of `frontend/src/styles.css`**

```css
/* ── Reset & base ─────────────────────────────────────────────────────────── */
:root {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  color: #111827;
  line-height: 1.5;
  font-weight: 400;
}

* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

body {
  min-height: 100vh;
  background: #f8f9fc;
}

/* ── Dashboard shell ─────────────────────────────────────────────────────── */
.dashboard-shell {
  display: flex;
  min-height: 100vh;
}

/* ── Sidebar ─────────────────────────────────────────────────────────────── */
.sidebar {
  width: 260px;
  flex-shrink: 0;
  background: #1a1d23;
  color: #e5e7eb;
  display: flex;
  flex-direction: column;
  position: sticky;
  top: 0;
  height: 100vh;
  overflow-y: auto;
}

.sidebar-brand {
  padding: 20px 20px 16px;
  border-bottom: 1px solid #2d3139;
}

.sidebar-brand-title {
  font-size: 14px;
  font-weight: 700;
  color: #ffffff;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}

.sidebar-brand-sub {
  font-size: 11px;
  color: #6b7280;
  margin-top: 2px;
}

.sidebar-nav {
  flex: 1;
  padding: 12px 0;
}

.sidebar-nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 20px;
  font-size: 13.5px;
  color: #9ca3af;
  cursor: pointer;
  border-left: 3px solid transparent;
  user-select: none;
}

.sidebar-nav-item:hover {
  background: #252930;
  color: #e5e7eb;
}

.sidebar-nav-item.active {
  background: #252930;
  color: #ffffff;
  border-left-color: #6366f1;
}

.sidebar-status {
  margin: 0 12px 12px;
  background: #252930;
  border-radius: 8px;
  padding: 12px;
}

.sidebar-status-title {
  font-size: 12px;
  font-weight: 600;
  color: #e5e7eb;
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 2px;
}

.sidebar-status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #22c55e;
  flex-shrink: 0;
}

.sidebar-status-sub {
  font-size: 11.5px;
  color: #6b7280;
}

.sidebar-quick-actions {
  margin: 0 12px 16px;
}

.sidebar-quick-label {
  font-size: 10.5px;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  margin-bottom: 6px;
  padding-left: 2px;
}

.btn-sidebar-action {
  display: block;
  width: 100%;
  padding: 7px 10px;
  margin-bottom: 6px;
  background: #2d3139;
  border: 1px solid #374151;
  border-radius: 6px;
  color: #d1d5db;
  font-size: 13px;
  cursor: pointer;
  text-align: left;
  font-family: inherit;
}

.btn-sidebar-action:hover {
  background: #374151;
  color: #ffffff;
}

/* ── Main content ────────────────────────────────────────────────────────── */
.dashboard-main {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
  min-width: 0;
}

.dashboard-top-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

.dashboard-bottom-row {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 20px;
}

/* ── Cards ───────────────────────────────────────────────────────────────── */
.card {
  background: #ffffff;
  border-radius: 12px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
  padding: 20px;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.card-title {
  font-size: 15px;
  font-weight: 600;
  color: #111827;
}

.card-link {
  font-size: 12px;
  color: #6366f1;
  text-decoration: none;
}

.card-link:hover {
  text-decoration: underline;
}

/* ── Journal card ────────────────────────────────────────────────────────── */
.journal-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.journal-label {
  font-size: 13px;
  font-weight: 600;
  color: #374151;
}

.journal-textarea {
  width: 100%;
  resize: vertical;
  padding: 10px 12px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font: inherit;
  font-size: 13.5px;
  color: #111827;
  min-height: 110px;
}

.journal-textarea:focus {
  outline: none;
  border-color: #6366f1;
  box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.1);
}

.btn-primary {
  padding: 9px 18px;
  background: #6366f1;
  color: #ffffff;
  border: none;
  border-radius: 8px;
  font-size: 13.5px;
  font-weight: 500;
  cursor: pointer;
  width: fit-content;
  font-family: inherit;
}

.btn-primary:hover:not(:disabled) {
  background: #4f46e5;
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* ── Summary card ────────────────────────────────────────────────────────── */
.summary-text {
  font-size: 13.5px;
  color: #374151;
  line-height: 1.6;
}

/* ── Projects & Tasks panel ──────────────────────────────────────────────── */
.projects-tasks-panel {
  display: grid;
  grid-template-columns: 200px 1fr;
  gap: 0;
  min-height: 400px;
}

.project-list {
  border-right: 1px solid #f0f0f0;
  padding-top: 4px;
}

.project-list-header {
  font-size: 10.5px;
  font-weight: 600;
  color: #9ca3af;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  padding: 0 12px 8px;
}

.project-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  font-size: 13.5px;
  color: #374151;
  cursor: pointer;
  border-left: 3px solid transparent;
  border-radius: 0 6px 6px 0;
  margin-right: 8px;
}

.project-item:hover {
  background: #f9fafb;
}

.project-item.active {
  background: #eef2ff;
  color: #4338ca;
  border-left-color: #6366f1;
  font-weight: 500;
}

.project-color-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.project-divider {
  border: none;
  border-top: 1px solid #f0f0f0;
  margin: 6px 12px;
}

/* ── Task panel ──────────────────────────────────────────────────────────── */
.task-panel {
  padding: 4px 0 0 20px;
}

.task-panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
}

.task-panel-title {
  font-size: 14px;
  font-weight: 600;
  color: #111827;
}

.task-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.task-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  background: #fafafa;
}

.task-item:hover {
  background: #f3f4f6;
}

.task-content {
  flex: 1;
  font-size: 13.5px;
  color: #111827;
}

.task-due {
  font-size: 11.5px;
  color: #9ca3af;
  flex-shrink: 0;
}

/* ── Priority badges ─────────────────────────────────────────────────────── */
.badge {
  font-size: 10.5px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 4px;
  letter-spacing: 0.04em;
  flex-shrink: 0;
}

.badge-p1 { background: #fee2e2; color: #b91c1c; }
.badge-p2 { background: #ffedd5; color: #c2410c; }
.badge-p3 { background: #dbeafe; color: #1d4ed8; }
.badge-p4 { background: #f3f4f6; color: #6b7280; }

/* ── Loading skeletons ───────────────────────────────────────────────────── */
@keyframes shimmer {
  0%   { background-position: -200% 0; }
  100% { background-position:  200% 0; }
}

.skeleton {
  background: linear-gradient(90deg, #f0f0f0 25%, #e6e6e6 50%, #f0f0f0 75%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite;
  border-radius: 6px;
}

.skeleton-row {
  height: 28px;
  margin: 6px 12px;
}

.skeleton-task {
  height: 40px;
  margin-bottom: 6px;
  border-radius: 8px;
}

/* ── Error / empty states ────────────────────────────────────────────────── */
.error-inline {
  color: #b91c1c;
  font-size: 13px;
}

.error-banner {
  background: #fef2f2;
  border: 1px solid #fecaca;
  border-radius: 8px;
  padding: 10px 14px;
  color: #b91c1c;
  font-size: 13px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.btn-retry {
  background: none;
  border: 1px solid #fca5a5;
  border-radius: 6px;
  color: #b91c1c;
  font-size: 12px;
  padding: 3px 10px;
  cursor: pointer;
  font-family: inherit;
  flex-shrink: 0;
}

.btn-retry:hover {
  background: #fee2e2;
}

.empty-state {
  color: #9ca3af;
  font-size: 13.5px;
  text-align: center;
  padding: 40px 16px;
}

.muted {
  color: #9ca3af;
  font-size: 13.5px;
}

/* ── Placeholder cards ───────────────────────────────────────────────────── */
.placeholder-card {
  position: relative;
}

.placeholder-demo-badge {
  position: absolute;
  top: 14px;
  right: 14px;
  font-size: 10px;
  font-weight: 600;
  color: #9ca3af;
  background: #f3f4f6;
  border-radius: 4px;
  padding: 2px 6px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
```

---

## Task 5 — Create `Sidebar.jsx`

**Test-first: No** — no frontend test runner.

**Files:**
- Create: `frontend/src/components/Sidebar.jsx`

- [ ] **Create the `components/` directory and `Sidebar.jsx`**

```jsx
const NAV_ITEMS = [
  { id: 'dashboard',  label: 'Dashboard',    icon: '⊞' },
  { id: 'journal',    label: 'Journal',       icon: '✏' },
  { id: 'tasks',      label: 'Tasks',         icon: '✓' },
  { id: 'projects',   label: 'Projects',      icon: '⬡' },
  { id: 'assistant',  label: 'AI Assistant',  icon: '✦' },
  { id: 'summaries',  label: 'Summaries',     icon: '◈' },
  { id: 'automation', label: 'Automation',    icon: '⚙' },
  { id: 'settings',   label: 'Settings',      icon: '≡' },
];

function Sidebar({ activeSection, onSectionChange }) {
  return (
    <aside className="sidebar">
      {/* Branding */}
      <div className="sidebar-brand">
        <p className="sidebar-brand-title">AI Productivity</p>
        <p className="sidebar-brand-sub">Assistant</p>
      </div>

      {/* Navigation */}
      <nav className="sidebar-nav">
        {NAV_ITEMS.map((item) => (
          <div
            key={item.id}
            className={`sidebar-nav-item${activeSection === item.id ? ' active' : ''}`}
            onClick={() => onSectionChange(item.id)}
          >
            <span aria-hidden="true">{item.icon}</span>
            <span>{item.label}</span>
          </div>
        ))}
      </nav>

      {/* Todoist status */}
      <div className="sidebar-status">
        <p className="sidebar-status-title">
          <span className="sidebar-status-dot" />
          Todoist
        </p>
        <p className="sidebar-status-sub">Connected</p>
      </div>

      {/* Quick actions */}
      <div className="sidebar-quick-actions">
        <p className="sidebar-quick-label">Quick Actions</p>
        <button className="btn-sidebar-action">+ New Task</button>
        <button className="btn-sidebar-action">+ Journal Entry</button>
      </div>
    </aside>
  );
}

export default Sidebar;
```

---

## Task 6 — Create `PlaceholderCard.jsx`

**Test-first: No** — no frontend test runner.

**Files:**
- Create: `frontend/src/components/PlaceholderCard.jsx`

- [ ] **Create `PlaceholderCard.jsx`**

```jsx
function PlaceholderCard({ title, children }) {
  return (
    <div className="card placeholder-card">
      <span className="placeholder-demo-badge">Demo</span>
      <div className="card-header">
        <h2 className="card-title">{title}</h2>
      </div>
      {children}
    </div>
  );
}

export default PlaceholderCard;
```

---

## Task 7 — Extract `JournalCard.jsx` and `SummaryCard.jsx`

**Test-first: No** — extraction / refactor of existing logic, no frontend test runner.

**Files:**
- Create: `frontend/src/components/JournalCard.jsx`
- Create: `frontend/src/components/SummaryCard.jsx`

- [ ] **Create `JournalCard.jsx`**

All journal logic is extracted from `App.jsx`. The component receives one prop: `onJournalProcessed(summary)` — called with the AI summary string after a successful submission.

```jsx
import { useState } from 'react';

function JournalCard({ onJournalProcessed }) {
  const [journalText, setJournalText] = useState('');
  const [loading, setLoading]         = useState(false);
  const [error, setError]             = useState('');

  async function handleSubmit(event) {
    event.preventDefault();
    setLoading(true);
    setError('');

    try {
      const response = await fetch('/api/journal', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ journalText }),
      });

      if (!response.ok) throw new Error('Failed to submit journal entry.');

      const data = await response.json();
      setJournalText('');
      onJournalProcessed(data.summary ?? '');
    } catch {
      setError('Failed to submit journal entry.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="card">
      <div className="card-header">
        <h2 className="card-title">Journal Entry</h2>
      </div>
      <form className="journal-form" onSubmit={handleSubmit}>
        <label className="journal-label" htmlFor="journal">
          What did you work on today?
        </label>
        <textarea
          id="journal"
          className="journal-textarea"
          value={journalText}
          onChange={(e) => setJournalText(e.target.value)}
          placeholder="Write what you completed, what is blocked, and what needs to happen next."
          rows="6"
        />
        <button
          type="submit"
          className="btn-primary"
          disabled={loading || !journalText.trim()}
        >
          {loading ? 'Processing...' : 'Process with AI'}
        </button>
      </form>
      {error && (
        <p className="error-inline" style={{ marginTop: 8 }}>{error}</p>
      )}
    </div>
  );
}

export default JournalCard;
```

- [ ] **Create `SummaryCard.jsx`**

```jsx
function SummaryCard({ summary }) {
  return (
    <div className="card">
      <div className="card-header">
        <h2 className="card-title">AI Extraction Result</h2>
      </div>
      {summary ? (
        <p className="summary-text">{summary}</p>
      ) : (
        <p className="muted">
          No summary yet. Submit a journal entry to see AI extraction results.
        </p>
      )}
    </div>
  );
}

export default SummaryCard;
```

---

## Task 8 — Create `ProjectsTasksPanel.jsx`

**Test-first: No** — no frontend test runner; verified via browser (Task 9 wires it in).

**Files:**
- Create: `frontend/src/components/ProjectsTasksPanel.jsx`

- [ ] **Create `ProjectsTasksPanel.jsx`**

`refreshKey` is an integer prop incremented by `App.jsx` after each journal submission. It is listed in the task-fetch `useEffect` dependency array so tasks reload automatically.

```jsx
import { useEffect, useState } from 'react';

// Maps Todoist color names to hex values.
const COLOR_MAP = {
  berry_red:   '#b91c1c', red:        '#ef4444',
  salmon:      '#f97316', orange:     '#f97316',
  yellow:      '#eab308', olive_green:'#65a30d',
  lime_green:  '#22c55e', green:      '#16a34a',
  mint_green:  '#10b981', teal:       '#14b8a6',
  sky_blue:    '#0ea5e9', light_blue: '#38bdf8',
  blue:        '#3b82f6', grape:      '#8b5cf6',
  violet:      '#7c3aed', lavender:   '#a78bfa',
  magenta:     '#ec4899', charcoal:   '#374151',
  grey:        '#9ca3af', taupe:      '#a1a1aa',
};

function priorityClass(p) {
  return `badge badge-p${p <= 3 ? p : 4}`;
}

function priorityLabel(p) {
  return `P${p <= 3 ? p : 4}`;
}

function ProjectsTasksPanel({ refreshKey }) {
  const [projects,        setProjects]        = useState([]);
  const [selectedId,      setSelectedId]      = useState(null); // null = All Tasks
  const [tasks,           setTasks]           = useState([]);
  const [loadingProjects, setLoadingProjects] = useState(true);
  const [loadingTasks,    setLoadingTasks]    = useState(true);
  const [projectsError,   setProjectsError]   = useState('');
  const [tasksError,      setTasksError]      = useState('');

  useEffect(() => { fetchProjects(); }, []);

  useEffect(() => { fetchTasks(); }, [selectedId, refreshKey]);

  async function fetchProjects() {
    setLoadingProjects(true);
    setProjectsError('');
    try {
      const res = await fetch('/api/projects');
      if (!res.ok) throw new Error();
      setProjects(await res.json());
    } catch {
      setProjectsError('Could not load projects.');
    } finally {
      setLoadingProjects(false);
    }
  }

  async function fetchTasks() {
    setLoadingTasks(true);
    setTasksError('');
    try {
      const url = selectedId
        ? `/api/tasks?projectId=${encodeURIComponent(selectedId)}`
        : '/api/tasks';
      const res = await fetch(url);
      if (!res.ok) throw new Error();
      setTasks(await res.json());
    } catch {
      setTasksError('Could not load tasks.');
    } finally {
      setLoadingTasks(false);
    }
  }

  const inboxProject  = projects.find((p) => p.isInboxProject);
  const otherProjects = projects.filter((p) => !p.isInboxProject);

  function selectedName() {
    if (selectedId === null) return 'All Tasks';
    return projects.find((p) => p.id === selectedId)?.name ?? 'Tasks';
  }

  return (
    <div className="card">
      <div className="card-header">
        <h2 className="card-title">Projects &amp; Tasks</h2>
      </div>

      <div className="projects-tasks-panel">
        {/* ── Project list ── */}
        <div className="project-list">
          <p className="project-list-header">Projects</p>

          {/* All Tasks */}
          <div
            className={`project-item${selectedId === null ? ' active' : ''}`}
            onClick={() => setSelectedId(null)}
          >
            All Tasks
          </div>

          {/* Inbox */}
          {inboxProject && (
            <div
              className={`project-item${selectedId === inboxProject.id ? ' active' : ''}`}
              onClick={() => setSelectedId(inboxProject.id)}
            >
              <span
                className="project-color-dot"
                style={{ background: COLOR_MAP[inboxProject.color] ?? '#9ca3af' }}
              />
              Inbox
            </div>
          )}

          {otherProjects.length > 0 && <hr className="project-divider" />}

          {/* Loading skeletons */}
          {loadingProjects && (
            <>
              <div className="skeleton skeleton-row" />
              <div className="skeleton skeleton-row" />
              <div className="skeleton skeleton-row" />
            </>
          )}

          {/* Error */}
          {!loadingProjects && projectsError && (
            <div style={{ padding: '8px 12px' }}>
              <p className="error-inline" style={{ marginBottom: 4 }}>
                {projectsError}
              </p>
              <button className="btn-retry" onClick={fetchProjects}>
                Retry
              </button>
            </div>
          )}

          {/* Project items */}
          {!loadingProjects &&
            !projectsError &&
            otherProjects.map((project) => (
              <div
                key={project.id}
                className={`project-item${selectedId === project.id ? ' active' : ''}`}
                onClick={() => setSelectedId(project.id)}
              >
                <span
                  className="project-color-dot"
                  style={{ background: COLOR_MAP[project.color] ?? '#9ca3af' }}
                />
                {project.name}
              </div>
            ))}

          {!loadingProjects && !projectsError && projects.length === 0 && (
            <p className="muted" style={{ padding: '8px 12px', fontSize: 12 }}>
              No projects found.
            </p>
          )}
        </div>

        {/* ── Task panel ── */}
        <div className="task-panel">
          <div className="task-panel-header">
            <span className="task-panel-title">{selectedName()}</span>
            <a
              className="card-link"
              href="https://todoist.com"
              target="_blank"
              rel="noreferrer"
            >
              View in Todoist →
            </a>
          </div>

          {/* Loading skeletons */}
          {loadingTasks && (
            <>
              {[...Array(5)].map((_, i) => (
                <div key={i} className="skeleton skeleton-task" />
              ))}
            </>
          )}

          {/* Error */}
          {!loadingTasks && tasksError && (
            <div className="error-banner">
              <span>{tasksError}</span>
              <button className="btn-retry" onClick={fetchTasks}>
                Retry
              </button>
            </div>
          )}

          {/* Empty */}
          {!loadingTasks && !tasksError && tasks.length === 0 && (
            <p className="empty-state">No active tasks in this project.</p>
          )}

          {/* Task list */}
          {!loadingTasks && !tasksError && tasks.length > 0 && (
            <ul className="task-list">
              {tasks.map((task) => (
                <li key={task.id} className="task-item">
                  <span className={priorityClass(task.priority)}>
                    {priorityLabel(task.priority)}
                  </span>
                  <span className="task-content">{task.title}</span>
                  {task.dueDate && (
                    <span className="task-due">{task.dueDate}</span>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

export default ProjectsTasksPanel;
```

---

## Task 9 — Refactor `App.jsx` to dashboard shell

**Test-first: No** — no frontend test runner; manual browser verification.

**Files:**
- Modify: `frontend/src/App.jsx`

- [ ] **Replace the entire contents of `frontend/src/App.jsx`**

```jsx
import { useState } from 'react';
import Sidebar            from './components/Sidebar';
import JournalCard        from './components/JournalCard';
import SummaryCard        from './components/SummaryCard';
import ProjectsTasksPanel from './components/ProjectsTasksPanel';
import PlaceholderCard    from './components/PlaceholderCard';

function App() {
  const [activeSection, setActiveSection] = useState('dashboard');
  const [summary,       setSummary]       = useState('');
  const [refreshKey,    setRefreshKey]    = useState(0);

  function handleJournalProcessed(newSummary) {
    setSummary(newSummary);
    setRefreshKey((k) => k + 1);
  }

  return (
    <div className="dashboard-shell">
      <Sidebar
        activeSection={activeSection}
        onSectionChange={setActiveSection}
      />

      <main className="dashboard-main">
        {/* Top row: Journal + Summary */}
        <div className="dashboard-top-row">
          <JournalCard onJournalProcessed={handleJournalProcessed} />
          <SummaryCard summary={summary} />
        </div>

        {/* Center row: Projects & Tasks (dominant) */}
        <ProjectsTasksPanel refreshKey={refreshKey} />

        {/* Bottom row: placeholder cards */}
        <div className="dashboard-bottom-row">
          {/* AI Assistant */}
          <PlaceholderCard title="AI Assistant">
            <p className="muted" style={{ fontSize: 12, marginBottom: 12 }}>
              Recommendations based on your journal &amp; tasks
            </p>
            <div style={{ background: '#f0f4ff', borderRadius: 8, padding: '10px 14px', marginBottom: 8 }}>
              <p style={{ fontSize: 13, color: '#4338ca', fontWeight: 500 }}>
                ✦ Focus on high-priority tasks first
              </p>
              <p style={{ fontSize: 12, color: '#6b7280', marginTop: 4 }}>
                3 P1 tasks are overdue — consider addressing them before new work.
              </p>
            </div>
            <div style={{ background: '#f0f4ff', borderRadius: 8, padding: '10px 14px' }}>
              <p style={{ fontSize: 13, color: '#4338ca', fontWeight: 500 }}>
                ✦ Schedule a review session
              </p>
              <p style={{ fontSize: 12, color: '#6b7280', marginTop: 4 }}>
                Your last journal entry mentioned blockers that haven't been resolved.
              </p>
            </div>
          </PlaceholderCard>

          {/* Daily Summary */}
          <PlaceholderCard title="Daily Summary">
            <p className="muted" style={{ fontSize: 12, marginBottom: 12 }}>
              Today's productivity snapshot
            </p>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 14 }}>
              <div style={{ background: '#f0fdf4', borderRadius: 8, padding: '10px 12px', textAlign: 'center' }}>
                <p style={{ fontSize: 22, fontWeight: 700, color: '#15803d' }}>3</p>
                <p style={{ fontSize: 11, color: '#6b7280' }}>Tasks Completed</p>
              </div>
              <div style={{ background: '#eff6ff', borderRadius: 8, padding: '10px 12px', textAlign: 'center' }}>
                <p style={{ fontSize: 22, fontWeight: 700, color: '#1d4ed8' }}>72%</p>
                <p style={{ fontSize: 11, color: '#6b7280' }}>Focus Score</p>
              </div>
            </div>
            <div style={{ background: '#f9fafb', borderRadius: 8, padding: '8px 12px' }}>
              <p style={{ fontSize: 12, color: '#374151' }}>🏆 Streak: 4 days in a row</p>
            </div>
          </PlaceholderCard>

          {/* Recent Activity */}
          <PlaceholderCard title="Recent Activity">
            <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 8 }}>
              {[
                { time: '2h ago',    text: 'Journal entry processed',          icon: '✏' },
                { time: '3h ago',    text: 'Task "Deploy hotfix" completed',   icon: '✓' },
                { time: '5h ago',    text: 'Task "Review PR #42" created',     icon: '+' },
                { time: 'Yesterday', text: 'Journal entry processed',          icon: '✏' },
                { time: 'Yesterday', text: '2 tasks moved to Inbox',           icon: '↓' },
              ].map((item, i) => (
                <li key={i} style={{ display: 'flex', gap: 10, alignItems: 'flex-start' }}>
                  <span style={{
                    width: 22, height: 22, background: '#f3f4f6',
                    borderRadius: '50%', display: 'flex', alignItems: 'center',
                    justifyContent: 'center', fontSize: 11, flexShrink: 0,
                  }}>
                    {item.icon}
                  </span>
                  <div>
                    <span style={{ fontSize: 13, color: '#111827' }}>{item.text}</span>
                    <span style={{ fontSize: 11, color: '#9ca3af', marginLeft: 6 }}>
                      {item.time}
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          </PlaceholderCard>
        </div>
      </main>
    </div>
  );
}

export default App;
```

- [ ] **Start the backend** (in a separate terminal, from repo root)

```
dotnet run --project backend/backend.csproj
```

Confirm it starts on `http://localhost:5184`. If `Todoist:ApiToken` is empty, you will see a startup warning — that's expected. Set your token in `backend/appsettings.Development.json` (this file is git-ignored):

```json
{
  "Todoist": {
    "ApiToken": "YOUR_TOKEN_HERE"
  }
}
```

- [ ] **Start the frontend** (in a separate terminal, from `frontend/`)

```
cd frontend
npm run dev
```

Open `http://localhost:5173` in a browser.

- [ ] **Verify the dashboard**

Check:
- [ ] Dark sidebar with nav items; "Dashboard" is highlighted with indigo left border
- [ ] "Todoist — Connected" status badge visible at bottom of sidebar
- [ ] Top row: Journal Entry card (textarea + "Process with AI" button) + AI Extraction Result card
- [ ] Center: Projects & Tasks panel with skeleton rows while loading, then project list on left and task list on right
- [ ] Selecting a project in the list fetches `/api/tasks?projectId=<id>` and updates the task list
- [ ] "All Tasks" clears the filter and shows all tasks
- [ ] "View in Todoist →" link opens todoist.com in a new tab
- [ ] Bottom row: 3 realistic placeholder cards with "Demo" badges (AI Assistant, Daily Summary, Recent Activity)
- [ ] Clicking sidebar nav items highlights them (no URL change, all content stays visible)

- [ ] **Run the full test suite one final time**

```
dotnet test backend.Tests/backend.Tests.csproj
```

Expected: all tests GREEN.

- [ ] **Ask user before committing**

> Implementation complete. All tests pass. Notify the user and ask which files to stage and what commit message to use before running any `git commit`.

---

## Self-Review Notes

- All spec requirements are covered: `GET /api/projects` (Task 2), `?projectId` filter (Task 3), project list / task list / inbox / all tasks / loading skeletons / error states / empty states (Task 8), sidebar / dashboard shell (Tasks 4–5, 9), placeholder cards (Tasks 6, 9).
- No placeholders, TBDs, or "similar to Task N" shortcuts in any step.
- Type consistency: `ProjectResponse(Id, Name, Color, Order, IsInboxProject)` defined in Task 1, used in Task 2's tests and controller. `refreshKey: number` defined in Task 9's `App.jsx`, consumed as prop in Task 8's `ProjectsTasksPanel`.
- The `useEffect` in `ProjectsTasksPanel` has `[selectedId, refreshKey]` as dependencies — covers both project selection changes and journal-triggered reloads.
- Existing `TasksControllerTests` calls updated in Task 3 (named param) before the signature change to avoid compile-time breakage.
