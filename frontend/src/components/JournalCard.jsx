import { useState } from 'react';

function JournalCard({ onJournalProcessed }) {
  const [journalText,       setJournalText]       = useState('');
  const [loading,           setLoading]           = useState(false);
  const [error,             setError]             = useState('');
  const [extractionWarning, setExtractionWarning] = useState('');

  async function handleSubmit(event) {
    event.preventDefault();
    setLoading(true);
    setError('');
    setExtractionWarning('');

    try {
      const response = await fetch('/api/journal', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ journalText }),
      });

      if (!response.ok) throw new Error('Failed to submit journal entry.');

      const data = await response.json();
      setJournalText('');

      if (data.extraction_status !== 'ok') {
        // Journal was saved server-side but extraction failed — show warning
        setExtractionWarning(
          data.extraction_error ??
          'Journal saved, but AI extraction is temporarily unavailable.'
        );
        onJournalProcessed('');
      } else {
        onJournalProcessed(data.summary ?? '');
      }
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
      {extractionWarning && (
        <p className="extraction-warning" style={{ marginTop: 8 }}>
          ⚠ {extractionWarning}
        </p>
      )}
    </div>
  );
}

export default JournalCard;
