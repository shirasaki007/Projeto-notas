using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Notas.Tests;

public sealed class FakeUser : ICurrentUser
{
    public string Id { get; set; } = "user-a";
    public ValueTask<string> GetUserIdAsync() => new(Id);
}

public sealed class NoopStorage : IFileStorage
{
    public Task SaveAsync(string path, Stream content, CancellationToken ct = default) => Task.CompletedTask;
    public Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
    public Task DeleteAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteUserDataAsync(string userId, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Banco SQLite em memória (com FKs ligadas) + serviços reais. Dois usuários: user-a e user-b.</summary>
public sealed class TestEnv : IDisposable
{
    private sealed class Factory(TestEnv env) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => env.CreateContext();
    }

    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    public FakeUser User { get; } = new();
    public INoteService Notes { get; }
    public IFolderService Folders { get; }
    public ITagService Tags { get; }

    public TestEnv()
    {
        _conn.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Users.AddRange(
            new AppUser { Id = "user-a", UserName = "a@test.com", Email = "a@test.com" },
            new AppUser { Id = "user-b", UserName = "b@test.com", Email = "b@test.com" });
        db.SaveChanges();

        var factory = new Factory(this);
        Notes = new NoteService(factory, User, new NoopStorage());
        Folders = new FolderService(factory, User);
        Tags = new TagService(factory, User);
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);

    public async Task<NoteDetailDto> NewNote(string title, string content = "", params string[] tags)
    {
        var r = await Notes.CreateAsync(new NoteSaveRequest { Title = title, Content = content, Tags = [.. tags] });
        Assert.True(r.Succeeded, r.Error?.Message);
        return r.Value!;
    }

    public void Dispose() => _conn.Dispose();
}
