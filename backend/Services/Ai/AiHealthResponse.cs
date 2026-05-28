// backend/Services/Ai/AiHealthResponse.cs
namespace backend.Services.Ai;

public sealed class AiHealthResponse
{
    public string   Provider           { get; init; } = string.Empty;
    public bool     Configured         { get; init; }
    public bool     Success            { get; init; }
    public int?     StatusCode         { get; init; }
    public long     LatencyMs          { get; init; }
    public DateTime TimestampUtc       { get; init; }
    public string   Message            { get; init; } = string.Empty;
    public string?  RawResponsePreview { get; init; }
}
