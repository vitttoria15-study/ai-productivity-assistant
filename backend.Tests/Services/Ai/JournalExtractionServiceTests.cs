using backend.Data;
using backend.Models;
using backend.Services.Ai;
using backend.Services.Ai.Providers;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace backend.Tests.Services.Ai;

public class JournalExtractionServiceTests : IDisposable
{
    private readonly AppDbContext          _db;
    private readonly Mock<IAiProvider>     _aiMock     = new();
    private readonly Mock<ITodoistService> _todoistMock = new();

    public JournalExtractionServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);
    }

    public void Dispose() => _db.Dispose();

    private JournalExtractionService BuildService(string provider = "mock")
    {
        var aiOptions = Options.Create(new AiOptions { Provider = provider });
        return new JournalExtractionService(
            _db,
            _aiMock.Object,
            _todoistMock.Object,
            NullLogger<JournalExtractionService>.Instance,
            aiOptions);
    }

    // ── Scenario 1: Happy path ───────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_ValidResponse_SavesJournalAndBlockersAndReturnsOk()
    {
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());

        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("""
                {
                  "completed_tasks": [],
                  "new_tasks": ["Write tests"],
                  "blockers": ["Missing env vars"],
                  "priorities": [],
                  "summary": "Good progress today."
                }
                """);

        _todoistMock
            .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
            .ReturnsAsync(new TodoistTask("id1", "Write tests", null, "proj1", 1, null, null));

        var svc = BuildService();
        var result = await svc.ExtractAsync("Worked on tests today.");

        Assert.Equal("ok", result.ExtractionStatus);
        Assert.Null(result.ExtractionError);
        Assert.Equal("mock", result.Provider);
        Assert.Single(result.NewTasks);
        Assert.Equal("Write tests", result.NewTasks[0]);
        Assert.Single(result.Blockers);
        Assert.Equal("Good progress today.", result.Summary);

        // Journal was saved
        var journal = await _db.JournalEntries.SingleAsync();
        Assert.Equal("Worked on tests today.", journal.Content);
        Assert.Equal("Good progress today.", journal.Summary);

        // Blocker was persisted
        var blocker = await _db.ExtractedBlockers.SingleAsync();
        Assert.Equal("Missing env vars", blocker.Description);
        Assert.Equal(journal.Id, blocker.JournalEntryId);

        // CreateTaskAsync called once
        _todoistMock.Verify(
            s => s.CreateTaskAsync(
                It.Is<CreateTaskRequest>(r => r.Content == "Write tests"),
                default),
            Times.Once);
    }

    // ── Scenario 2: Provider failure ────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_ProviderThrows_SavesJournalAndReturnsFailed()
    {
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());

        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var svc = BuildService("dial");
        var result = await svc.ExtractAsync("Some journal text.");

        Assert.Equal("failed", result.ExtractionStatus);
        Assert.NotNull(result.ExtractionError);
        Assert.Equal("dial", result.Provider);
        Assert.Empty(result.NewTasks);

        // Journal still saved, summary empty
        var journal = await _db.JournalEntries.SingleAsync();
        Assert.Equal("Some journal text.", journal.Content);
        Assert.Equal(string.Empty, journal.Summary);

        // No Todoist mutations
        _todoistMock.Verify(
            s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default),
            Times.Never);
    }

    // ── Scenario 3: Hard validation failure ─────────────────────────────────

    [Fact]
    public async Task ExtractAsync_InvalidAiResponse_ReturnsInvalidResponse()
    {
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());

        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("not json at all");

        var svc = BuildService();
        var result = await svc.ExtractAsync("Some journal text.");

        Assert.Equal("invalid_response", result.ExtractionStatus);
        Assert.NotNull(result.ExtractionError);

        // No Todoist mutations
        _todoistMock.Verify(
            s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default),
            Times.Never);
    }

    // ── Scenario 4: Active task fetch failure — auto-close disabled ──────────

    [Fact]
    public async Task ExtractAsync_ActiveTaskFetchFails_SkipsCloseButCreatesNewTasks()
    {
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ThrowsAsync(new HttpRequestException("Todoist unavailable"));

        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("""
                {
                  "completed_tasks": ["Some Task"],
                  "new_tasks": ["New Work"],
                  "blockers": [],
                  "priorities": [],
                  "summary": "Did some work."
                }
                """);

        _todoistMock
            .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
            .ReturnsAsync(new TodoistTask("id2", "New Work", null, "proj1", 1, null, null));

        var svc = BuildService();
        var result = await svc.ExtractAsync("Did some work, completed Some Task.");

        Assert.Equal("ok", result.ExtractionStatus);

        // CloseTaskAsync must NOT have been called (no task context available)
        _todoistMock.Verify(
            s => s.CloseTaskAsync(It.IsAny<string>(), default),
            Times.Never);

        // CreateTaskAsync still called for new tasks
        _todoistMock.Verify(
            s => s.CreateTaskAsync(
                It.Is<CreateTaskRequest>(r => r.Content == "New Work"),
                default),
            Times.Once);
    }

    // ── Project-aware prompt injection ─────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_WithProjects_InjectsProjectNamesIntoUserMessage()
    {
        _todoistMock
            .Setup(s => s.GetProjectsAsync(default))
            .ReturnsAsync(new List<TodoistProject>
            {
                new("work-id", "Work", "blue", 1, false),
                new("personal-id", "Personal", "green", 2, false),
            });
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());

        string capturedUser = string.Empty;
        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Callback<string, string, CancellationToken>((_, user, _) => capturedUser = user)
            .ReturnsAsync("""
                {"completed_tasks":[],"new_tasks":[],"blockers":[],"priorities":[],"summary":""}
                """);

        var svc = BuildService();
        await svc.ExtractAsync("Quick journal entry.");

        Assert.Contains("Available Todoist projects:", capturedUser);
        Assert.Contains("- Work", capturedUser);
        Assert.Contains("- Personal", capturedUser);
    }

    // ── Routing: matched project ────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_RoutedTasksWithMatchingProject_CreatesTaskInProject()
    {
        _todoistMock
            .Setup(s => s.GetProjectsAsync(default))
            .ReturnsAsync(new List<TodoistProject>
            {
                new("work-id", "Work", "blue", 1, false),
                new("personal-id", "Personal", "green", 2, false),
            });
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());
        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("""
                {
                  "completed_tasks": [],
                  "new_tasks": ["Write spec"],
                  "routed_tasks": [{"title": "Write spec", "project": "Work"}],
                  "blockers": [],
                  "priorities": [],
                  "summary": "Worked on spec."
                }
                """);
        _todoistMock
            .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
            .ReturnsAsync(new TodoistTask("id1", "Write spec", null, "work-id", 1, null, null));

        var svc = BuildService();
        var result = await svc.ExtractAsync("Worked on spec for Work project.");

        Assert.Equal("ok", result.ExtractionStatus);
        _todoistMock.Verify(
            s => s.CreateTaskAsync(
                It.Is<CreateTaskRequest>(r => r.Content == "Write spec" && r.ProjectId == "work-id"),
                default),
            Times.Once);
    }

    // ── Routing: unknown project → Inbox ────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_RoutedTasksWithUnknownProject_FallsBackToInbox()
    {
        _todoistMock
            .Setup(s => s.GetProjectsAsync(default))
            .ReturnsAsync(new List<TodoistProject>
            {
                new("work-id", "Work", "blue", 1, false),
            });
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());
        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("""
                {
                  "completed_tasks": [],
                  "new_tasks": ["Buy milk"],
                  "routed_tasks": [{"title": "Buy milk", "project": "Shopping"}],
                  "blockers": [],
                  "priorities": [],
                  "summary": "Grocery run needed."
                }
                """);
        _todoistMock
            .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
            .ReturnsAsync(new TodoistTask("id1", "Buy milk", null, "inbox-id", 1, null, null));

        var svc = BuildService();
        var result = await svc.ExtractAsync("Need to buy milk.");

        Assert.Equal("ok", result.ExtractionStatus);
        _todoistMock.Verify(
            s => s.CreateTaskAsync(
                It.Is<CreateTaskRequest>(r => r.Content == "Buy milk" && r.ProjectId == null),
                default),
            Times.Once);
    }

    // ── Routing: routed_tasks absent → fallback ─────────────────────────────

    [Fact]
    public async Task ExtractAsync_RoutedTasksAbsent_FallsBackToNewTasksInbox()
    {
        _todoistMock
            .Setup(s => s.GetProjectsAsync(default))
            .ReturnsAsync(new List<TodoistProject>
            {
                new("work-id", "Work", "blue", 1, false),
            });
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());
        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("""
                {
                  "completed_tasks": [],
                  "new_tasks": ["Deploy service"],
                  "blockers": [],
                  "priorities": [],
                  "summary": "Planning deployment."
                }
                """);
        _todoistMock
            .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
            .ReturnsAsync(new TodoistTask("id1", "Deploy service", null, "inbox-id", 1, null, null));

        var svc = BuildService();
        var result = await svc.ExtractAsync("Need to deploy service.");

        Assert.Equal("ok", result.ExtractionStatus);
        _todoistMock.Verify(
            s => s.CreateTaskAsync(
                It.Is<CreateTaskRequest>(r => r.Content == "Deploy service" && r.ProjectId == null),
                default),
            Times.Once);
    }

    // ── Routing: count mismatch → fallback ──────────────────────────────────

    [Fact]
    public async Task ExtractAsync_RoutedTasksCountMismatch_FallsBackToInbox()
    {
        _todoistMock
            .Setup(s => s.GetProjectsAsync(default))
            .ReturnsAsync(new List<TodoistProject>
            {
                new("work-id", "Work", "blue", 1, false),
            });
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());
        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("""
                {
                  "completed_tasks": [],
                  "new_tasks": ["Task A", "Task B"],
                  "routed_tasks": [{"title": "Task A", "project": "Work"}],
                  "blockers": [],
                  "priorities": [],
                  "summary": "Two tasks."
                }
                """);
        _todoistMock
            .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
            .ReturnsAsync(new TodoistTask("id1", "Task A", null, "inbox-id", 1, null, null));

        var svc = BuildService();
        var result = await svc.ExtractAsync("Need to do Task A and Task B.");

        Assert.Equal("ok", result.ExtractionStatus);
        _todoistMock.Verify(
            s => s.CreateTaskAsync(
                It.Is<CreateTaskRequest>(r => r.ProjectId == null),
                default),
            Times.Exactly(2));
    }

    // ── Routing: projects fetch fails → Inbox ───────────────────────────────

    [Fact]
    public async Task ExtractAsync_ProjectsFetchFails_CreatesTasksInInbox()
    {
        _todoistMock
            .Setup(s => s.GetProjectsAsync(default))
            .ThrowsAsync(new HttpRequestException("Todoist unavailable"));
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());
        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("""
                {
                  "completed_tasks": [],
                  "new_tasks": ["Write report"],
                  "blockers": [],
                  "priorities": [],
                  "summary": "Writing report."
                }
                """);
        _todoistMock
            .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
            .ReturnsAsync(new TodoistTask("id1", "Write report", null, "inbox-id", 1, null, null));

        var svc = BuildService();
        var result = await svc.ExtractAsync("Need to write report.");

        Assert.Equal("ok", result.ExtractionStatus);
        _todoistMock.Verify(
            s => s.CreateTaskAsync(
                It.Is<CreateTaskRequest>(r => r.Content == "Write report" && r.ProjectId == null),
                default),
            Times.Once);
    }

    // ── Routing: duplicate project names → first match wins ─────────────────

    [Fact]
    public async Task ExtractAsync_DuplicateProjectNames_UsesFirstMatch()
    {
        _todoistMock
            .Setup(s => s.GetProjectsAsync(default))
            .ReturnsAsync(new List<TodoistProject>
            {
                new("work-id-1", "Work",  "blue",  1, false),
                new("work-id-2", "WORK",  "green", 2, false),
            });
        _todoistMock
            .Setup(s => s.GetActiveTasksAsync(null, default))
            .ReturnsAsync(new List<TodoistTask>());
        _aiMock
            .Setup(p => p.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("""
                {
                  "completed_tasks": [],
                  "new_tasks": ["Write spec"],
                  "routed_tasks": [{"title": "Write spec", "project": "Work"}],
                  "blockers": [],
                  "priorities": [],
                  "summary": "Spec work."
                }
                """);
        _todoistMock
            .Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), default))
            .ReturnsAsync(new TodoistTask("id1", "Write spec", null, "work-id-1", 1, null, null));

        var svc = BuildService();
        var result = await svc.ExtractAsync("Write spec for Work.");

        Assert.Equal("ok", result.ExtractionStatus);
        _todoistMock.Verify(
            s => s.CreateTaskAsync(
                It.Is<CreateTaskRequest>(r => r.Content == "Write spec" && r.ProjectId == "work-id-1"),
                default),
            Times.Once);
    }
}
