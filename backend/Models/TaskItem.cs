namespace backend.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int JournalEntryId { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;
}