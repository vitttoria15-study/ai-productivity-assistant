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

        // ── Step 2a: Fetch Todoist projects for task routing (soft failure) ─────
        IReadOnlyList<TodoistProject>? projects = null;
        try
        {
            projects = await _todoist.GetProjectsAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Failed to fetch Todoist projects. Project routing disabled for this extraction.");
        }

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
            var (system, user) = BuildPrompt(journalText, activeTasks, projects);
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

        // ── Build project-name → ID lookup (first match wins for duplicates) ─────
        var projectLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (projects is not null)
        {
            foreach (var proj in projects)
            {
                if (!projectLookup.TryAdd(proj.Name, proj.Id))
                    _log.LogWarning(
                        "Duplicate Todoist project name '{Name}' (case-insensitive). First match wins.",
                        proj.Name);
            }
        }

        // ── Routing guard ─────────────────────────────────────────────────────────
        var useRoutedPath = result!.RoutedTasks is { Length: > 0 };
        if (useRoutedPath && result.RoutedTasks!.Length != result.NewTasks.Length)
        {
            _log.LogWarning(
                "routed_tasks length ({RoutedCount}) != new_tasks length ({NewCount}). " +
                "Falling back to Inbox creation.",
                result.RoutedTasks.Length, result.NewTasks.Length);
            useRoutedPath = false;
        }

        if (useRoutedPath)
        {
            for (var i = 0; i < result.NewTasks.Length; i++)
            {
                var taskTitle   = result.NewTasks[i];       // authoritative title
                var routedEntry = result.RoutedTasks![i];   // routing metadata only

                if (routedEntry.Title != taskTitle)
                    _log.LogWarning(
                        "routed_tasks[{Index}].Title '{Routed}' != new_tasks[{Index}] '{Original}'. " +
                        "Using new_tasks title.",
                        i, routedEntry.Title, i, taskTitle);

                string? projectId = null;
                if (routedEntry.Project is not null)
                {
                    if (projectLookup.TryGetValue(routedEntry.Project, out var pid))
                        projectId = pid;
                    else
                        _log.LogWarning(
                            "AI selected project '{Name}' which is not in the Todoist project list. " +
                            "Task will go to Inbox.",
                            routedEntry.Project);
                }

                try
                {
                    await _todoist.CreateTaskAsync(
                        new CreateTaskRequest(taskTitle, projectId, Priority: 1), ct);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex,
                        "Failed to create Todoist task '{Title}'. Skipping.", taskTitle);
                }
            }
        }
        else
        {
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
        IReadOnlyList<TodoistTask>?    activeTasks,
        IReadOnlyList<TodoistProject>? projects)
    {
        const string systemBase = """
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

        var hasProjects = projects is { Count: > 0 };

        var systemPrompt = hasProjects
            ? systemBase + """

                If a list of Todoist project names is provided in the user message, you MUST also
                return a routed_tasks array. It must have the same length as new_tasks and be in the
                same order (routed_tasks[i] describes new_tasks[i]). Each item must contain:
                - "title": the task title, identical to the corresponding entry in new_tasks.
                - "project": one of the provided project names verbatim, or null if the journal
                  entry does not clearly name a project for this task.
                Do NOT invent project names. Only use names from the provided list.
                Updated schema when projects are provided:
                {
                  "completed_tasks": ["string"],
                  "new_tasks":       ["string"],
                  "routed_tasks":    [{"title": "string", "project": "string or null"}],
                  "blockers":        ["string"],
                  "priorities":      ["string"],
                  "summary":         "string"
                }
                """
            : systemBase;

        var sb = new StringBuilder();

        if (hasProjects)
        {
            sb.AppendLine("Available Todoist projects:");
            foreach (var p in projects!)
                sb.AppendLine($"- {p.Name}");
            sb.AppendLine();
        }

        if (activeTasks is { Count: > 0 })
        {
            sb.AppendLine("Active tasks:");
            foreach (var t in activeTasks)
                sb.AppendLine($"- {t.Content}");
            sb.AppendLine();
        }

        sb.AppendLine("Journal entry:");
        sb.Append(journalText);

        return (systemPrompt, sb.ToString());
    }

    // ── Error response helpers ────────────────────────────────────────────────

    private static JournalExtractionResponse Failed(string provider, string msg) =>
        new("failed", msg, provider, [], [], [], [], string.Empty);

    private static JournalExtractionResponse InvalidResponse(string provider, string msg) =>
        new("invalid_response", msg, provider, [], [], [], [], string.Empty);
}
