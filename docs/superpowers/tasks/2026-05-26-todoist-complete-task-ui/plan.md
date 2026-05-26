# Todoist Complete Task UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a circle checkbox to each `todo` task row in `ProjectsTasksPanel` that calls `POST /api/tasks/{id}/close`, shows a per-task spinner while in-flight, re-fetches the task list on success, and shows an inline error on failure.

**Architecture:** All changes are confined to two files: `frontend/src/components/ProjectsTasksPanel.jsx` (new state, `closeTask` function, updated JSX) and `frontend/src/styles.css` (new CSS rules). No new dependencies, no backend changes, no DB migrations.

**Tech Stack:** React 18, plain CSS, native `fetch()`.

---

## File Map

| File | Action | What changes |
|---|---|---|
| `frontend/src/styles.css` | Modify | Add rules for `.task-complete-btn`, `.loading` spinner, `:focus-visible`, `.task-error-inline`, `@keyframes spin` |
| `frontend/src/components/ProjectsTasksPanel.jsx` | Modify | Add `closingIds` + `closeErrors` state, `closeTask` async function, updated `<li>` task row JSX |

---

## Task 1: Add CSS for the completion button, spinner, and inline error

**Files:**
- Modify: `frontend/src/styles.css` (append after line 632)

- [ ] **Step 1.1: Append the new CSS rules to `styles.css`**

  Open `frontend/src/styles.css`. Add the following block at the very end of the file (after the last `}` on line 632):

  ```css
  /* ── Task completion button ──────────────────────────────────────────────── */
  .task-complete-btn {
    flex-shrink: 0;
    width: 18px;
    height: 18px;
    border-radius: 50%;
    border: 2px solid #9ca3af;
    background: transparent;
    cursor: pointer;
    padding: 0;
    transition: border-color 150ms ease;
  }

  .task-complete-btn:hover:not(:disabled) {
    border-color: #16a34a;
  }

  .task-complete-btn:focus-visible {
    outline: 2px solid #6366f1;
    outline-offset: 2px;
  }

  .task-complete-btn.loading {
    border-color: #e5e7eb;      /* muted grey track on all sides */
    border-top-color: #16a34a;  /* green arc on top only */
    animation: spin 0.6s linear infinite;
    cursor: not-allowed;
    opacity: 0.7;
  }

  @keyframes spin {
    to { transform: rotate(360deg); }
  }

  /* ── Per-task inline error ───────────────────────────────────────────────── */
  .task-error-inline {
    display: block;
    font-size: 11px;
    color: #ef4444;
    margin-top: 2px;
  }
  ```

- [ ] **Step 1.2: Visual sanity check**

  Start (or keep running) the frontend dev server:
  ```
  cd frontend && npm run dev
  ```
  Open `http://localhost:5173` in a browser and verify the task list still renders correctly with no layout changes (CSS we added targets classes not yet present in the DOM — nothing should look different yet).

---

## Task 2: Add state and `closeTask` function to `ProjectsTasksPanel`

**Files:**
- Modify: `frontend/src/components/ProjectsTasksPanel.jsx`

- [ ] **Step 2.1: Add `closingIds` and `closeErrors` state declarations**

  In `ProjectsTasksPanel.jsx`, locate the existing state block (lines 57–68):
  ```js
  const [projects,        setProjects]        = useState([]);
  const [selectedId,      setSelectedId]      = useState(null);
  const [tasks,           setTasks]           = useState([]);
  const [loadingProjects, setLoadingProjects] = useState(true);
  const [loadingTasks,    setLoadingTasks]    = useState(true);
  const [projectsError,   setProjectsError]   = useState('');
  const [tasksError,      setTasksError]      = useState('');

  // Client-side filter/sort state
  const [statusFilter,   setStatusFilter]   = useState('all');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [sortOrder,      setSortOrder]      = useState('default');
  ```

  Add two new lines **after** the `tasksError` line and before the comment about filter/sort state:
  ```js
  const [closingIds,  setClosingIds]  = useState(new Set());  // task IDs with close call in-flight
  const [closeErrors, setCloseErrors] = useState(new Map());  // task ID → error string
  ```

  The full state block should now read:
  ```js
  const [projects,        setProjects]        = useState([]);
  const [selectedId,      setSelectedId]      = useState(null);
  const [tasks,           setTasks]           = useState([]);
  const [loadingProjects, setLoadingProjects] = useState(true);
  const [loadingTasks,    setLoadingTasks]    = useState(true);
  const [projectsError,   setProjectsError]   = useState('');
  const [tasksError,      setTasksError]      = useState('');
  const [closingIds,  setClosingIds]  = useState(new Set());  // task IDs with close call in-flight
  const [closeErrors, setCloseErrors] = useState(new Map());  // task ID → error string

  // Client-side filter/sort state — persists across project selection changes.
  const [statusFilter,   setStatusFilter]   = useState('all');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [sortOrder,      setSortOrder]      = useState('default');
  ```

- [ ] **Step 2.2: Add the `closeTask` async function**

  Place this function directly after the existing `fetchTasks` function (after its closing `}`, around line 105). Add it as a new named function in the component body:

  ```js
  async function closeTask(id) {
    // Clear any stale error for this task before the new attempt.
    setCloseErrors((prev) => {
      const next = new Map(prev);
      next.delete(id);
      return next;
    });
    // Mark in-flight so the button shows a spinner and is disabled.
    setClosingIds((prev) => new Set([...prev, id]));

    try {
      const res = await fetch(`/api/tasks/${id}/close`, { method: 'POST' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      // Re-fetch to get the authoritative list from Todoist.
      await fetchTasks();
    } catch {
      setCloseErrors((prev) => new Map([...prev, [id, 'Failed to complete — try again.']]));
    } finally {
      // Always clear the in-flight marker, whether success or failure.
      setClosingIds((prev) => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
    }
  }
  ```

- [ ] **Step 2.3: Verify no console errors**

  Save the file. The dev server hot-reloads automatically. Open the browser console (`F12 → Console`) and confirm no new errors appear. The UI should be visually unchanged (we haven't touched the JSX yet).

---

## Task 3: Update task row JSX to render the completion button and inline error

**Files:**
- Modify: `frontend/src/components/ProjectsTasksPanel.jsx` (the `<ul>` task list, lines 294–308)

- [ ] **Step 3.1: Replace the existing `<li>` task row JSX**

  Locate this block (approximately lines 296–307):
  ```jsx
  {displayedTasks.map((task) => (
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
  ```

  Replace it with:
  ```jsx
  {displayedTasks.map((task) => (
    <li key={task.id} className="task-item">
      {task.status !== 'done' && (
        <button
          className={`task-complete-btn${closingIds.has(task.id) ? ' loading' : ''}`}
          aria-label="Complete task"
          disabled={closingIds.has(task.id)}
          onClick={() => closeTask(task.id)}
        />
      )}
      <span className={priorityClass(task.priority)}>
        {priorityLabel(task.priority)}
      </span>
      <span className="task-content">
        {task.title}
        {closeErrors.get(task.id) && (
          <span className="task-error-inline">{closeErrors.get(task.id)}</span>
        )}
      </span>
      {task.dueDate && (
        <span className="task-due">{task.dueDate}</span>
      )}
    </li>
  ))}
  ```

  Key changes:
  - A `<button>` appears before the priority badge for every `todo` task.
  - The button has class `loading` while its `id` is in `closingIds`; it is also `disabled`.
  - `<span className="task-content">` now wraps both the title text and the conditional error span.
  - `done` tasks omit the button entirely.

- [ ] **Step 3.2: Verify in the browser**

  1. Reload the page (or let the hot reload trigger).
  2. Confirm a small hollow circle appears to the left of each `todo` task row.
  3. Hover over a circle — border should turn green.
  4. Tab to a circle with the keyboard — it should show an indigo `focus-visible` outline.
  5. `done` tasks (visible under the "Completed" filter tab) should have no circle.

---

## Task 4: End-to-end manual verification

> This task has no code. It verifies all acceptance criteria against a running backend + frontend.

**Pre-requisites:** Backend running on `http://localhost:5184`, frontend dev server on `http://localhost:5173`.

- [ ] **AC1 — Circle renders for every todo task**

  Open the app. Switch to a project with active tasks. Confirm every task row shows a small hollow circle on the left.

- [ ] **AC2 — No circle on done tasks**

  Click the "Completed" filter tab. Confirm no circle buttons appear on any completed task row.

- [ ] **AC3 — Spinner while in-flight**

  Open the browser DevTools Network tab. Throttle the connection (Slow 3G or add a `setTimeout` mock) OR use DevTools → Network → "Offline" briefly.
  - Click a task's circle.
  - While the request is pending: the circle should spin (green arc rotating) and be non-interactive.
  - Once the response returns: spinner stops.

  *Alternative if throttling is hard:* wrap the fetch in the running app with a brief `await new Promise(r => setTimeout(r, 1500))` temporarily to make the spinner visible, then remove it.

- [ ] **AC4 — Task disappears after successful close**

  Click a circle on a real task. After the 204 response, the task should disappear from the list (it is no longer returned by `GET /api/tasks` because Todoist marks it complete).

- [ ] **AC5 — Inline error on failure**

  In the browser DevTools Network tab, select the pending `POST /api/tasks/{id}/close` call and use "Block request URL" to force a network error. Click a task circle. After the failure, a red *"Failed to complete — try again."* message should appear below the task title. The circle should be re-enabled.

- [ ] **AC6 — Retry clears previous error**

  While the error from AC5 is still visible, click the circle again. The error text should vanish immediately (before the new request returns) and the spinner should appear.

- [ ] **AC7 — No new npm dependencies**

  Run:
  ```
  git diff main -- frontend/package.json
  ```
  Expected output: no diff (the file is unchanged).

- [ ] **AC8 — No DB migrations and no backend changes**

  Run:
  ```
  git diff main --name-only -- backend/
  ```
  Expected: empty output (no backend files changed, no migration files added).
