using backend.Data;
using backend.Services.Todoist;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Todoist ───────────────────────────────────────────────────────────────────
builder.Services.Configure<TodoistOptions>(
    builder.Configuration.GetSection(TodoistOptions.SectionName));

builder.Services
    .AddTransient<TodoistAuthHandler>()
    .AddHttpClient<ITodoistService, TodoistService>(client =>
    {
        var baseUrl = builder.Configuration["Todoist:BaseUrl"]
                      ?? "https://api.todoist.com/api/v1";
        // Ensure trailing slash so relative paths resolve correctly
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    })
    .AddHttpMessageHandler<TodoistAuthHandler>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
db.Database.EnsureCreated();

// ── Startup warning for missing Todoist token ─────────────────────────────────
var todoistOpts = app.Services.GetRequiredService<IOptions<TodoistOptions>>().Value;
if (string.IsNullOrWhiteSpace(todoistOpts.ApiToken))
{
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var startupLogger = loggerFactory.CreateLogger("Startup");
    startupLogger.LogWarning(
        "Todoist ApiToken is not configured. Task endpoints will return 503.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
