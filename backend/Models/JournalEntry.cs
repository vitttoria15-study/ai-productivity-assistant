namespace backend.Models;

public class JournalEntry
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Summary { get; set; } = string.Empty;

    public ICollection<TaskItem>         TaskItems         { get; set; } = new List<TaskItem>();
    public ICollection<ExtractedBlocker> ExtractedBlockers { get; set; } = new List<ExtractedBlocker>();
}