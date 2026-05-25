const NAV_ITEMS = [
  { id: 'dashboard',  label: 'Dashboard',   icon: '⊞' },
  { id: 'journal',    label: 'Journal',      icon: '✏' },
  { id: 'tasks',      label: 'Tasks',        icon: '✓' },
  { id: 'projects',   label: 'Projects',     icon: '⬡' },
  { id: 'assistant',  label: 'AI Assistant', icon: '✦' },
  { id: 'summaries',  label: 'Summaries',    icon: '◈' },
  { id: 'automation', label: 'Automation',   icon: '⚙' },
  { id: 'settings',   label: 'Settings',     icon: '≡' },
];

function Sidebar({ activeSection, onSectionChange }) {
  return (
    <aside className="sidebar">
      {/* Branding */}
      <div className="sidebar-brand">
        <p className="sidebar-brand-title">AI Productivity</p>
        <p className="sidebar-brand-sub">Assistant</p>
      </div>

      {/* Navigation — UI state only, no URL routing */}
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

      {/* Todoist connection status */}
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
