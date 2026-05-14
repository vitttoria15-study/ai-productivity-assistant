namespace backend.Models;

public class AutomationLog
{
    public int Id { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
