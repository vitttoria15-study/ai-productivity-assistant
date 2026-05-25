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
