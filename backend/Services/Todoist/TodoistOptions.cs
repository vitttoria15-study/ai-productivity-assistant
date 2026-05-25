namespace backend.Services.Todoist;

public class TodoistOptions
{
    public const string SectionName = "Todoist";

    public string BaseUrl { get; set; } = "https://api.todoist.com/api/v1";

    public string ApiToken { get; set; } = string.Empty;
}
