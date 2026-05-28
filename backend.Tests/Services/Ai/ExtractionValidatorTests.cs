using backend.Services.Ai;
using Xunit;

namespace backend.Tests.Services.Ai;

public class ExtractionValidatorTests
{
    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_ValidJson_ReturnsTrueAndResult()
    {
        const string json = """
            {
              "completed_tasks": ["Task A"],
              "new_tasks": ["Task B"],
              "blockers": ["blocker"],
              "priorities": ["priority"],
              "summary": "A short summary."
            }
            """;

        var ok = ExtractionValidator.TryParse(json, out var result, out var error);

        Assert.True(ok);
        Assert.NotNull(result);
        Assert.Null(error);
        Assert.Equal("Task A", result!.CompletedTasks[0]);
        Assert.Equal("A short summary.", result.Summary);
    }

    // ── Hard failures ────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalse()
    {
        var ok = ExtractionValidator.TryParse("not json", out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_MissingField_ReturnsFalse()
    {
        const string json = """{"completed_tasks":[],"new_tasks":[],"blockers":[],"priorities":[]}""";

        var ok = ExtractionValidator.TryParse(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_ItemExceeds500Chars_ReturnsFalse()
    {
        var longItem = new string('x', 501);
        var json = $$"""
            {
              "completed_tasks": [],
              "new_tasks": ["{{longItem}}"],
              "blockers": [],
              "priorities": [],
              "summary": "ok"
            }
            """;

        var ok = ExtractionValidator.TryParse(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_SummaryExceeds1000Chars_ReturnsFalse()
    {
        var longSummary = new string('x', 1001);
        var json = $$"""
            {
              "completed_tasks": [],
              "new_tasks": [],
              "blockers": [],
              "priorities": [],
              "summary": "{{longSummary}}"
            }
            """;

        var ok = ExtractionValidator.TryParse(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_EmptyArrays_ReturnsTrue()
    {
        const string json = """
            {"completed_tasks":[],"new_tasks":[],"blockers":[],"priorities":[],"summary":""}
            """;

        var ok = ExtractionValidator.TryParse(json, out var result, out _);

        Assert.True(ok);
        Assert.Empty(result!.CompletedTasks);
        Assert.Equal(string.Empty, result.Summary);
    }

    // ── RoutedTasks — structural deserialization ────────────────────────────

    [Fact]
    public void TryParse_RoutedTasksAbsent_ReturnsTrue()
    {
        const string json = """
            {"completed_tasks":[],"new_tasks":["Task A"],"blockers":[],"priorities":[],"summary":""}
            """;

        var ok = ExtractionValidator.TryParse(json, out var result, out _);

        Assert.True(ok);
        Assert.Null(result!.RoutedTasks);
    }

    [Fact]
    public void TryParse_RoutedTasksPresent_Valid_ReturnsTrue()
    {
        const string json = """
            {
              "completed_tasks": [],
              "new_tasks": ["Buy milk"],
              "routed_tasks": [{"title": "Buy milk", "project": "Shopping"}],
              "blockers": [],
              "priorities": [],
              "summary": ""
            }
            """;

        var ok = ExtractionValidator.TryParse(json, out var result, out _);

        Assert.True(ok);
        Assert.NotNull(result!.RoutedTasks);
        Assert.Single(result.RoutedTasks!);
        Assert.Equal("Buy milk", result.RoutedTasks![0].Title);
        Assert.Equal("Shopping", result.RoutedTasks[0].Project);
    }

    [Fact]
    public void TryParse_RoutedTasksProjectNull_ReturnsTrue()
    {
        const string json = """
            {
              "completed_tasks": [],
              "new_tasks": ["Write report"],
              "routed_tasks": [{"title": "Write report", "project": null}],
              "blockers": [],
              "priorities": [],
              "summary": ""
            }
            """;

        var ok = ExtractionValidator.TryParse(json, out _, out _);

        Assert.True(ok);
    }

    // ── RoutedTasks — length validation ────────────────────────────────────

    [Fact]
    public void TryParse_RoutedTasksTitleExceeds500Chars_ReturnsFalse()
    {
        var longTitle = new string('x', 501);
        var json = $$"""
            {
              "completed_tasks": [],
              "new_tasks": ["ok"],
              "routed_tasks": [{"title": "{{longTitle}}", "project": null}],
              "blockers": [],
              "priorities": [],
              "summary": ""
            }
            """;

        var ok = ExtractionValidator.TryParse(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_RoutedTasksProjectExceeds200Chars_ReturnsFalse()
    {
        var longProject = new string('x', 201);
        var json = $$"""
            {
              "completed_tasks": [],
              "new_tasks": ["ok"],
              "routed_tasks": [{"title": "ok", "project": "{{longProject}}"}],
              "blockers": [],
              "priorities": [],
              "summary": ""
            }
            """;

        var ok = ExtractionValidator.TryParse(json, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }
}
