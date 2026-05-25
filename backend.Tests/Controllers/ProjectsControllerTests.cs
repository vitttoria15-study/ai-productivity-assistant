using backend.Controllers;
using backend.Models;
using backend.Services.Todoist;
using backend.Services.Todoist.Dtos;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace backend.Tests.Controllers;

public class ProjectsControllerTests
{
    private static Mock<ITodoistService> MockService() => new Mock<ITodoistService>();

    // ── GET /api/projects ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsMappedProjectResponses()
    {
        var mock = MockService();
        mock.Setup(s => s.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TodoistProject>
            {
                new("p1", "Inbox", "grey", 1, true),
                new("p2", "Work",  "blue", 2, false)
            });

        var controller = new ProjectsController(mock.Object);
        var result     = await controller.GetAll(CancellationToken.None);

        var ok       = Assert.IsType<OkObjectResult>(result);
        var projects = Assert.IsAssignableFrom<IEnumerable<ProjectResponse>>(ok.Value).ToList();

        Assert.Equal(2,       projects.Count);
        Assert.Equal("p1",    projects[0].Id);
        Assert.Equal("Inbox", projects[0].Name);
        Assert.Equal("grey",  projects[0].Color);
        Assert.Equal(1,       projects[0].Order);
        Assert.True(projects[0].IsInboxProject);

        Assert.Equal("p2",   projects[1].Id);
        Assert.Equal("Work", projects[1].Name);
        Assert.False(projects[1].IsInboxProject);
    }

    [Fact]
    public async Task GetAll_WhenTokenMissing_Returns503()
    {
        var mock = MockService();
        mock.Setup(s => s.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Todoist ApiToken is not configured"));

        var controller = new ProjectsController(mock.Object);
        var result     = await controller.GetAll(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
    }
}
