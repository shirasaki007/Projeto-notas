namespace Notas.Infrastructure.Services;

public sealed class AttachmentService(
    IDbContextFactory<AppDbContext> factory,
    ICurrentUser currentUser,
    IFileStorage storage) : IAttachmentService
{
    public async Task<Result<AttachmentDto>> UploadAsync(Guid noteId, string fileName, Stream content, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        if (!await db.Notes.AnyAsync(n => n.Id == noteId && n.UserId == uid && n.DeletedAt == null, ct))
            return Result<AttachmentDto>.NotFound("Nota não encontrada.");
        if (await db.Attachments.CountAsync(a => a.NoteId == noteId, ct) >= AttachmentLimits.MaxPerNote)
            return Result<AttachmentDto>.Fail(ErrorKind.Validation, $"Limite de {AttachmentLimits.MaxPerNote} anexos por nota.");

        // Lê com teto rígido de tamanho (não confia no tamanho informado pelo cliente).
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > AttachmentLimits.MaxBytes)
                return Result<AttachmentDto>.Fail(ErrorKind.Validation, "O arquivo excede o limite de 5 MB.");
        }
        if (buffer.Length == 0) return Result<AttachmentDto>.Fail(ErrorKind.Validation, "Arquivo vazio.");

        // O tipo é decidido pelo CONTEÚDO (magic bytes), nunca pelo Content-Type enviado.
        var safeName = SafeFileName(fileName);
        var contentType = Sniff(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), Path.GetExtension(safeName).ToLowerInvariant());
        if (contentType is null)
            return Result<AttachmentDto>.Fail(ErrorKind.Validation, "Tipo de arquivo não permitido. Use imagens (PNG, JPG, GIF, WEBP), PDF ou texto.");

        var att = new Attachment
        {
            UserId = uid,
            NoteId = noteId,
            FileName = safeName,
            ContentType = contentType,
            Size = buffer.Length
        };
        att.StoragePath = $"{uid}/{att.Id:N}";

        buffer.Position = 0;
        await storage.SaveAsync(att.StoragePath, buffer, ct);
        try
        {
            db.Attachments.Add(att);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(att.StoragePath, CancellationToken.None);
            throw;
        }
        return Result<AttachmentDto>.Ok(new AttachmentDto(att.Id, att.FileName, att.ContentType, att.Size, att.CreatedAt));
    }

    public async Task<Result<AttachmentFile>> OpenAsync(Guid id, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var a = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct);
        if (a is null) return Result<AttachmentFile>.NotFound("Anexo não encontrado.");
        var stream = await storage.OpenReadAsync(a.StoragePath, ct);
        return stream is null
            ? Result<AttachmentFile>.NotFound("Arquivo indisponível.")
            : Result<AttachmentFile>.Ok(new AttachmentFile(stream, a.FileName, a.ContentType));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct);
        if (a is null) return Result.NotFound("Anexo não encontrado.");
        db.Attachments.Remove(a);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(a.StoragePath, ct);
        return Result.Ok();
    }

    private static string SafeFileName(string? name)
    {
        var n = Path.GetFileName((name ?? "arquivo").Replace('\\', '/'));
        n = new string(n.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (n.Length == 0) n = "arquivo";
        return n.Length > 120 ? n[^120..] : n;
    }

    private static string? Sniff(ReadOnlySpan<byte> h, string ext)
    {
        if (h.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })) return "image/png";
        if (h.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return "image/jpeg";
        if (h.StartsWith("GIF8"u8)) return "image/gif";
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        if (h.StartsWith("%PDF-"u8)) return "application/pdf";
        if (ext is ".txt" or ".md" && !h.Contains((byte)0)) return "text/plain";
        return null; // SVG, HTML, executáveis etc. são recusados de propósito
    }
}
