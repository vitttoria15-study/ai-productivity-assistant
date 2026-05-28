using backend.Data;
using backend.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests.Data;

public class DbSchemaTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"schema_test_{Guid.NewGuid()}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private AppDbContext BuildSqliteContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new AppDbContext(opts);
    }

    // Regression test: inserting a blocker must not throw "table does not exist".
    // Fails before InitialCreate migration exists; passes after Migrate() is applied.
    [Fact]
    public async Task Migrate_AllTablesCreated_BlockerCanBePersisted()
    {
        await using var db = BuildSqliteContext();
        await db.Database.MigrateAsync();

        var entry = new JournalEntry
        {
            Content   = "Today I worked on DIAL integration. Blocker: API URL is unclear.",
            CreatedAt = DateTime.UtcNow,
            Summary   = string.Empty,
        };
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();

        var blocker = new ExtractedBlocker
        {
            JournalEntryId = entry.Id,
            Description    = "Cannot finish DIAL integration because the correct API URL is unclear.",
            CreatedAt      = DateTime.UtcNow,
        };
        db.ExtractedBlockers.Add(blocker);
        await db.SaveChangesAsync();

        var saved = await db.ExtractedBlockers.SingleAsync();
        Assert.Equal(blocker.Description, saved.Description);
        Assert.Equal(entry.Id, saved.JournalEntryId);
    }

    [Fact]
    public async Task Migrate_AllTablesCreated_SchemaMigrationsTableExists()
    {
        await using var db = BuildSqliteContext();
        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
    }
}
