using backend.Controllers;
using backend.Models;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public class TasksControllerTests
{
    private static Mock<ITodoistService> MockService() => new Mock<ITodoistService>();

    // ── GET /api/tasks ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsMappedTaskResponses()
    {
        var mock = MockService();
        mock.Setup(s => s.GetActiveTasksAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TodoistTask>
            {
                new("1", "Buy milk",    null, "proj1", 1, null,                   null),
                new("2", "Send report", null, "proj1", 2, "2026-05-20T09:00:00Z", null)
            });

        var controller = new TasksController(mock.Object);
        var result     = await controller.GetAll(CancellationToken.None);

        var ok    = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsAssignableFrom<IEnumerable<TaskResponse>>(ok.Value).ToList();

        Assert.Equal(2, tasks.Count);

        Assert.Equal("1",        tasks[0].Id);
        Assert.Equal("Buy milk", tasks[0].Title);
        Assert.Equal("todo",     tasks[0].Status);   // CompletedAt=null → "todo"
        Assert.Equal(1,          tasks[0].Priority);

        Assert.Equal("2",           tasks[1].Id);
        Assert.Equal("Send report", tasks[1].Title);
        Assert.Equal("done",        tasks[1].Status); // CompletedAt non-null → "done"
    }

    [Fact]
    public async Task GetAll_WhenTokenMissing_Returns503()
    {
        var mock = MockService();
        mock.Setup(s => s.GetActiveTasksAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Todoist ApiToken is not configured"));

        var controller = new TasksController(mock.Object);
        var result     = await controller.GetAll(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
    }

    // ── POST /api/tasks ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Returns201WithMappedResponse()
    {
        var mock = MockService();
        mock.Setup(s => s.CreateTaskAsync(It.IsAny<CreateTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TodoistTask("new1", "New task", null, "p1", 1, null, null));

        var controller = new TasksController(mock.Object);
        var result     = await controller.Create(
            new CreateTaskRequest("New task", null, 1), CancellationToken.None);

        var created  = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<TaskResponse>(created.Value);
        Assert.Equal("new1",     response.Id);
        Assert.Equal("New task", response.Title);
        Assert.Equal("todo",     response.Status);
    }

    // ── DELETE /api/tasks/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Returns204()
    {
        var mock = MockService();
        mock.Setup(s => s.DeleteTaskAsync("99", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new TasksController(mock.Object);
        var result     = await controller.Delete("99", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
