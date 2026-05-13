import { useEffect, useState } from 'react';

function App() {
  const [journalText, setJournalText] = useState('');
  const [summary, setSummary] = useState('');
  const [tasks, setTasks] = useState([]);
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [loadingTasks, setLoadingTasks] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    loadTasks();
  }, []);

  async function loadTasks() {
    setLoadingTasks(true);
    setError('');

    try {
      const response = await fetch('/api/tasks');
      if (!response.ok) {
        throw new Error('Failed to load tasks.');
      }

      const data = await response.json();
      setTasks(data);
    } catch {
      setError('Failed to load tasks.');
    } finally {
      setLoadingTasks(false);
    }
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setLoadingSummary(true);
    setError('');

    try {
      const response = await fetch('/api/journal', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ journalText })
      });

      if (!response.ok) {
        throw new Error('Failed to submit journal entry.');
      }

      const data = await response.json();
      setSummary(data.summary ?? '');
      setJournalText('');
      await loadTasks();
    } catch {
      setError('Failed to submit journal entry.');
    } finally {
      setLoadingSummary(false);
    }
  }

  return (
    <main className="app-shell">
      <section className="card">
        <h1>AI Productivity Assistant</h1>
        <form onSubmit={handleSubmit}>
          <label htmlFor="journal">Journal entry</label>
          <textarea
            id="journal"
            value={journalText}
            onChange={(event) => setJournalText(event.target.value)}
            placeholder="Write what you completed, what is blocked, and what needs to happen next."
            rows="8"
          />
          <button type="submit" disabled={loadingSummary || !journalText.trim()}>
            {loadingSummary ? 'Submitting...' : 'Submit'}
          </button>
        </form>
        {error ? <p className="error">{error}</p> : null}
      </section>

      <section className="card">
        <h2>Summary</h2>
        {summary ? <p>{summary}</p> : <p className="muted">No summary yet.</p>}
      </section>

      <section className="card">
        <h2>Tasks</h2>
        {loadingTasks ? <p className="muted">Loading tasks...</p> : null}
        {!loadingTasks && tasks.length === 0 ? <p className="muted">No tasks yet.</p> : null}
        <ul className="task-list">
          {tasks.map((task) => (
            <li key={task.id}>
              <span>{task.title}</span>
              <span className="status">{task.status}</span>
            </li>
          ))}
        </ul>
      </section>
    </main>
  );
}

export default App;