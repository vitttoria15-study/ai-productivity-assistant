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
builder.Services.AddScoped<AiDiagnosticsService>();   // ← health-check probe

// ── Build ─────────────────────────────────────────────────────────────────
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ── Startup warnings ──────────────────────────────────────────────────────
var logFac        = app.Services.GetRequiredService<ILoggerFactory>();
var startupLogger = logFac.CreateLogger("Startup");

var todoistOpts = app.Services.GetRequiredService<IOptions<TodoistOptions>>().Value;
if (string.IsNullOrWhiteSpace(todoistOpts.ApiToken))
    startupLogger.LogWarning("Todoist ApiToken is not configured. Task endpoints will return 503.");

var aiOpts = app.Services.GetRequiredService<IOptions<AiOptions>>().Value;
var (aiConfigured, aiMissingReason) = AiProviderConfigValidator.Check(aiOpts);
if (!aiConfigured)
    startupLogger.LogWarning(
        "AI provider misconfigured at startup: {Reason}", aiMissingReason);

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
