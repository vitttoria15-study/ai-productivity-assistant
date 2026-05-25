import { useState } from 'react';
import Sidebar            from './components/Sidebar';
import JournalCard        from './components/JournalCard';
import SummaryCard        from './components/SummaryCard';
import ProjectsTasksPanel from './components/ProjectsTasksPanel';
import PlaceholderCard    from './components/PlaceholderCard';

function App() {
  const [activeSection, setActiveSection] = useState('dashboard');
  const [summary,       setSummary]       = useState('');
  // Incremented after each successful journal submission to trigger a task reload
  // in ProjectsTasksPanel without callback-threading through intermediate components.
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
        {/* Top row: Journal entry + AI extraction result */}
        <div className="dashboard-top-row">
          <JournalCard onJournalProcessed={handleJournalProcessed} />
          <SummaryCard summary={summary} />
        </div>

        {/* Center row: Projects & Tasks — primary demo feature, visually dominant */}
        <ProjectsTasksPanel refreshKey={refreshKey} />

        {/* Bottom row: demo placeholder cards */}
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
                { time: '2h ago',    text: 'Journal entry processed',        icon: '✏' },
                { time: '3h ago',    text: 'Task "Deploy hotfix" completed', icon: '✓' },
                { time: '5h ago',    text: 'Task "Review PR #42" created',   icon: '+' },
                { time: 'Yesterday', text: 'Journal entry processed',        icon: '✏' },
                { time: 'Yesterday', text: '2 tasks moved to Inbox',         icon: '↓' },
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
