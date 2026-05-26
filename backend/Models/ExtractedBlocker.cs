namespace backend.Models;

public class ExtractedBlocker
{
    public int    Id             { get; set; }
    public int    JournalEntryId { get; set; }
    public string Description    { get; set; } = string.Empty;
    public DateTime CreatedAt    { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;
}
