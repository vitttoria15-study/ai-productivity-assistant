using System.Text;
using backend.Data;
using backend.Models;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Services.Ai;

public class JournalExtractionService
{
    private readonly AppDbContext          _db;
    private readonly IAiProvider           _ai;
    private readonly ITodoistService       _todoist;
    private readonly ILogger<JournalExtractionService> _log;
    private readonly string                _providerName;

    public JournalExtractionService(
        AppDbContext db,
        IAiProvider ai,
        ITodoistService todoist,
        ILogger<JournalExtractionService> log,
        IOptions<AiOptions> aiOptions)
    {
        _db           = db;
        _ai           = ai;
        _todoist      = todoist;
        _log          = log;
        _providerName = aiOptions.Value.Provider;
    }

    public async Task<JournalExtractionResponse> ExtractAsync(
        string journalText, CancellationToken ct = default)
    {
        // ── Step 1: Always save the journal entry first ──────────────────────
        var entry = new JournalEntry
        {
            Content   = journalText,
            CreatedAt = DateTime.UtcNow,
            Summary   = string.Empty,
        };
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        // ── Step 2: Fetch active tasks for context (soft failure) ────────────
        IReadOnlyList<TodoistTask>? activeTasks = null;
        try
        {
            activeTasks = await _todoist.GetActiveTasksAsync(ct: ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Failed to fetch active Todoist tasks for prompt context. " +
                "Auto-close disabled for this extraction.");
        }

        // ── Step 3: Call AI provider ─────────────────────────────────────────
        string rawResponse;
        try
        {
            var (system, user) = BuildPrompt(journalText, activeTasks);
            rawResponse = await _ai.CompleteAsync(system, user, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "AI provider call failed.");
            return Failed(_providerName,
                "AI extraction is temporarily unavailable. Your journal entry was saved.");
        }

        // ── Step 4: Validate ─────────────────────────────────────────────────
        if (!ExtractionValidator.TryParse(rawResponse, out var result, out var parseError))
        {
            _log.LogWarning("AI response validation failed: {Error}", parseError);
            return InvalidResponse(_providerName,
                "AI extraction returned an unexpected response format.");
        }

        // ── Step 5: Route to Todoist ──────────────────────────────────────────
        // Build close dictionary only when active tasks were fetched successfully
        Dictionary<string, string>? closeDict = activeTasks != null
            ? activeTasks.ToDictionary(
                t => t.Content,
                t => t.Id,
                StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var taskTitle in result!.NewTasks)
        {
            try
            {
                await _todoist.CreateTaskAsync(
                    new CreateTaskRequest(taskTitle, null, Priority: 1), ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Failed to create Todoist task '{Title}'. Skipping.", taskTitle);
            }
        }

        if (closeDict != null)
        {
            foreach (var completedTitle in result.CompletedTasks)
            {
                if (closeDict.TryGetValue(completedTitle, out var taskId))
                {
                    try
                    {
                        await _todoist.CloseTaskAsync(taskId, ct);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex,
                            "Failed to close Todoist task '{Id}'. Skipping.", taskId);
                    }
                }
                else
                {
                    _log.LogWarning(
                        "Completed task '{Title}' not found in active task list. Skipping.",
                        completedTitle);
                }
            }
        }

        // ── Steps 6 + 7: Persist blockers + update summary ───────────────────
        foreach (var desc in result.Blockers)
        {
            _db.ExtractedBlockers.Add(new ExtractedBlocker
            {
                JournalEntryId = entry.Id,
                Description    = desc,
                CreatedAt      = DateTime.UtcNow,
            });
        }
        entry.Summary = result.Summary;
        await _db.SaveChangesAsync(ct);

        return new JournalExtractionResponse(
            ExtractionStatus: "ok",
            ExtractionError:  null,
            Provider:         _providerName,
            CompletedTasks:   result.CompletedTasks,
            NewTasks:         result.NewTasks,
            Blockers:         result.Blockers,
            Priorities:       result.Priorities,
            Summary:          result.Summary);
    }

    // ── Prompt builder ────────────────────────────────────────────────────────

    private static (string system, string user) BuildPrompt(
        string journalText,
        IReadOnlyList<TodoistTask>? activeTasks)
    {
        const string system = """
            You are a productivity assistant. Extract structured information from the user's journal entry.
            Return ONLY valid JSON. No markdown. No explanations. No code blocks.

            Schema:
            {
              "completed_tasks": ["string"],
              "new_tasks":       ["string"],
              "blockers":        ["string"],
              "priorities":      ["string"],
              "summary":         "string"
            }

            Rules:
            - completed_tasks: titles of tasks the user says they finished. Match exactly from the
              provided Active tasks list. If no active tasks list is provided, return [].
            - new_tasks: tasks the user mentions planning to do that are NOT in the active tasks list.
            - blockers: obstacles, blockers, or dependencies the user is waiting on.
            - priorities: anything the user flags as important or urgent.
            - summary: 1-2 sentences describing the day's work.
            - Return empty arrays if nothing is found. Never omit a field.
            """;

        var sb = new StringBuilder();
        if (activeTasks is { Count: > 0 })
        {
            sb.AppendLine("Active tasks:");
            foreach (var t in activeTasks)
                sb.AppendLine($"- {t.Content}");
            sb.AppendLine();
        }
        sb.AppendLine("Journal entry:");
        sb.Append(journalText);

        return (system, sb.ToString());
    }

    // ── Error response helpers ────────────────────────────────────────────────

    private static JournalExtractionResponse Failed(string provider, string msg) =>
        new("failed", msg, provider, [], [], [], [], string.Empty);

    private static JournalExtractionResponse InvalidResponse(string provider, string msg) =>
        new("invalid_response", msg, provider, [], [], [], [], string.Empty);
}
