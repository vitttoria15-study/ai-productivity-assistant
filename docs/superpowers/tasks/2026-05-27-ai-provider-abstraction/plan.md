# AI Provider Abstraction — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development`
> (recommended) or `superpowers:executing-plans` to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hardcoded `MockExtractionResponse` in `JournalController` with a real AI
extraction pipeline that supports EPAM DIAL, Ollama, and Mock providers, validates AI output
before touching Todoist, and degrades gracefully on provider failure.

**Architecture:** `JournalExtractionService` orchestrates the full pipeline — saving the journal
entry first, building a context-aware prompt, calling `IAiProvider`, validating the JSON
response, routing to `ITodoistService`, and persisting blockers + summary to SQLite. The
provider layer is a thin HTTP transport; all domain logic stays in the service. Provider
selection is driven by `"Ai:Provider"` config value; secrets flow through .NET user-secrets
or environment variables only.

**Tech Stack:** .NET 8 / ASP.NET Core, `System.Net.Http.Json` (built-in), Entity Framework Core
8 + SQLite, React 18 + Vite, xUnit 2 + Moq 4 + EF InMemory (tests).

---

## File Map

### New files

| Path | Responsibility |
|---|---|
| `backend.Tests/Services/Ai/ExtractionValidatorTests.cs` | Unit tests — hard/soft validation rules |
| `backend.Tests/Services/Ai/JournalExtractionServiceTests.cs` | Unit tests — full pipeline scenarios |
| `backend.Tests/Services/Ai/DialAiProviderTests.cs` | Unit tests — DIAL HTTP request format |
| `backend.Tests/Services/Ai/OllamaProviderTests.cs` | Unit tests — Ollama HTTP request format |
| `backend/Models/ExtractedBlocker.cs` | SQLite model |
| `backend/Models/JournalExtractionResponse.cs` | API response record |
| `backend/Services/Ai/IAiProvider.cs` | Provider interface |
| `backend/Services/Ai/AiOptions.cs` | Strongly-typed config |
| `backend/Services/Ai/ExtractionResult.cs` | Deserialized AI JSON record |
| `backend/Services/Ai/ExtractionValidator.cs` | Hard + soft validation logic |
| `backend/Services/Ai/JournalExtractionService.cs` | Orchestrator |
| `backend/Services/Ai/Providers/MockAiProvider.cs` | Mock implementation |
| `backend/Services/Ai/Providers/DialAiProvider.cs` | EPAM DIAL implementation |
| `backend/Services/Ai/Providers/DialAuthHandler.cs` | Bearer-token delegating handler |
| `backend/Services/Ai/Providers/OllamaProvider.cs` | Ollama implementation |

### Modified files

| Path | Change |
|---|---|
| `backend/Data/AppDbContext.cs` | Add `DbSet<ExtractedBlocker>` + navigation config |
| `backend/Controllers/JournalController.cs` | Inject service, remove inline mock, use new response type |
| `backend/Program.cs` | Register `AiOptions`, DI-select `IAiProvider`, add startup warning |
| `backend/appsettings.json` | Add `"Ai"` section (no secrets) |
| `frontend/src/components/JournalCard.jsx` | Check `extraction_status`, show degraded-state banner |

---

## Task 1 — Prepare existing test project

**Test-first:** no — this task extends existing test infrastructure.

> `backend.Tests/` already exists with xUnit 2.5.3, Moq 4.20.72, and a project reference to
> `backend`. It is missing only `Microsoft.EntityFrameworkCore.InMemory`, which is needed for
> in-memory database fixtures in Task 7. Do NOT create a second test project.

**Files:**
- Modify: `backend.Tests/backend.Tests.csproj` (add EF InMemory package)

- [ ] **Step 1: Add EF InMemory to the existing test project**

  ```
  dotnet add backend.Tests/backend.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 8.0.10
  ```

- [ ] **Step 2: Create the Services/Ai test directory**

  Create the folder `backend.Tests/Services/Ai/` (no files yet — just the directory).

- [ ] **Step 3: Verify the existing test suite still passes**

  ```
  dotnet test backend.Tests/backend.Tests.csproj
  ```

  Expected: all existing tests pass (check `backend.Tests/Services/` and `Controllers/` tests).

- [ ] **Step 4: Commit**

  ```
  git add backend.Tests/backend.Tests.csproj
  git commit -m "test(ai-provider-abstraction): add EF InMemory to existing test project"
  ```

---

## Task 2 — ExtractedBlocker model + AppDbContext + appsettings Ai section

**Test-first:** no — pure data model; behaviour is verified by the service tests in Task 7.

**Files:**
- Create: `backend/Models/ExtractedBlocker.cs`
- Modify: `backend/Data/AppDbContext.cs`
- Modify: `backend/appsettings.json`

- [ ] **Step 1: Create ExtractedBlocker model**

  Create `backend/Models/ExtractedBlocker.cs`:

  ```csharp
  namespace backend.Models;

  public class ExtractedBlocker
  {
      public int    Id             { get; set; }
      public int    JournalEntryId { get; set; }
      public string Description    { get; set; } = string.Empty;
      public DateTime CreatedAt    { get; set; }

      public JournalEntry JournalEntry { get; set; } = null!;
  }
  ```

- [ ] **Step 2: Add DbSet and navigation to AppDbContext**

  Open `backend/Data/AppDbContext.cs`. Add the `DbSet` property and the relationship config:

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using backend.Models;

  namespace backend.Data;

  public class AppDbContext : DbContext
  {
      public AppDbContext(DbContextOptions<AppDbContext> options)
          : base(options)
      {
      }

      public DbSet<JournalEntry>     JournalEntries    => Set<JournalEntry>();
      public DbSet<TaskItem>         TaskItems         => Set<TaskItem>();
      public DbSet<AutomationLog>    AutomationLogs    => Set<AutomationLog>();
      public DbSet<ExtractedBlocker> ExtractedBlockers => Set<ExtractedBlocker>();

      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
          modelBuilder.Entity<JournalEntry>()
              .HasMany(j => j.TaskItems)
              .WithOne(t => t.JournalEntry)
              .HasForeignKey(t => t.JournalEntryId);

          modelBuilder.Entity<JournalEntry>()
              .HasMany(j => j.ExtractedBlockers)
              .WithOne(b => b.JournalEntry)
              .HasForeignKey(b => b.JournalEntryId);
      }
  }
  ```

- [ ] **Step 3: Add the navigation collection to JournalEntry**

  Open `backend/Models/JournalEntry.cs`. Add the `ExtractedBlockers` collection:

  ```csharp
  namespace backend.Models;

  public class JournalEntry
  {
      public int    Id        { get; set; }
      public string Content   { get; set; } = string.Empty;
      public DateTime CreatedAt { get; set; }
      public string Summary   { get; set; } = string.Empty;

      public ICollection<TaskItem>         TaskItems         { get; set; } = new List<TaskItem>();
      public ICollection<ExtractedBlocker> ExtractedBlockers { get; set; } = new List<ExtractedBlocker>();
  }
  ```

- [ ] **Step 4: Add the Ai section to appsettings.json**

  Open `backend/appsettings.json`. The full file should now read:

  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Data Source=ai-productivity-assistant.db"
    },
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "AllowedHosts": "*",
    "Ai": {
      "Provider": "mock",
      "Dial": {
        "Endpoint": "https://your-dial-endpoint/openai/deployments/gpt-4o/chat/completions",
        "Model": "gpt-4o"
      },
      "Ollama": {
        "Endpoint": "http://localhost:11434",
        "Model": "llama3"
      }
    }
  }
  ```

  > **Secret note:** `Ai:Dial:ApiKey` is intentionally absent from this file. Set it with
  > `dotnet user-secrets set "Ai:Dial:ApiKey" "sk-..."` or the env var `Ai__Dial__ApiKey`.

- [ ] **Step 5: Verify the backend still builds**

  ```
  dotnet build backend/backend.csproj
  ```

  Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

  ```
  git add backend/Models/ExtractedBlocker.cs backend/Models/JournalEntry.cs \
          backend/Data/AppDbContext.cs backend/appsettings.json
  git commit -m "feat(ai-provider-abstraction): add ExtractedBlocker model and Ai config section"
  ```

---

## Task 3 — AiOptions configuration class

**Test-first:** no — config binding is validated at startup; exercised implicitly by all later tasks.

**Files:**
- Create: `backend/Services/Ai/AiOptions.cs`

- [ ] **Step 1: Create AiOptions**

  Create `backend/Services/Ai/AiOptions.cs`:

  ```csharp
  namespace backend.Services.Ai;

  public class AiOptions
  {
      public const string SectionName = "Ai";

      public string Provider { get; set; } = "mock";

      public DialOptions   Dial   { get; set; } = new();
      public OllamaOptions Ollama { get; set; } = new();

      public class DialOptions
      {
          public string Endpoint { get; set; } = string.Empty;
          public string Model    { get; set; } = "gpt-4o";

          /// <summary>
          /// Populated from .NET user-secrets or the Ai__Dial__ApiKey environment variable.
          /// Never set in appsettings.json or appsettings.Development.json.
          /// </summary>
          public string ApiKey { get; set; } = string.Empty;  // populated from user-secrets or Ai__Dial__ApiKey env var; never from appsettings
      }

      public class OllamaOptions
      {
          public string Endpoint { get; set; } = "http://localhost:11434";
          public string Model    { get; set; } = "llama3";
      }
  }
  ```

- [ ] **Step 2: Build**

  ```
  dotnet build backend/backend.csproj
  ```

  Expected: 0 errors.

- [ ] **Step 3: Commit**

  ```
  git add backend/Services/Ai/AiOptions.cs
  git commit -m "feat(ai-provider-abstraction): add AiOptions strongly-typed config"
  ```

---

## Task 4 — IAiProvider interface + MockAiProvider

**Test-first:** yes — verify Mock returns parseable JSON with all five required fields.

**Files:**
- Create: `backend/Services/Ai/IAiProvider.cs`
- Create: `backend/Services/Ai/Providers/MockAiProvider.cs`
- Create: `backend.Tests/Services/Ai/MockAiProviderTests.cs`

- [ ] **Step 1: Write the failing test**

  Create `backend.Tests/Services/Ai/MockAiProviderTests.cs`:

  ```csharp
  using System.Text.Json;
  using backend.Services.Ai;
  using backend.Services.Ai.Providers;
  using Xunit;

  namespace backend.Tests.Services.Ai;

  public class MockAiProviderTests
  {
      [Fact]
      public async Task CompleteAsync_ReturnsValidJsonWithAllRequiredFields()
      {
          var provider = new MockAiProvider();

          var json = await provider.CompleteAsync("system", "user");

          using var doc = JsonDocument.Parse(json);  // throws if invalid JSON
          var root = doc.RootElement;

          Assert.Equal(JsonValueKind.Array,  root.GetProperty("completed_tasks").ValueKind);
          Assert.Equal(JsonValueKind.Array,  root.GetProperty("new_tasks").ValueKind);
          Assert.Equal(JsonValueKind.Array,  root.GetProperty("blockers").ValueKind);
          Assert.Equal(JsonValueKind.Array,  root.GetProperty("priorities").ValueKind);
          Assert.Equal(JsonValueKind.String, root.GetProperty("summary").ValueKind);
      }
  }
  ```

- [ ] **Step 2: Run test — expect compile failure (types don't exist yet)**

  ```
  dotnet test backend.Tests/backend.Tests.csproj --no-build 2>&1 | head -20
  ```

  Expected: build error — `IAiProvider`, `MockAiProvider` not found.

- [ ] **Step 3: Create IAiProvider**

  Create `backend/Services/Ai/IAiProvider.cs`:

  ```csharp
  namespace backend.Services.Ai;

  public interface IAiProvider
  {
      Task<string> CompleteAsync(
          string systemPrompt,
          string userMessage,
          CancellationToken ct = default);
  }
  ```

- [ ] **Step 4: Create MockAiProvider**

  Create `backend/Services/Ai/Providers/MockAiProvider.cs`:

  ```csharp
  namespace backend.Services.Ai.Providers;

  public class MockAiProvider : IAiProvider
  {
      private const string MockJson = """
          {
            "completed_tasks": [],
            "new_tasks": ["Fix frontend validation"],
            "blockers": [],
            "priorities": ["Prepare demo"],
            "summary": "User worked on project setup."
          }
          """;

      public Task<string> CompleteAsync(
          string systemPrompt,
          string userMessage,
          CancellationToken ct = default)
          => Task.FromResult(MockJson);
  }
  ```

- [ ] **Step 5: Run test — expect GREEN**

  ```
  dotnet test backend.Tests/backend.Tests.csproj
  ```

  Expected: `1 test passed`.

- [ ] **Step 6: Commit**

  ```
  git add backend/Services/Ai/IAiProvider.cs \
          backend/Services/Ai/Providers/MockAiProvider.cs \
          backend.Tests/Services/Ai/MockAiProviderTests.cs
  git commit -m "feat(ai-provider-abstraction): add IAiProvider interface and MockAiProvider"
  ```

---

## Task 5 — ExtractionResult + ExtractionValidator

**Test-first:** yes — cover all hard failure rules and one happy-path parse.

**Files:**
- Create: `backend/Services/Ai/ExtractionResult.cs`
- Create: `backend/Services/Ai/ExtractionValidator.cs`
- Create: `backend.Tests/Services/Ai/ExtractionValidatorTests.cs`

- [ ] **Step 1: Write the failing tests**

  Create `backend.Tests/Services/Ai/ExtractionValidatorTests.cs`:

  ```csharp
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
  }
  ```

- [ ] **Step 2: Run tests — expect compile failure**

  ```
  dotnet test backend.Tests/backend.Tests.csproj 2>&1 | head -20
  ```

  Expected: build error — `ExtractionValidator`, `ExtractionResult` not found.

- [ ] **Step 3: Create ExtractionResult**

  Create `backend/Services/Ai/ExtractionResult.cs`:

  ```csharp
  using System.Text.Json.Serialization;

  namespace backend.Services.Ai;

  public sealed record ExtractionResult(
      [property: JsonPropertyName("completed_tasks")] string[] CompletedTasks,
      [property: JsonPropertyName("new_tasks")]       string[] NewTasks,
      [property: JsonPropertyName("blockers")]        string[] Blockers,
      [property: JsonPropertyName("priorities")]      string[] Priorities,
      [property: JsonPropertyName("summary")]         string   Summary
  );
  ```

- [ ] **Step 4: Create ExtractionValidator**

  Create `backend/Services/Ai/ExtractionValidator.cs`:

  ```csharp
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
  ```

- [ ] **Step 5: Run tests — expect GREEN**

  ```
  dotnet test backend.Tests/backend.Tests.csproj
  ```

  Expected: `6 tests passed` (1 from Task 4 + 5 new).

- [ ] **Step 6: Commit**

  ```
  git add backend/Services/Ai/ExtractionResult.cs \
          backend/Services/Ai/ExtractionValidator.cs \
          backend.Tests/Services/Ai/ExtractionValidatorTests.cs
  git commit -m "feat(ai-provider-abstraction): add ExtractionResult and ExtractionValidator with TDD"
  ```

---

## Task 6 — JournalExtractionResponse model

**Test-first:** no — plain record; serialisation is verified through controller behaviour.

**Files:**
- Create: `backend/Models/JournalExtractionResponse.cs`

- [ ] **Step 1: Create the response record**

  Create `backend/Models/JournalExtractionResponse.cs`:

  ```csharp
  using System.Text.Json.Serialization;

  namespace backend.Models;

  public sealed record JournalExtractionResponse(
      [property: JsonPropertyName("extraction_status")] string   ExtractionStatus,
      [property: JsonPropertyName("extraction_error")]  string?  ExtractionError,
      [property: JsonPropertyName("provider")]          string   Provider,
      [property: JsonPropertyName("completed_tasks")]   string[] CompletedTasks,
      [property: JsonPropertyName("new_tasks")]         string[] NewTasks,
      [property: JsonPropertyName("blockers")]          string[] Blockers,
      [property: JsonPropertyName("priorities")]        string[] Priorities,
      [property: JsonPropertyName("summary")]           string   Summary
  );
  ```

- [ ] **Step 2: Build**

  ```
  dotnet build backend/backend.csproj
  ```

- [ ] **Step 3: Commit**

  ```
  git add backend/Models/JournalExtractionResponse.cs
  git commit -m "feat(ai-provider-abstraction): add JournalExtractionResponse model"
  ```

---

## Task 7 — JournalExtractionService

**Test-first:** yes — four key scenarios before implementing the service.

**Files:**
- Create: `backend/Services/Ai/JournalExtractionService.cs`
- Create: `backend.Tests/Services/Ai/JournalExtractionServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

  Create `backend.Tests/Services/Ai/JournalExtractionServiceTests.cs`:

  ```csharp
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
  }
  ```

- [ ] **Step 2: Run tests — expect compile failure**

  ```
  dotnet test backend.Tests/backend.Tests.csproj 2>&1 | head -20
  ```

  Expected: build error — `JournalExtractionService` not found.

- [ ] **Step 3: Create JournalExtractionService**

  Create `backend/Services/Ai/JournalExtractionService.cs`:

  ```csharp
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
  ```

- [ ] **Step 4: Run tests — expect GREEN**

  ```
  dotnet test backend.Tests/backend.Tests.csproj
  ```

  Expected: `10 tests passed`.

- [ ] **Step 5: Commit**

  ```
  git add backend/Services/Ai/JournalExtractionService.cs \
          backend.Tests/Services/Ai/JournalExtractionServiceTests.cs
  git commit -m "feat(ai-provider-abstraction): add JournalExtractionService with TDD"
  ```

---

## Task 8 — Update JournalController

**Test-first:** no — the controller is a thin delegation layer; service behaviour is covered by Task 7 tests. Manual verification in Stage 5.

**Files:**
- Modify: `backend/Controllers/JournalController.cs`

- [ ] **Step 1: Replace JournalController**

  Overwrite `backend/Controllers/JournalController.cs` with:

  ```csharp
  using backend.Services.Ai;
  using Microsoft.AspNetCore.Mvc;

  namespace backend.Controllers;

  [ApiController]
  [Route("api/journal")]
  public class JournalController : ControllerBase
  {
      private readonly JournalExtractionService _extraction;

      public JournalController(JournalExtractionService extraction)
      {
          _extraction = extraction;
      }

      [HttpPost]
      public async Task<IActionResult> Create(
          [FromBody] JournalCreateRequest request,
          CancellationToken ct = default)
      {
          var response = await _extraction.ExtractAsync(request.JournalText, ct);
          return Ok(response);
      }

      [HttpGet("has-entry-today")]
      public async Task<IActionResult> HasEntryToday(
          [FromServices] backend.Data.AppDbContext db)
      {
          var today = DateTime.UtcNow.Date;
          var has   = await db.JournalEntries
              .AnyAsync(e => e.CreatedAt >= today && e.CreatedAt < today.AddDays(1));
          return Ok(new { hasEntryToday = has });
      }

      public sealed class JournalCreateRequest
      {
          public string JournalText { get; set; } = string.Empty;
      }
  }
  ```

  > `AnyAsync` requires `using Microsoft.EntityFrameworkCore;` — add it at the top of the file.

  Full file with all usings:

  ```csharp
  using backend.Services.Ai;
  using Microsoft.AspNetCore.Mvc;
  using Microsoft.EntityFrameworkCore;

  namespace backend.Controllers;

  [ApiController]
  [Route("api/journal")]
  public class JournalController : ControllerBase
  {
      private readonly JournalExtractionService _extraction;

      public JournalController(JournalExtractionService extraction)
      {
          _extraction = extraction;
      }

      [HttpPost]
      public async Task<IActionResult> Create(
          [FromBody] JournalCreateRequest request,
          CancellationToken ct = default)
      {
          var response = await _extraction.ExtractAsync(request.JournalText, ct);
          return Ok(response);
      }

      [HttpGet("has-entry-today")]
      public async Task<IActionResult> HasEntryToday(
          [FromServices] backend.Data.AppDbContext db)
      {
          var today = DateTime.UtcNow.Date;
          var has   = await db.JournalEntries
              .AnyAsync(e => e.CreatedAt >= today && e.CreatedAt < today.AddDays(1));
          return Ok(new { hasEntryToday = has });
      }

      public sealed class JournalCreateRequest
      {
          public string JournalText { get; set; } = string.Empty;
      }
  }
  ```

- [ ] **Step 2: Build**

  ```
  dotnet build backend/backend.csproj
  ```

  Expected: 0 errors. (DI will fail at startup until Task 11 — that's fine, the build succeeds.)

- [ ] **Step 3: Commit**

  ```
  git add backend/Controllers/JournalController.cs
  git commit -m "feat(ai-provider-abstraction): update JournalController to use JournalExtractionService"
  ```

---

## Task 9 — DialAiProvider + DialAuthHandler

**Test-first:** yes — verify the correct request body is sent to the DIAL endpoint.

**Files:**
- Create: `backend/Services/Ai/Providers/DialAuthHandler.cs`
- Create: `backend/Services/Ai/Providers/DialAiProvider.cs`
- Create: `backend.Tests/Services/Ai/DialAiProviderTests.cs`

- [ ] **Step 1: Write the failing test**

  Create `backend.Tests/Services/Ai/DialAiProviderTests.cs`:

  ```csharp
  using System.Net;
  using System.Text.Json;
  using backend.Services.Ai;
  using backend.Services.Ai.Providers;
  using backend.Tests.Helpers;
  using Microsoft.Extensions.Options;

  namespace backend.Tests.Services.Ai;

  public class DialAiProviderTests
  {
      [Fact]
      public async Task CompleteAsync_SendsCorrectRequestBodyToEndpoint()
      {
          // Arrange — capture the outgoing request via TestHttpMessageHandler
          HttpRequestMessage? captured = null;

          var handler = new TestHttpMessageHandler(req =>
          {
              captured = req;
              return new HttpResponseMessage(HttpStatusCode.OK)
              {
                  Content = new StringContent(
                      """{"choices":[{"message":{"content":"hello"}}]}""")
              };
          });

          var http    = new HttpClient(handler);
          var options = Options.Create(new AiOptions
          {
              Provider = "dial",
              Dial     = new AiOptions.DialOptions
              {
                  Endpoint = "https://dial.example.com/chat/completions",
                  Model    = "gpt-4o",
              }
          });

          var provider = new DialAiProvider(http, options);

          // Act
          var result = await provider.CompleteAsync("sys", "usr");

          // Assert
          Assert.Equal("hello", result);
          Assert.NotNull(captured);

          var body = await captured!.Content!.ReadAsStringAsync();
          var doc  = JsonDocument.Parse(body);
          Assert.Equal("gpt-4o", doc.RootElement.GetProperty("model").GetString());

          var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
          Assert.Equal(2, messages.Count);
          Assert.Equal("system", messages[0].GetProperty("role").GetString());
          Assert.Equal("sys",    messages[0].GetProperty("content").GetString());
          Assert.Equal("user",   messages[1].GetProperty("role").GetString());
          Assert.Equal("usr",    messages[1].GetProperty("content").GetString());
      }
  }
  ```

- [ ] **Step 2: Run test — expect compile failure**

  ```
  dotnet test backend.Tests/backend.Tests.csproj 2>&1 | head -20
  ```

  Expected: build error — `DialAiProvider` not found.

- [ ] **Step 3: Create DialAuthHandler**

  Create `backend/Services/Ai/Providers/DialAuthHandler.cs`:

  ```csharp
  using System.Net.Http.Headers;
  using Microsoft.Extensions.Options;

  namespace backend.Services.Ai.Providers;

  public class DialAuthHandler : DelegatingHandler
  {
      private readonly string _apiKey;

      public DialAuthHandler(IOptions<AiOptions> options)
      {
          _apiKey = options.Value.Dial.ApiKey;
      }

      protected override Task<HttpResponseMessage> SendAsync(
          HttpRequestMessage request, CancellationToken ct)
      {
          if (!string.IsNullOrEmpty(_apiKey))
              request.Headers.Authorization =
                  new AuthenticationHeaderValue("Bearer", _apiKey);
          return base.SendAsync(request, ct);
      }
  }
  ```

- [ ] **Step 4: Create DialAiProvider**

  Create `backend/Services/Ai/Providers/DialAiProvider.cs`:

  ```csharp
  using System.Net.Http.Json;
  using System.Text.Json.Serialization;
  using Microsoft.Extensions.Options;

  namespace backend.Services.Ai.Providers;

  public class DialAiProvider : IAiProvider
  {
      private readonly HttpClient _http;
      private readonly string     _endpoint;
      private readonly string     _model;

      public DialAiProvider(HttpClient http, IOptions<AiOptions> options)
      {
          _http     = http;
          _endpoint = options.Value.Dial.Endpoint;
          _model    = options.Value.Dial.Model;
      }

      public async Task<string> CompleteAsync(
          string systemPrompt, string userMessage, CancellationToken ct = default)
      {
          var body = new
          {
              model    = _model,
              messages = new[]
              {
                  new { role = "system", content = systemPrompt },
                  new { role = "user",   content = userMessage  },
              }
          };

          var response = await _http.PostAsJsonAsync(_endpoint, body, ct);
          response.EnsureSuccessStatusCode();

          var parsed = await response.Content
              .ReadFromJsonAsync<DialCompletionResponse>(ct);

          return parsed?.Choices?[0]?.Message?.Content
              ?? throw new InvalidOperationException(
                  "DIAL response did not contain a message content.");
      }

      private sealed record DialCompletionResponse(
          [property: JsonPropertyName("choices")] DialChoice[]? Choices);

      private sealed record DialChoice(
          [property: JsonPropertyName("message")] DialMessage? Message);

      private sealed record DialMessage(
          [property: JsonPropertyName("content")] string? Content);
  }
  ```

- [ ] **Step 5: Run test — expect GREEN**

  ```
  dotnet test backend.Tests/backend.Tests.csproj
  ```

  Expected: `11 tests passed`.

- [ ] **Step 6: Commit**

  ```
  git add backend/Services/Ai/Providers/DialAuthHandler.cs \
          backend/Services/Ai/Providers/DialAiProvider.cs \
          backend.Tests/Services/Ai/DialAiProviderTests.cs
  git commit -m "feat(ai-provider-abstraction): add DialAiProvider and DialAuthHandler with TDD"
  ```

---

## Task 10 — OllamaProvider

**Test-first:** yes — verify correct request body sent to `/api/chat` with `stream: false`.

**Files:**
- Create: `backend/Services/Ai/Providers/OllamaProvider.cs`
- Create: `backend.Tests/Services/Ai/OllamaProviderTests.cs`

- [ ] **Step 1: Write the failing test**

  Create `backend.Tests/Services/Ai/OllamaProviderTests.cs`:

  ```csharp
  using System.Net;
  using System.Text.Json;
  using backend.Services.Ai;
  using backend.Services.Ai.Providers;
  using backend.Tests.Helpers;
  using Microsoft.Extensions.Options;

  namespace backend.Tests.Services.Ai;

  public class OllamaProviderTests
  {
      [Fact]
      public async Task CompleteAsync_SendsRequestToApiChatWithStreamFalse()
      {
          // Arrange — capture the outgoing request via TestHttpMessageHandler
          HttpRequestMessage? captured = null;

          var handler = new TestHttpMessageHandler(req =>
          {
              captured = req;
              return new HttpResponseMessage(HttpStatusCode.OK)
              {
                  Content = new StringContent(
                      """{"message":{"content":"ollama reply"}}""")
              };
          });

          var http = new HttpClient(handler)
          {
              BaseAddress = new Uri("http://localhost:11434/")
          };
          var options = Options.Create(new AiOptions
          {
              Ollama = new AiOptions.OllamaOptions { Model = "llama3" }
          });

          var provider = new OllamaProvider(http, options);
          var result   = await provider.CompleteAsync("sys", "usr");

          Assert.Equal("ollama reply", result);
          Assert.NotNull(captured);
          Assert.Equal("api/chat", captured!.RequestUri?.PathAndQuery.TrimStart('/'));

          var body = await captured.Content!.ReadAsStringAsync();
          var doc  = JsonDocument.Parse(body);
          Assert.Equal("llama3", doc.RootElement.GetProperty("model").GetString());
          Assert.False(doc.RootElement.GetProperty("stream").GetBoolean());

          var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
          Assert.Equal("system", messages[0].GetProperty("role").GetString());
          Assert.Equal("user",   messages[1].GetProperty("role").GetString());
      }
  }
  ```

- [ ] **Step 2: Run test — expect compile failure**

  ```
  dotnet test backend.Tests/backend.Tests.csproj 2>&1 | head -20
  ```

- [ ] **Step 3: Create OllamaProvider**

  Create `backend/Services/Ai/Providers/OllamaProvider.cs`:

  ```csharp
  using System.Net.Http.Json;
  using System.Text.Json.Serialization;
  using Microsoft.Extensions.Options;

  namespace backend.Services.Ai.Providers;

  public class OllamaProvider : IAiProvider
  {
      private readonly HttpClient _http;
      private readonly string     _model;

      public OllamaProvider(HttpClient http, IOptions<AiOptions> options)
      {
          _http  = http;
          _model = options.Value.Ollama.Model;
      }

      public async Task<string> CompleteAsync(
          string systemPrompt, string userMessage, CancellationToken ct = default)
      {
          var body = new
          {
              model    = _model,
              stream   = false,
              messages = new[]
              {
                  new { role = "system", content = systemPrompt },
                  new { role = "user",   content = userMessage  },
              }
          };

          var response = await _http.PostAsJsonAsync("api/chat", body, ct);
          response.EnsureSuccessStatusCode();

          var parsed = await response.Content
              .ReadFromJsonAsync<OllamaChatResponse>(ct);

          return parsed?.Message?.Content
              ?? throw new InvalidOperationException(
                  "Ollama response did not contain a message content.");
      }

      private sealed record OllamaChatResponse(
          [property: JsonPropertyName("message")] OllamaMessage? Message);

      private sealed record OllamaMessage(
          [property: JsonPropertyName("content")] string? Content);
  }
  ```

- [ ] **Step 4: Run test — expect GREEN**

  ```
  dotnet test backend.Tests/backend.Tests.csproj
  ```

  Expected: `12 tests passed`.

- [ ] **Step 5: Commit**

  ```
  git add backend/Services/Ai/Providers/OllamaProvider.cs \
          backend.Tests/Services/Ai/OllamaProviderTests.cs
  git commit -m "feat(ai-provider-abstraction): add OllamaProvider with TDD"
  ```

---

## Task 11 — DI wiring in Program.cs

**Test-first:** no — wiring is verified by the app starting correctly (manual smoke test).

**Files:**
- Modify: `backend/Program.cs`

- [ ] **Step 1: Replace Program.cs**

  Open `backend/Program.cs`. Replace the full file with:

  ```csharp
  using backend.Data;
  using backend.Services.Ai;
  using backend.Services.Ai.Providers;
  using backend.Services.Todoist;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Options;

  var builder = WebApplication.CreateBuilder(args);

  // ── Controllers + Swagger ─────────────────────────────────────────────────
  builder.Services.AddControllers();
  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen();

  // ── SQLite / EF Core ──────────────────────────────────────────────────────
  builder.Services.AddDbContext<AppDbContext>(opts =>
      opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

  // ── Todoist ───────────────────────────────────────────────────────────────
  builder.Services.Configure<TodoistOptions>(
      builder.Configuration.GetSection(TodoistOptions.SectionName));

  builder.Services
      .AddTransient<TodoistAuthHandler>()
      .AddHttpClient<ITodoistService, TodoistService>(client =>
      {
          var baseUrl = builder.Configuration["Todoist:BaseUrl"]
                        ?? "https://api.todoist.com/api/v1";
          client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
      })
      .AddHttpMessageHandler<TodoistAuthHandler>();

  // ── AI provider ───────────────────────────────────────────────────────────
  builder.Services.Configure<AiOptions>(
      builder.Configuration.GetSection(AiOptions.SectionName));

  var aiProvider = (builder.Configuration["Ai:Provider"] ?? "mock")
      .Trim().ToLowerInvariant();

  switch (aiProvider)
  {
      case "dial":
          builder.Services
              .AddTransient<DialAuthHandler>()
              .AddHttpClient<IAiProvider, DialAiProvider>()
              .AddHttpMessageHandler<DialAuthHandler>();
          break;

      case "ollama":
          builder.Services
              .AddHttpClient<IAiProvider, OllamaProvider>(client =>
              {
                  var endpoint = builder.Configuration["Ai:Ollama:Endpoint"]
                                 ?? "http://localhost:11434";
                  client.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
              });
          break;

      case "mock":
          builder.Services.AddSingleton<IAiProvider, MockAiProvider>();
          break;

      default:
          throw new InvalidOperationException(
              $"Unknown AI provider '{aiProvider}'. " +
              "Valid values: dial, ollama, mock. " +
              "Set 'Ai:Provider' in appsettings.json.");
  }

  builder.Services.AddScoped<JournalExtractionService>();

  // ── Build ─────────────────────────────────────────────────────────────────
  var app = builder.Build();

  using (var scope = app.Services.CreateScope())
  {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.Database.EnsureCreated();
  }

  // ── Startup warnings ──────────────────────────────────────────────────────
  var logFac         = app.Services.GetRequiredService<ILoggerFactory>();
  var startupLogger  = logFac.CreateLogger("Startup");

  var todoistOpts = app.Services.GetRequiredService<IOptions<TodoistOptions>>().Value;
  if (string.IsNullOrWhiteSpace(todoistOpts.ApiToken))
      startupLogger.LogWarning("Todoist ApiToken is not configured. Task endpoints will return 503.");

  var aiOpts = app.Services.GetRequiredService<IOptions<AiOptions>>().Value;
  if (aiOpts.Provider.Equals("dial", StringComparison.OrdinalIgnoreCase)
      && string.IsNullOrWhiteSpace(aiOpts.Dial.ApiKey))
      startupLogger.LogWarning(
          "Ai:Dial:ApiKey is not configured. " +
          "Set it via 'dotnet user-secrets set \"Ai:Dial:ApiKey\" \"sk-...\"' " +
          "or the Ai__Dial__ApiKey environment variable. " +
          "DIAL provider will fail at runtime.");

  // ── HTTP pipeline ─────────────────────────────────────────────────────────
  if (app.Environment.IsDevelopment())
  {
      app.UseSwagger();
      app.UseSwaggerUI();
  }

  app.UseHttpsRedirection();
  app.UseAuthorization();
  app.MapControllers();
  app.Run();
  ```

- [ ] **Step 2: Build**

  ```
  dotnet build backend/backend.csproj
  ```

  Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Smoke test — start the app and call the journal endpoint with mock provider**

  Ensure `appsettings.json` has `"Ai": { "Provider": "mock" }` (it does from Task 2).

  ```
  dotnet run --project backend/backend.csproj
  ```

  In a separate terminal:

  ```
  curl -s -X POST http://localhost:5162/api/journal \
    -H "Content-Type: application/json" \
    -d "{\"journalText\": \"Finished setup today.\"}" | python -m json.tool
  ```

  Expected response shape:
  ```json
  {
    "extraction_status": "ok",
    "extraction_error": null,
    "provider": "mock",
    "completed_tasks": [],
    "new_tasks": ["Fix frontend validation"],
    "blockers": [],
    "priorities": ["Prepare demo"],
    "summary": "User worked on project setup."
  }
  ```

  Stop the server with `Ctrl+C`.

- [ ] **Step 4: Run all tests to confirm nothing regressed**

  ```
  dotnet test backend.Tests/backend.Tests.csproj
  ```

  Expected: `12 tests passed`.

- [ ] **Step 5: Commit**

  ```
  git add backend/Program.cs
  git commit -m "feat(ai-provider-abstraction): wire DI provider selection and startup warnings in Program.cs"
  ```

---

## Task 12 — Frontend JournalCard.jsx updates

**Test-first:** no — frontend has no automated test runner; verified manually.

> This task covers **both** spec UI requirements:
> 1. **Degraded-state banner** — shown when `extraction_status !== "ok"`, displays
>    `extraction_error` from the server (or a default message). Replaces the results area.
> 2. **Summary rendering** — `JournalCard` passes `data.summary` to `onJournalProcessed()`
>    only when `extraction_status === "ok"`. `App.jsx` sets `summary` state which `SummaryCard`
>    renders. On failure, `onJournalProcessed('')` is called so `SummaryCard` shows its
>    placeholder. No changes needed to `App.jsx` or `SummaryCard.jsx` — the callback contract
>    already handles it.

**Files:**
- Modify: `frontend/src/components/JournalCard.jsx`
- Modify: `frontend/src/styles.css`

- [ ] **Step 1: Update JournalCard to handle extraction_status**

  Open `frontend/src/components/JournalCard.jsx`. Replace it with:

  ```jsx
  import { useState } from 'react';

  function JournalCard({ onJournalProcessed }) {
    const [journalText,       setJournalText]       = useState('');
    const [loading,           setLoading]           = useState(false);
    const [error,             setError]             = useState('');
    const [extractionWarning, setExtractionWarning] = useState('');

    async function handleSubmit(event) {
      event.preventDefault();
      setLoading(true);
      setError('');
      setExtractionWarning('');

      try {
        const response = await fetch('/api/journal', {
          method:  'POST',
          headers: { 'Content-Type': 'application/json' },
          body:    JSON.stringify({ journalText }),
        });

        if (!response.ok) throw new Error('Failed to submit journal entry.');

        const data = await response.json();
        setJournalText('');

        if (data.extraction_status !== 'ok') {
          // Journal was saved server-side but extraction failed — show warning
          setExtractionWarning(
            data.extraction_error ??
            'Journal saved, but AI extraction is temporarily unavailable.'
          );
          onJournalProcessed('');
        } else {
          onJournalProcessed(data.summary ?? '');
        }
      } catch {
        setError('Failed to submit journal entry.');
      } finally {
        setLoading(false);
      }
    }

    return (
      <div className="card">
        <div className="card-header">
          <h2 className="card-title">Journal Entry</h2>
        </div>
        <form className="journal-form" onSubmit={handleSubmit}>
          <label className="journal-label" htmlFor="journal">
            What did you work on today?
          </label>
          <textarea
            id="journal"
            className="journal-textarea"
            value={journalText}
            onChange={(e) => setJournalText(e.target.value)}
            placeholder="Write what you completed, what is blocked, and what needs to happen next."
            rows="6"
          />
          <button
            type="submit"
            className="btn-primary"
            disabled={loading || !journalText.trim()}
          >
            {loading ? 'Processing...' : 'Process with AI'}
          </button>
        </form>
        {error && (
          <p className="error-inline" style={{ marginTop: 8 }}>{error}</p>
        )}
        {extractionWarning && (
          <p className="extraction-warning" style={{ marginTop: 8 }}>
            ⚠ {extractionWarning}
          </p>
        )}
      </div>
    );
  }

  export default JournalCard;
  ```

- [ ] **Step 2: Add the warning style to styles.css**

  Open `frontend/src/styles.css`. Append at the end:

  ```css
  /* ── AI extraction warning ─────────────────────────────────────────────── */
  .extraction-warning {
    font-size: 13px;
    color: #b45309;
    background: #fffbeb;
    border: 1px solid #fcd34d;
    border-radius: 6px;
    padding: 6px 10px;
  }
  ```

- [ ] **Step 3: Verify the frontend starts and the warning renders**

  In one terminal: `dotnet run --project backend/backend.csproj`

  In another: `cd frontend && npm run dev`

  Open `http://localhost:5173`.

  Test the happy path — submit a journal entry with `provider=mock`, confirm `SummaryCard` updates with `"User worked on project setup."`.

  Test the degraded path — temporarily change `appsettings.json` `Provider` to `"dial"` with no ApiKey set, restart the backend, submit a journal entry. Confirm the yellow warning banner appears in `JournalCard` and the task list refreshes.

  Restore `Provider` to `"mock"` and restart.

- [ ] **Step 4: Commit**

  ```
  git add frontend/src/components/JournalCard.jsx frontend/src/styles.css
  git commit -m "feat(ai-provider-abstraction): add degraded-state banner to JournalCard"
  ```

---

## Summary

| Task | What it delivers |
|---|---|
| 1 | xUnit + Moq test project |
| 2 | `ExtractedBlocker` model, `AppDbContext` update, `appsettings.json` Ai section |
| 3 | `AiOptions` config class |
| 4 | `IAiProvider` interface, `MockAiProvider` (tested) |
| 5 | `ExtractionResult`, `ExtractionValidator` — all hard failure rules (tested) |
| 6 | `JournalExtractionResponse` API model |
| 7 | `JournalExtractionService` — full pipeline with 4 scenario tests |
| 8 | `JournalController` — inline mock replaced |
| 9 | `DialAiProvider` + `DialAuthHandler` (tested) |
| 10 | `OllamaProvider` (tested) |
| 11 | DI wiring in `Program.cs` + startup warnings |
| 12 | Frontend degraded-state banner + summary pass-through to `SummaryCard` |
