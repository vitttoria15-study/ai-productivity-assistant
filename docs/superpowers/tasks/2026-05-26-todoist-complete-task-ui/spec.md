# Spec: Todoist Complete Task UI

**Slug:** `todoist-complete-task-ui`  
**Date:** 2026-05-26  
**Branch:** `feature/todoist-complete-task-ui`

---

## Goal

Allow users to mark Todoist tasks as complete directly from `ProjectsTasksPanel` in the React frontend. Each `todo` task row gets a clickable circle checkbox that calls the existing backend close endpoint, then re-fetches the task list to reflect Todoist's state.

---

## Scope

- Add a circle checkbox button to every `todo` task row in `ProjectsTasksPanel`.
- On click, call `POST /api/tasks/{id}/close` (returns 204 NoContent).
- Show a per-task spinner while the call is in-flight.
- On success, call `fetchTasks()` to refresh the list from Todoist (source of truth).
- On failure, show a small inline error message beneath the task title.
- Tasks with `status === 'done'` do not render the checkbox.
- No AI, no DB migrations, no new frontend dependencies.

---

## Out of Scope

- Undo / un-complete support.
- Optimistic removal of the task row before the server confirms.
- Toast notifications or global error banners.
- Multi-task bulk completion.
- Any backend changes.

---

## Architecture

No new files, no new components. All changes are confined to two existing files:

| File | Change |
|---|---|
| `frontend/src/components/ProjectsTasksPanel.jsx` | New state, `closeTask` function, updated task row JSX |
| `frontend/src/styles.css` | New CSS rules for circle button, spinner, and inline error |

No new test files are created. Acceptance criteria are verified manually in Stage 5 (feature verification). A separate follow-up task will introduce the frontend testing infrastructure (Vitest + RTL).

---

## State

Two new pieces of state added to the `ProjectsTasksPanel` function component:

```js
const [closingIds,  setClosingIds]  = useState(new Set());   // task IDs in-flight
const [closeErrors, setCloseErrors] = useState(new Map());   // task ID → error string
```

---

## `closeTask(id)` Function

```
1. Clear closeErrors[id] (remove stale error from previous attempt).
2. Add id to closingIds (triggers spinner + disables button).
3. POST /api/tasks/${id}/close.
4a. On 204: remove id from closingIds; call fetchTasks().
4b. On error: remove id from closingIds; set closeErrors[id] = "Failed to complete — try again."
```

State updates use functional form (`prev => new Set/Map`) to avoid stale-closure bugs with concurrent completions.

---

## Task Row UI

**Current layout:**
```
[P-badge]  [task-content]  [due-date?]
```

**New layout (todo tasks only):**
```
[circle-btn]  [P-badge]  [task-content]  [due-date?]
                         [error-text?]
```

- **`<button className="task-complete-btn">`** — hollow circle, 18 × 18 px, `aria-label="Complete task"`.
  - While `closingIds.has(task.id)`: adds class `loading`, is `disabled`.
  - On click: calls `closeTask(task.id)`.
- **`<span className="task-error-inline">`** — rendered inside the content area, only when `closeErrors.get(task.id)` is set.
- Done tasks (`task.status === 'done'`) omit the button entirely.

---

## CSS (additions to `styles.css`)

```css
/* ── Task completion button ────────────────────────────── */
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
.task-complete-btn.loading {
  border-top-color: #16a34a;
  animation: spin 0.6s linear infinite;
  cursor: not-allowed;
  opacity: 0.7;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}

/* ── Keyboard accessibility ────────────────────────────── */
.task-complete-btn:focus-visible {
  outline: 2px solid #6366f1;
  outline-offset: 2px;
}

/* ── Per-task inline error ──────────────────────────────── */
.task-error-inline {
  display: block;
  font-size: 11px;
  color: #ef4444;
  margin-top: 2px;
}
```

If a `@keyframes spin` rule already exists in `styles.css`, reuse it instead of adding a duplicate.

---

## Error Handling

| Scenario | Behaviour |
|---|---|
| Network failure or non-2xx response | `closeErrors[id]` set; button re-enabled |
| User retries | `closeErrors[id]` cleared at start of attempt |
| Successful close | Error cleared; `fetchTasks()` called; row removed from list |
| Double-click before first call returns | Button is `disabled` while in-flight; second click not possible |

---

## Tests

Automated unit tests are deferred to a separate follow-up task (frontend has no test runner yet). Acceptance criteria are verified manually via feature verification in Stage 5.

---

## Acceptance Criteria

1. Every `todo` task row shows a circle checkbox.
2. Clicking the checkbox disables it and shows a spinner while the API call is in-flight.
3. After a successful 204, the task list refreshes and the completed task is no longer shown (Todoist no longer returns it as active).
4. After a failure, an inline error appears beneath the task title; the button is re-enabled.
5. Retrying after a failure clears the previous error before the new attempt.
6. `done` tasks do not show the checkbox.
7. No new npm dependencies are introduced (automated tests deferred to a follow-up task).
8. No database migrations are added.
