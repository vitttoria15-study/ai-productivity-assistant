using System.Text.Json;

namespace backend.Services.Ai;

public static class ExtractionValidator
{
    private const int MaxItemLength    = 500;
    private const int MaxSummaryLength = 1000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Attempts to deserialise and validate the raw JSON string from an AI provider.
    /// Returns <c>true</c> on success and populates <paramref name="result"/>.
    /// Returns <c>false</c> on any hard failure and populates <paramref name="error"/>.
    /// </summary>
    public static bool TryParse(
        string rawJson,
        out ExtractionResult? result,
        out string? error)
    {
        result = null;
        error  = null;

        try
        {
            result = JsonSerializer.Deserialize<ExtractionResult>(rawJson, JsonOpts);
        }
        catch (JsonException ex)
        {
            error = $"AI provider returned invalid JSON: {ex.Message}";
            return false;
        }

        if (result is null)
        {
            error = "AI provider returned a null response.";
            return false;
        }

        // Required-field checks (null guards for missing JSON properties)
        if (result.CompletedTasks is null || result.NewTasks is null ||
            result.Blockers       is null || result.Priorities is null ||
            result.Summary        is null)
        {
            error = "AI response is missing one or more required fields " +
                    "(completed_tasks, new_tasks, blockers, priorities, summary).";
            return false;
        }

        // Length checks across all array items
        var allItems = result.CompletedTasks
            .Concat(result.NewTasks)
            .Concat(result.Blockers)
            .Concat(result.Priorities);

        foreach (var item in allItems)
        {
            if (item.Length > MaxItemLength)
            {
                error = $"AI response contains an item exceeding {MaxItemLength} characters.";
                return false;
            }
        }

        if (result.Summary.Length > MaxSummaryLength)
        {
            error = $"AI response summary exceeds {MaxSummaryLength} characters.";
            return false;
        }

        return true;
    }
}
