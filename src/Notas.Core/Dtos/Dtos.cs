using System.ComponentModel.DataAnnotations;

namespace Notas.Core.Dtos;

public enum NoteView { All, Pinned, Favorites, Archived, Trash }

public enum NoteAction { Pin, Unpin, Favorite, Unfavorite, Archive, Unarchive, Trash, Restore, DeleteForever }

public record NoteQuery(
    NoteView View = NoteView.All,
    Guid? FolderId = null,
    Guid? TagId = null,
    string? Search = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 30);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public bool HasMore => Page * PageSize < Total;
}

public record TagDto(Guid Id, string Name, int NoteCount = 0);
public record FolderDto(Guid Id, string Name, string? Color, int NoteCount = 0);
public record NoteCounts(int All, int Pinned, int Favorites, int Archived, int Trash);

public record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, DateTime CreatedAt)
{
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
    public string Url => $"/api/v1/attachments/{Id}";
}

public sealed record AttachmentFile(Stream Content, string FileName, string ContentType)
{
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}

public static class AttachmentLimits
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int MaxPerNote = 20;
}

public record NoteListItemDto(
    Guid Id, string Title, string Snippet, Guid? FolderId, string? FolderName,
    bool IsPinned, bool IsFavorite, bool IsArchived, bool IsTrashed,
    DateTime UpdatedAt, IReadOnlyList<string> Tags);

public record NoteDetailDto(
    Guid Id, string Title, string Content, Guid? FolderId, string? FolderName,
    bool IsPinned, bool IsFavorite, bool IsArchived, bool IsTrashed,
    DateTime CreatedAt, DateTime UpdatedAt, int Revision,
    IReadOnlyList<string> Tags, IReadOnlyList<AttachmentDto> Attachments)
{
    public NoteListItemDto ToListItem() =>
        new(Id, Title, Common.Snippets.From(Content), FolderId, FolderName,
            IsPinned, IsFavorite, IsArchived, IsTrashed, UpdatedAt, Tags);
}

public record NoteVersionDto(Guid Id, string Title, string Content, DateTime CreatedAt);

public class NoteSaveRequest
{
    [StringLength(200, ErrorMessage = "O título pode ter no máximo 200 caracteres.")]
    public string Title { get; set; } = "";

    [MaxLength(200_000, ErrorMessage = "A nota excede o limite de 200.000 caracteres.")]
    public string Content { get; set; } = "";

    public Guid? FolderId { get; set; }
    public List<string> Tags { get; set; } = new();

    /// <summary>Revisão que o cliente viu por último (concorrência otimista).</summary>
    public int Revision { get; set; }
}
