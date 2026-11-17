using System.Text.RegularExpressions;

namespace Notas.Infrastructure.Services;

public sealed partial class FolderService(IDbContextFactory<AppDbContext> factory, ICurrentUser currentUser) : IFolderService
{
    private const int MaxFolders = 100;
    private static readonly string[] Palette = ["#5646e0", "#d9534f", "#e08a1e", "#2f9e6b", "#2d8fd5", "#c2409b", "#7a8794"];

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    public async Task<IReadOnlyList<FolderDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Folders.AsNoTracking().Where(f => f.UserId == uid).OrderBy(f => f.Name)
            .Select(f => new FolderDto(f.Id, f.Name, f.Color, f.Notes.Count(n => n.DeletedAt == null && !n.IsArchived)))
            .ToListAsync(ct);
    }

    public async Task<Result<FolderDto>> CreateAsync(string name, string? color = null, CancellationToken ct = default)
    {
        name = (name ?? "").Trim();
        if (name.Length is 0 or > 60) return Result<FolderDto>.Fail(ErrorKind.Validation, "O nome da pasta deve ter entre 1 e 60 caracteres.");
        if (color is not null && !HexColor().IsMatch(color)) return Result<FolderDto>.Fail(ErrorKind.Validation, "Cor inválida.");

        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        var lower = name.ToLowerInvariant();
        var count = await db.Folders.CountAsync(f => f.UserId == uid, ct);
        if (count >= MaxFolders) return Result<FolderDto>.Fail(ErrorKind.Validation, $"Limite de {MaxFolders} pastas atingido.");
        if (await db.Folders.AnyAsync(f => f.UserId == uid && f.Name.ToLower() == lower, ct))
            return Result<FolderDto>.Fail(ErrorKind.Conflict, "Já existe uma pasta com esse nome.");

        var folder = new Folder { UserId = uid, Name = name, Color = color ?? Palette[count % Palette.Length] };
        db.Folders.Add(folder);
        await db.SaveChangesAsync(ct);
        return Result<FolderDto>.Ok(new FolderDto(folder.Id, folder.Name, folder.Color));
    }

    public async Task<Result> RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        name = (name ?? "").Trim();
        if (name.Length is 0 or > 60) return Result.Fail(ErrorKind.Validation, "O nome da pasta deve ter entre 1 e 60 caracteres.");

        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var lower = name.ToLowerInvariant();
        if (await db.Folders.AnyAsync(f => f.UserId == uid && f.Id != id && f.Name.ToLower() == lower, ct))
            return Result.Fail(ErrorKind.Conflict, "Já existe uma pasta com esse nome.");

        var n = await db.Folders.Where(f => f.Id == id && f.UserId == uid)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Name, name), ct);
        return n > 0 ? Result.Ok() : Result.NotFound("Pasta não encontrada.");
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        // FK SetNull: as notas da pasta continuam existindo, apenas sem pasta.
        var n = await db.Folders.Where(f => f.Id == id && f.UserId == uid).ExecuteDeleteAsync(ct);
        return n > 0 ? Result.Ok() : Result.NotFound("Pasta não encontrada.");
    }
}

public sealed class TagService(IDbContextFactory<AppDbContext> factory, ICurrentUser currentUser) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Tags.AsNoTracking()
            .Where(t => t.UserId == uid && t.Notes.Any(n => n.DeletedAt == null))
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.Notes.Count(n => n.DeletedAt == null)))
            .ToListAsync(ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var n = await db.Tags.Where(t => t.Id == id && t.UserId == uid).ExecuteDeleteAsync(ct);
        return n > 0 ? Result.Ok() : Result.NotFound("Tag não encontrada.");
    }
}
