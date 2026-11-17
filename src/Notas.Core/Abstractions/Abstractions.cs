using Notas.Core.Common;
using Notas.Core.Dtos;

namespace Notas.Core.Abstractions;

public interface ICurrentUser
{
    /// <summary>Id do usuário autenticado; lança UnauthorizedAccessException se não houver.</summary>
    ValueTask<string> GetUserIdAsync();
}

public interface IFileStorage
{
    Task SaveAsync(string path, Stream content, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task DeleteUserDataAsync(string userId, CancellationToken ct = default);
}

public interface INoteService
{
    Task<PagedResult<NoteListItemDto>> ListAsync(NoteQuery query, CancellationToken ct = default);
    Task<NoteCounts> GetCountsAsync(CancellationToken ct = default);
    Task<Result<NoteDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<NoteDetailDto>> CreateAsync(NoteSaveRequest request, CancellationToken ct = default);
    Task<Result<NoteDetailDto>> UpdateAsync(Guid id, NoteSaveRequest request, CancellationToken ct = default);
    Task<Result> ApplyAsync(Guid id, NoteAction action, CancellationToken ct = default);
    Task<Result> DeletePermanentlyAsync(Guid id, CancellationToken ct = default);
    Task<int> EmptyTrashAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NoteVersionDto>> GetVersionsAsync(Guid noteId, CancellationToken ct = default);
    Task<Result<NoteDetailDto>> RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken ct = default);
    Task<IReadOnlyList<NoteDetailDto>> ExportAllAsync(CancellationToken ct = default);
}

public interface IFolderService
{
    Task<IReadOnlyList<FolderDto>> ListAsync(CancellationToken ct = default);
    Task<Result<FolderDto>> CreateAsync(string name, string? color = null, CancellationToken ct = default);
    Task<Result> RenameAsync(Guid id, string name, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> ListAsync(CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IAttachmentService
{
    Task<Result<AttachmentDto>> UploadAsync(Guid noteId, string fileName, Stream content, CancellationToken ct = default);
    Task<Result<AttachmentFile>> OpenAsync(Guid id, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
