using BaseLibrary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ServerLibrary.Data;
using ServerLibrary.Repositories.Implementations;

namespace ServerLibrary.UnitTests.Repositories;

public class EmployeeNoteRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AddAsync_PersistsNote()
    {
        await using var ctx  = CreateContext();
        var repo = new EmployeeNoteRepository(ctx);

        var note = new EmployeeNote
        {
            EmployeeId      = 1,
            NoteText        = "Great performance this quarter.",
            SentimentLabel  = "Positive",
            SentimentScore  = 0.85f,
            CreatedAt       = DateTime.UtcNow,
            CreatedByUserId = "test-user"
        };

        await repo.AddAsync(note);

        Assert.Equal(1, await ctx.EmployeeNotes.CountAsync());
        Assert.True(note.Id > 0);
    }

    [Fact]
    public async Task GetByEmployeeIdAsync_ReturnsOnlyMatchingEmployee()
    {
        await using var ctx  = CreateContext();
        var repo = new EmployeeNoteRepository(ctx);

        ctx.EmployeeNotes.AddRange(
            new EmployeeNote { EmployeeId = 1, NoteText = "Note A", SentimentLabel = "Positive", CreatedAt = DateTime.UtcNow, CreatedByUserId = "u" },
            new EmployeeNote { EmployeeId = 2, NoteText = "Note B", SentimentLabel = "Neutral",  CreatedAt = DateTime.UtcNow, CreatedByUserId = "u" },
            new EmployeeNote { EmployeeId = 1, NoteText = "Note C", SentimentLabel = "Negative", CreatedAt = DateTime.UtcNow, CreatedByUserId = "u" }
        );
        await ctx.SaveChangesAsync();

        var results = await repo.GetByEmployeeIdAsync(1);

        Assert.Equal(2, results.Count);
        Assert.All(results, n => Assert.Equal(1, n.EmployeeId));
    }

    [Fact]
    public async Task GetAllAsync_WithFromFilter_ReturnsOnlyRecentNotes()
    {
        await using var ctx  = CreateContext();
        var repo = new EmployeeNoteRepository(ctx);
        var cutoff = DateTime.UtcNow.AddDays(-7);

        ctx.EmployeeNotes.AddRange(
            new EmployeeNote { EmployeeId = 1, NoteText = "Old note",    SentimentLabel = "Neutral",  CreatedAt = DateTime.UtcNow.AddDays(-30), CreatedByUserId = "u" },
            new EmployeeNote { EmployeeId = 1, NoteText = "Recent note", SentimentLabel = "Positive", CreatedAt = DateTime.UtcNow.AddDays(-3),  CreatedByUserId = "u" }
        );
        await ctx.SaveChangesAsync();

        var results = await repo.GetAllAsync(from: cutoff);

        Assert.Single(results);
        Assert.Equal("Recent note", results[0].NoteText);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsCount()
    {
        await using var ctx  = CreateContext();
        var repo = new EmployeeNoteRepository(ctx);

        for (int i = 0; i < 10; i++)
        {
            ctx.EmployeeNotes.Add(new EmployeeNote
            {
                EmployeeId      = 1,
                NoteText        = $"Note {i}",
                SentimentLabel  = "Neutral",
                CreatedAt       = DateTime.UtcNow.AddMinutes(-i),
                CreatedByUserId = "u"
            });
        }
        await ctx.SaveChangesAsync();

        var results = await repo.GetRecentAsync(count: 5);

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task GetByEmployeeIdAsync_OrdersByCreatedAtDescending()
    {
        await using var ctx  = CreateContext();
        var repo = new EmployeeNoteRepository(ctx);

        ctx.EmployeeNotes.AddRange(
            new EmployeeNote { EmployeeId = 1, NoteText = "Older",  SentimentLabel = "Neutral", CreatedAt = DateTime.UtcNow.AddDays(-5), CreatedByUserId = "u" },
            new EmployeeNote { EmployeeId = 1, NoteText = "Newer",  SentimentLabel = "Neutral", CreatedAt = DateTime.UtcNow.AddDays(-1), CreatedByUserId = "u" }
        );
        await ctx.SaveChangesAsync();

        var results = await repo.GetByEmployeeIdAsync(1);

        Assert.Equal("Newer", results[0].NoteText);
        Assert.Equal("Older", results[1].NoteText);
    }
}
