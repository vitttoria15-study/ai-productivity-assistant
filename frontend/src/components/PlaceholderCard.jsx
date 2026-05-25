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
