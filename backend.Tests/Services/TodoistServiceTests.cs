using System.Net;
using System.Text;
using System.Text.Json;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using backend.Tests.Helpers;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services;

public class TodoistServiceTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a TodoistService backed by a fake HttpMessageHandler.
    /// No auth handler — use for happy-path tests where token injection is irrelevant.
    /// Accepts both sync and async handler delegates.
    /// </summary>
    private static TodoistService BuildService(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> httpHandler)
    {
        var handler = new TestHttpMessageHandler(httpHandler);
        var client  = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.todoist.com/api/v1/")
        };
        return new TodoistService(client);
    }

    private static TodoistService BuildService(
        Func<HttpRequestMessage, HttpResponseMessage> httpHandler)
        => BuildService(req => Task.FromResult(httpHandler(req)));

    /// <summary>
    /// Builds a TodoistService with the real TodoistAuthHandler wired in.
    /// Use for missing-token tests — the handler is the single validation point.
    /// </summary>
    private static TodoistService BuildServiceWithAuth(
        Func<HttpRequestMessage, HttpResponseMessage> httpHandler,
        string apiToken)
    {
        var options     = Options.Create(new TodoistOptions { ApiToken = apiToken });
        var testHandler = new TestHttpMessageHandler(httpHandler);
        var authHandler = new TodoistAuthHandler(options) { InnerHandler = testHandler };
        var client      = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("https://api.todoist.com/api/v1/")
        };
        return new TodoistService(client);
    }

    private static HttpResponseMessage JsonResponse(object payload, HttpStatusCode status = HttpStatusCode.OK)
    {
        var json    = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return new HttpResponseMessage(status) { Content = content };
    }

    // ── missing-token guard (via TodoistAuthHandler — single source of truth) ──

    [Fact]
    public async Task GetActiveTasksAsync_WhenTokenEmpty_ThrowsInvalidOperationException()
    {
        // Auth handler is the single validation point — service itself has no token check.
        var service = BuildServiceWithAuth(
            _ => new HttpResponseMessage(HttpStatusCode.OK), apiToken: "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetActiveTasksAsync());

        Assert.Equal("Todoist ApiToken is not configured", ex.Message);
    }

    [Fact]
    public async Task CreateTaskAsync_WhenTokenEmpty_ThrowsInvalidOperationException()
    {
        var service = BuildServiceWithAuth(
            _ => new HttpResponseMessage(HttpStatusCode.OK), apiToken: "");
        var req = new CreateTaskRequest("Do something", null, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateTaskAsync(req));
    }

    // ── GetProjectsAsync happy path ───────────────────────────────────────────

    [Fact]
    public async Task GetProjectsAsync_ReturnsMappedProjects()
    {
        // API v1 wraps results in { "results": [...], "next_cursor": null }
        var payload = new
        {
            results    = new[] { new { id = "p1", name = "Inbox", color = "grey", order = 1, is_inbox_project = true } },
            next_cursor = (string?)null
        };

        var service = BuildService(_ => JsonResponse(payload));

        var projects = await service.GetProjectsAsync();

        Assert.Single(projects);
        Assert.Equal("p1",    projects[0].Id);
        Assert.Equal("Inbox", projects[0].Name);
    }

    // ── GetActiveTasksAsync happy path ────────────────────────────────────────

    [Fact]
    public async Task GetActiveTasksAsync_ReturnsMappedTasks()
    {
        // API v1 wraps results in { "results": [...], "next_cursor": null }
        var payload = new
        {
            results = new[]
            {
                new
                {
                    id = "1", content = "Buy milk", description = (string?)null,
                    project_id = "p1", priority = 1, completed_at = (string?)null, due = (object?)null
                }
            },
            next_cursor = (string?)null
        };

        var service = BuildService(_ => JsonResponse(payload));

        var tasks = await service.GetActiveTasksAsync();

        Assert.Single(tasks);
        Assert.Equal("1",        tasks[0].Id);
        Assert.Equal("Buy milk", tasks[0].Content);
        Assert.Null(tasks[0].CompletedAt); // active task → null
    }

    [Fact]
    public async Task GetActiveTasksAsync_PassesProjectIdAsQueryParam()
    {
        HttpRequestMessage? captured = null;
        var emptyPage = new { results = Array.Empty<object>(), next_cursor = (string?)null };
        var service = BuildService(req =>
        {
            captured = req;
            return JsonResponse(emptyPage);
        });

        await service.GetActiveTasksAsync(projectId: "proj123");

        Assert.NotNull(captured);
        Assert.Contains("project_id=proj123", captured!.RequestUri?.Query);
    }

    // ── CreateTaskAsync happy path ────────────────────────────────────────────

    [Fact]
    public async Task CreateTaskAsync_SendsRequestAndReturnsTask()
    {
        string? requestBody = null;
        var response = new
        {
            id = "99", content = "New task", description = (string?)null,
            project_id = "p1", priority = 2, completed_at = (string?)null, due = (object?)null
        };

        var service = BuildService(async req =>
        {
            requestBody = await req.Content!.ReadAsStringAsync();
            return JsonResponse(response);
        });

        var result = await service.CreateTaskAsync(new CreateTaskRequest("New task", null, 2));

        Assert.NotNull(requestBody);
        Assert.Contains("New task", requestBody);
        Assert.Equal("99",       result.Id);
        Assert.Equal("New task", result.Content);
    }

    // ── UpdateTaskAsync happy path ────────────────────────────────────────────

    [Fact]
    public async Task UpdateTaskAsync_PatchesToCorrectUrlAndReturnsTask()
    {
        string? capturedUrl    = null;
        HttpMethod? capturedMethod = null;
        var response = new
        {
            id = "42", content = "Updated content", description = (string?)null,
            project_id = "p1", priority = 3, completed_at = (string?)null, due = (object?)null
        };

        var service = BuildService(req =>
        {
            capturedUrl    = req.RequestUri?.ToString();
            capturedMethod = req.Method;
            return JsonResponse(response);
        });

        var result = await service.UpdateTaskAsync("42", new UpdateTaskRequest("Updated content", 3));

        Assert.NotNull(capturedUrl);
        Assert.EndsWith("tasks/42", capturedUrl);
        Assert.Equal(HttpMethod.Patch, capturedMethod);
        Assert.Equal("Updated content", result.Content);
    }

    // ── CloseTaskAsync happy path ─────────────────────────────────────────────

    [Fact]
    public async Task CloseTaskAsync_PostsToCloseEndpoint()
    {
        string? capturedUrl = null;

        var service = BuildService(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await service.CloseTaskAsync("77");

        Assert.NotNull(capturedUrl);
        Assert.EndsWith("tasks/77/close", capturedUrl);
    }

    // ── DeleteTaskAsync happy path ────────────────────────────────────────────

    [Fact]
    public async Task DeleteTaskAsync_SendsDeleteToCorrectUrl()
    {
        string? capturedUrl    = null;
        HttpMethod? capturedMethod = null;

        var service = BuildService(req =>
        {
            capturedUrl    = req.RequestUri?.ToString();
            capturedMethod = req.Method;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await service.DeleteTaskAsync("55");

        Assert.NotNull(capturedUrl);
        Assert.EndsWith("tasks/55", capturedUrl);
        Assert.Equal(HttpMethod.Delete, capturedMethod);
    }
}
