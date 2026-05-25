using Microsoft.Extensions.Configuration;

namespace backend.Tests.Services;

public class TodoistOptionsTests
{
    [Fact]
    public void TodoistOptions_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Todoist:BaseUrl"]  = "https://api.todoist.com/api/v1",
                ["Todoist:ApiToken"] = "test-token-123"
            })
            .Build();

        var options = new backend.Services.Todoist.TodoistOptions();
        config.GetSection("Todoist").Bind(options);

        Assert.Equal("https://api.todoist.com/api/v1", options.BaseUrl);
        Assert.Equal("test-token-123", options.ApiToken);
    }

    [Fact]
    public void TodoistOptions_DefaultBaseUrl_IsSet()
    {
        var options = new backend.Services.Todoist.TodoistOptions();
        Assert.Equal("https://api.todoist.com/api/v1", options.BaseUrl);
    }

    [Fact]
    public void TodoistOptions_DefaultApiToken_IsEmpty()
    {
        var options = new backend.Services.Todoist.TodoistOptions();
        Assert.Equal(string.Empty, options.ApiToken);
    }
}
