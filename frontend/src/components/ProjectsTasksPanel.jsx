import { useEffect, useState } from 'react';

// Maps Todoist color names to hex values.
const COLOR_MAP = {
  berry_red:    '#b91c1c', red:         '#ef4444',
  salmon:       '#f97316', orange:      '#f97316',
  yellow:       '#eab308', olive_green: '#65a30d',
  lime_green:   '#22c55e', green:       '#16a34a',
  mint_green:   '#10b981', teal:        '#14b8a6',
  sky_blue:     '#0ea5e9', light_blue:  '#38bdf8',
  blue:         '#3b82f6', grape:       '#8b5cf6',
  violet:       '#7c3aed', lavender:    '#a78bfa',
  magenta:      '#ec4899', charcoal:    '#374151',
  grey:         '#9ca3af', taupe:       '#a1a1aa',
};

function priorityClass(p) {
  return `badge badge-p${p >= 1 && p <= 3 ? p : 4}`;
}

function priorityLabel(p) {
  return `P${p >= 1 && p <= 3 ? p : 4}`;
}

// Client-side filter + sort. Receives the full tasks array from the API.
function applyFiltersAndSort(tasks, statusFilter, priorityFilter, sortOrder) {
  let result = tasks;

  // Status filter
  if (statusFilter !== 'all') {
    result = result.filter((t) => t.status === statusFilter);
  }

  // Priority filter
  if (priorityFilter !== 'all') {
    const p = parseInt(priorityFilter, 10);
    result = result.filter((t) => t.priority === p);
  }

  // Sort (copy first so we don't mutate state)
  if (sortOrder === 'priority') {
    result = [...result].sort((a, b) => a.priority - b.priority);
  } else if (sortOrder === 'due') {
    result = [...result].sort((a, b) => {
      if (!a.dueDate && !b.dueDate) return 0;
      if (!a.dueDate) return 1;  // nulls last
      if (!b.dueDate) return -1;
      return a.dueDate.localeCompare(b.dueDate);
    });
  }
  // 'default' → preserve original API order

  return result;
}

function ProjectsTasksPanel({ refreshKey }) {
  const [projects,        setProjects]        = useState([]);
  const [selectedId,      setSelectedId]      = useState(null); // null = All Tasks
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

  useEffect(() => { fetchProjects(); }, []);

  // Re-fetch tasks when selected project changes or journal triggers a refresh.
  // eslint-disable-next-line react-hooks/exhaustive-deps
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

  const inboxProject  = projects.find((p) => p.isInboxProject);
  const otherProjects = projects.filter((p) => !p.isInboxProject);

  function selectedName() {
    if (selectedId === null) return 'All Tasks';
    return projects.find((p) => p.id === selectedId)?.name ?? 'Tasks';
  }

  // Derived: filtered + sorted task list for display.
  const displayedTasks = applyFiltersAndSort(tasks, statusFilter, priorityFilter, sortOrder);

  // Is any filter active? Used to distinguish "no tasks in project" from "no matches".
  const isFiltered = statusFilter !== 'all' || priorityFilter !== 'all';

  return (
    <div className="card">
      <div className="card-header">
        <h2 className="card-title">Projects &amp; Tasks</h2>
      </div>

      <div className="projects-tasks-panel">
        {/* ── Project list ────────────────────────────────────────────── */}
        <div className="project-list">
          {/* Pinned header items — always visible */}
          <p className="project-list-header">Projects</p>
          <div
            className={`project-item${selectedId === null ? ' active' : ''}`}
            onClick={() => setSelectedId(null)}
          >
            All Tasks
          </div>
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

          {/* Scrollable project items */}
          <div className="project-list-scroll">
            {loadingProjects && (
              <>
                <div className="skeleton skeleton-row" />
                <div className="skeleton skeleton-row" />
                <div className="skeleton skeleton-row" />
              </>
            )}
            {!loadingProjects && projectsError && (
              <div style={{ padding: '8px 12px' }}>
                <p className="error-inline" style={{ marginBottom: 4 }}>{projectsError}</p>
                <button className="btn-retry" onClick={fetchProjects}>Retry</button>
              </div>
            )}
            {!loadingProjects && !projectsError && otherProjects.map((project) => (
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
        </div>

        {/* ── Task panel ──────────────────────────────────────────────── */}
        <div className="task-panel">
          {/* Pinned title + Todoist link */}
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

          {/* Pinned filter bar — stays visible while task list scrolls */}
          {!loadingTasks && !tasksError && tasks.length > 0 && (
            <div className="task-filter-bar">
              {/* Status tabs */}
              <div className="task-filter-tabs" role="tablist" aria-label="Filter by status">
                {[
                  { value: 'all',  label: 'All' },
                  { value: 'todo', label: 'Todo' },
                  { value: 'done', label: 'Completed' },
                ].map(({ value, label }) => (
                  <button
                    key={value}
                    role="tab"
                    aria-selected={statusFilter === value}
                    className={`task-filter-tab${statusFilter === value ? ' active' : ''}`}
                    onClick={() => setStatusFilter(value)}
                  >
                    {label}
                  </button>
                ))}
              </div>

              {/* Priority + Sort dropdowns */}
              <div className="task-filter-selects">
                <select
                  className="task-filter-select"
                  value={priorityFilter}
                  onChange={(e) => setPriorityFilter(e.target.value)}
                  aria-label="Filter by priority"
                >
                  <option value="all">All priorities</option>
                  <option value="1">P1</option>
                  <option value="2">P2</option>
                  <option value="3">P3</option>
                  <option value="4">P4</option>
                </select>

                <select
                  className="task-filter-select"
                  value={sortOrder}
                  onChange={(e) => setSortOrder(e.target.value)}
                  aria-label="Sort tasks"
                >
                  <option value="default">Default order</option>
                  <option value="priority">Priority</option>
                  <option value="due">Due date</option>
                </select>
              </div>
            </div>
          )}

          {/* Scrollable content area */}
          <div className="task-list-scroll">
            {/* Loading skeletons */}
            {loadingTasks && (
              <>
                {[...Array(5)].map((_, i) => (
                  <div key={i} className="skeleton skeleton-task" />
                ))}
              </>
            )}

            {/* API error */}
            {!loadingTasks && tasksError && (
              <div className="error-banner">
                <span>{tasksError}</span>
                <button className="btn-retry" onClick={fetchTasks}>Retry</button>
              </div>
            )}

            {/* Empty: no tasks in this project at all */}
            {!loadingTasks && !tasksError && tasks.length === 0 && (
              <p className="empty-state">No active tasks in this project.</p>
            )}

            {/* Empty: tasks exist but filters exclude them all */}
            {!loadingTasks && !tasksError && tasks.length > 0 && displayedTasks.length === 0 && (
              <p className="empty-state">
                No tasks match the current filters.{' '}
                {isFiltered && (
                  <button
                    className="task-filter-clear"
                    onClick={() => { setStatusFilter('all'); setPriorityFilter('all'); }}
                  >
                    Clear filters
                  </button>
                )}
              </p>
            )}

            {/* Task list */}
            {!loadingTasks && !tasksError && displayedTasks.length > 0 && (
              <ul className="task-list">
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
              </ul>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default ProjectsTasksPanel;
