using Npgsql;
using NpgsqlTypes;

namespace Notas.Infrastructure.Services;

public sealed class NoteService(
    IDbContextFactory<AppDbContext> factory,
    ICurrentUser currentUser,
    IFileStorage storage) : INoteService
{
    private const int MaxVersions = 30;
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMinutes(10);

    // ---------- Consulta ----------

    public async Task<PagedResult<NoteListItemDto>> ListAsync(NoteQuery query, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<Note> q = db.Notes.AsNoTracking().Where(n => n.UserId == uid);
        q = query.View switch
        {
            NoteView.Trash => q.Where(n => n.DeletedAt != null),
            NoteView.Archived => q.Where(n => n.DeletedAt == null && n.IsArchived),
            NoteView.Pinned => q.Where(n => n.DeletedAt == null && !n.IsArchived && n.IsPinned),
            NoteView.Favorites => q.Where(n => n.DeletedAt == null && !n.IsArchived && n.IsFavorite),
            _ => q.Where(n => n.DeletedAt == null && !n.IsArchived)
        };

        if (query.FolderId is Guid folderId) q = q.Where(n => n.FolderId == folderId);
        if (query.TagId is Guid tagId) q = q.Where(n => n.Tags.Any(t => t.Id == tagId));
        if (query.From is DateOnly fromDate)
        {
            var d = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(n => n.UpdatedAt >= d);
        }
        if (query.To is DateOnly toDate)
        {
            var d = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(n => n.UpdatedAt < d);
        }
        if (!string.IsNullOrWhiteSpace(query.Search)) q = ApplySearch(db, q, query.Search);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(n => new
            {
                n.Id,
                n.Title,
                Preview = n.Content.Length > 400 ? n.Content.Substring(0, 400) : n.Content,
                n.FolderId,
                FolderName = n.Folder != null ? n.Folder.Name : null,
                n.IsPinned, n.IsFavorite, n.IsArchived,
                Trashed = n.DeletedAt != null,
                n.UpdatedAt,
                Tags = n.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new NoteListItemDto(
            r.Id, r.Title, Snippets.From(r.Preview), r.FolderId, r.FolderName,
            r.IsPinned, r.IsFavorite, r.IsArchived, r.Trashed, r.UpdatedAt, r.Tags)).ToList();

        return new PagedResult<NoteListItemDto>(items, total, page, size);
    }

    private static IQueryable<Note> ApplySearch(AppDbContext db, IQueryable<Note> q, string term)
    {
        term = term.Trim();
        var lower = term.ToLowerInvariant();

        if (db.Database.IsNpgsql())
        {
            // Título por prefixo/substring (digitação parcial) OU texto completo (stemming PT) OU tag.
            var pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            return q.Where(n =>
                EF.Functions.ILike(n.Title, pattern, "\\") ||
                EF.Property<NpgsqlTsVector>(n, "SearchVector").Matches(EF.Functions.WebSearchToTsQuery("portuguese", term)) ||
                n.Tags.Any(t => t.Name.Contains(lower)));
        }

        // Fallback portável (testes com SQLite).
        return q.Where(n => n.Title.ToLower().Contains(lower) || n.Content.ToLower().Contains(lower) || n.Tags.Any(t => t.Name.Contains(lower)));
    }

    public async Task<NoteCounts> GetCountsAsync(CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        var live = db.Notes.AsNoTracking().Where(n => n.UserId == uid && n.DeletedAt == null);
        var agg = await live.GroupBy(_ => 1).Select(g => new
        {
            All = g.Count(n => !n.IsArchived),
            Pinned = g.Count(n => n.IsPinned && !n.IsArchived),
            Fav = g.Count(n => n.IsFavorite && !n.IsArchived),
            Arch = g.Count(n => n.IsArchived)
        }).FirstOrDefaultAsync(ct);
        var trash = await db.Notes.AsNoTracking().CountAsync(n => n.UserId == uid && n.DeletedAt != null, ct);

        return new NoteCounts(agg?.All ?? 0, agg?.Pinned ?? 0, agg?.Fav ?? 0, agg?.Arch ?? 0, trash);
    }

    public async Task<Result<NoteDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await LoadDetailAsync(db, uid, id, ct);
    }

    private static async Task<Result<NoteDetailDto>> LoadDetailAsync(AppDbContext db, string uid, Guid id, CancellationToken ct)
    {
        var note = await db.Notes.AsNoTracking().AsSplitQuery()
            .Include(n => n.Tags).Include(n => n.Folder).Include(n => n.Attachments)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == uid, ct);
        return note is null ? Result<NoteDetailDto>.NotFound("Nota não encontrada.") : Result<NoteDetailDto>.Ok(ToDetail(note));
    }

    private static NoteDetailDto ToDetail(Note n) => new(
        n.Id, n.Title, n.Content, n.FolderId, n.Folder?.Name,
        n.IsPinned, n.IsFavorite, n.IsArchived, n.DeletedAt != null,
        n.CreatedAt, n.UpdatedAt, n.Revision,
        n.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList(),
        n.Attachments.OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.Size, a.CreatedAt)).ToList());

    // ---------- Escrita ----------

    public async Task<Result<NoteDetailDto>> CreateAsync(NoteSaveRequest request, CancellationToken ct = default)
    {
        if (Validation.FirstError(request) is { } err) return Result<NoteDetailDto>.Fail(ErrorKind.Validation, err);
        var tagNames = NormalizeTags(request.Tags);
        if (tagNames.Error is not null) return Result<NoteDetailDto>.Fail(tagNames.Error.Kind, tagNames.Error.Message);

        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        if (request.FolderId is Guid fid && !await db.Folders.AnyAsync(f => f.Id == fid && f.UserId == uid, ct))
            return Result<NoteDetailDto>.Fail(ErrorKind.Validation, "Pasta inválida.");

        var now = DateTime.UtcNow;
        var note = new Note
        {
            UserId = uid,
            Title = request.Title.Trim(),
            Content = request.Content,
            FolderId = request.FolderId,
            CreatedAt = now,
            UpdatedAt = now,
            Tags = await ResolveTagsAsync(db, uid, tagNames.Value!, ct)
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return await LoadDetailAsync(db, uid, note.Id, ct);
    }

    public async Task<Result<NoteDetailDto>> UpdateAsync(Guid id, NoteSaveRequest request, CancellationToken ct = default)
    {
        if (Validation.FirstError(request) is { } err) return Result<NoteDetailDto>.Fail(ErrorKind.Validation, err);
        var tagNames = NormalizeTags(request.Tags);
        if (tagNames.Error is not null) return Result<NoteDetailDto>.Fail(tagNames.Error.Kind, tagNames.Error.Message);

        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        var note = await db.Notes.Include(n => n.Tags).FirstOrDefaultAsync(n => n.Id == id && n.UserId == uid, ct);
        if (note is null) return Result<NoteDetailDto>.NotFound("Nota não encontrada.");
        if (note.DeletedAt != null)
            return Result<NoteDetailDto>.Fail(ErrorKind.Conflict, "Restaure a nota da lixeira antes de editá-la.");
        if (note.Revision != request.Revision)
            return Result<NoteDetailDto>.Fail(ErrorKind.Conflict, "Esta nota foi alterada em outro lugar.");

        if (request.FolderId is Guid fid && !await db.Folders.AnyAsync(f => f.Id == fid && f.UserId == uid, ct))
            return Result<NoteDetailDto>.Fail(ErrorKind.Validation, "Pasta inválida.");

        var title = request.Title.Trim();
        var newTags = tagNames.Value!;
        var textChanged = note.Title != title || note.Content != request.Content;
        var folderChanged = note.FolderId != request.FolderId;
        var tagsChanged = !note.Tags.Select(t => t.Name).OrderBy(x => x).SequenceEqual(newTags.OrderBy(x => x));

        if (!textChanged && !folderChanged && !tagsChanged) return await LoadDetailAsync(db, uid, id, ct);

        if (textChanged) await SnapshotAsync(db, note, force: false, ct);

        note.Title = title;
        note.Content = request.Content;
        note.FolderId = request.FolderId;
        if (tagsChanged)
        {
            note.Tags.Clear();
            foreach (var t in await ResolveTagsAsync(db, uid, newTags, ct)) note.Tags.Add(t);
        }
        note.UpdatedAt = DateTime.UtcNow;
        note.Revision++;

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Result<NoteDetailDto>.Fail(ErrorKind.Conflict, "Esta nota foi alterada em outro lugar.");
        }
        return await LoadDetailAsync(db, uid, id, ct);
    }

    public async Task<Result> ApplyAsync(Guid id, NoteAction action, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        var q = db.Notes.Where(n => n.Id == id && n.UserId == uid);
        var live = q.Where(n => n.DeletedAt == null);
        var now = (DateTime?)DateTime.UtcNow;

        // Flags não alteram UpdatedAt nem Revision: não conflitam com o autosave do editor.
        var affected = action switch
        {
            NoteAction.Pin => await live.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsPinned, true).SetProperty(n => n.IsArchived, false), ct),
            NoteAction.Unpin => await live.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsPinned, false), ct),
            NoteAction.Favorite => await live.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsFavorite, true), ct),
            NoteAction.Unfavorite => await live.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsFavorite, false), ct),
            NoteAction.Archive => await live.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsArchived, true).SetProperty(n => n.IsPinned, false), ct),
            NoteAction.Unarchive => await live.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsArchived, false), ct),
            NoteAction.Trash => await live.ExecuteUpdateAsync(s => s.SetProperty(n => n.DeletedAt, now).SetProperty(n => n.IsPinned, false), ct),
            NoteAction.Restore => await q.Where(n => n.DeletedAt != null).ExecuteUpdateAsync(s => s.SetProperty(n => n.DeletedAt, (DateTime?)null), ct),
            _ => -1
        };

        if (affected < 0) return Result.Fail(ErrorKind.Validation, "Ação inválida.");
        return affected > 0 ? Result.Ok() : Result.NotFound("Nota não encontrada.");
    }

    public async Task<Result> DeletePermanentlyAsync(Guid id, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var n = await NotePurger.PurgeAsync(db, storage,
            db.Notes.Where(x => x.Id == id && x.UserId == uid && x.DeletedAt != null), ct);
        return n > 0 ? Result.Ok() : Result.NotFound("Nota não encontrada na lixeira.");
    }

    public async Task<int> EmptyTrashAsync(CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await NotePurger.PurgeAsync(db, storage, db.Notes.Where(x => x.UserId == uid && x.DeletedAt != null), ct);
    }

    // ---------- Histórico ----------

    public async Task<IReadOnlyList<NoteVersionDto>> GetVersionsAsync(Guid noteId, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.NoteVersions.AsNoTracking()
            .Where(v => v.NoteId == noteId && v.Note.UserId == uid)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new NoteVersionDto(v.Id, v.Title, v.Content, v.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<Result<NoteDetailDto>> RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == uid && n.DeletedAt == null, ct);
        if (note is null) return Result<NoteDetailDto>.NotFound("Nota não encontrada.");
        var version = await db.NoteVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.NoteId == noteId, ct);
        if (version is null) return Result<NoteDetailDto>.NotFound("Versão não encontrada.");

        await SnapshotAsync(db, note, force: true, ct); // a restauração é reversível
        note.Title = version.Title;
        note.Content = version.Content;
        note.UpdatedAt = DateTime.UtcNow;
        note.Revision++;
        await db.SaveChangesAsync(ct);
        return await LoadDetailAsync(db, uid, noteId, ct);
    }

    public async Task<IReadOnlyList<NoteDetailDto>> ExportAllAsync(CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var notes = await db.Notes.AsNoTracking().AsSplitQuery()
            .Include(n => n.Tags).Include(n => n.Folder).Include(n => n.Attachments)
            .Where(n => n.UserId == uid && n.DeletedAt == null)
            .OrderBy(n => n.CreatedAt).ToListAsync(ct);
        return notes.Select(ToDetail).ToList();
    }

    // ---------- Helpers ----------

    private static async Task SnapshotAsync(AppDbContext db, Note note, bool force, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(note.Title) && string.IsNullOrWhiteSpace(note.Content)) return;

        var last = await db.NoteVersions.Where(v => v.NoteId == note.Id)
            .OrderByDescending(v => v.CreatedAt).FirstOrDefaultAsync(ct);
        if (!force && last is not null && DateTime.UtcNow - last.CreatedAt < SnapshotInterval) return;

        db.NoteVersions.Add(new NoteVersion { NoteId = note.Id, Title = note.Title, Content = note.Content });

        var stale = await db.NoteVersions.Where(v => v.NoteId == note.Id)
            .OrderByDescending(v => v.CreatedAt).Skip(MaxVersions - 1).Select(v => v.Id).ToListAsync(ct);
        if (stale.Count > 0) await db.NoteVersions.Where(v => stale.Contains(v.Id)).ExecuteDeleteAsync(ct);
    }

    private static Result<List<string>> NormalizeTags(IEnumerable<string>? raw)
    {
        var list = new List<string>();
        foreach (var t in raw ?? [])
        {
            if (!TagRules.TryNormalize(t, out var n)) continue;
            if (n.Length > TagRules.MaxLength)
                return Result<List<string>>.Fail(ErrorKind.Validation, $"Cada tag pode ter no máximo {TagRules.MaxLength} caracteres.");
            if (!list.Contains(n)) list.Add(n);
        }
        return list.Count > TagRules.MaxPerNote
            ? Result<List<string>>.Fail(ErrorKind.Validation, $"Use no máximo {TagRules.MaxPerNote} tags por nota.")
            : Result<List<string>>.Ok(list);
    }

    private static async Task<List<Tag>> ResolveTagsAsync(AppDbContext db, string uid, List<string> names, CancellationToken ct)
    {
        if (names.Count == 0) return [];
        var existing = await db.Tags.Where(t => t.UserId == uid && names.Contains(t.Name)).ToListAsync(ct);
        foreach (var name in names.Except(existing.Select(t => t.Name)))
        {
            var tag = new Tag { UserId = uid, Name = name };
            db.Tags.Add(tag);
            existing.Add(tag);
        }
        return existing;
    }
}

internal static class NotePurger
{
    /// <summary>Exclui definitivamente as notas (cascata no banco) e os arquivos anexados.</summary>
    public static async Task<int> PurgeAsync(AppDbContext db, IFileStorage storage, IQueryable<Note> notes, CancellationToken ct)
    {
        var paths = await db.Attachments.Where(a => notes.Any(n => n.Id == a.NoteId))
            .Select(a => a.StoragePath).ToListAsync(ct);
        var count = await notes.ExecuteDeleteAsync(ct);
        foreach (var p in paths) await storage.DeleteAsync(p, ct);
        return count;
    }
}
