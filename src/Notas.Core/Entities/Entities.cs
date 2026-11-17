namespace Notas.Core.Entities;

public abstract class UserOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Dono do registro. TODA consulta de serviço filtra por este campo.</summary>
    public string UserId { get; set; } = "";
}

public class Folder : UserOwnedEntity
{
    public string Name { get; set; } = "";
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}

public class Tag : UserOwnedEntity
{
    public string Name { get; set; } = "";
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}

public class Note : UserOwnedEntity
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public Guid? FolderId { get; set; }
    public Folder? Folder { get; set; }
    public bool IsPinned { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    /// <summary>Soft delete: não nulo = na lixeira (purgado após 30 dias).</summary>
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Token de concorrência otimista (detecta edição simultânea em outra aba/dispositivo).</summary>
    public int Revision { get; set; } = 1;
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<NoteVersion> Versions { get; set; } = new List<NoteVersion>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

public class NoteVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public Note Note { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Attachment : UserOwnedEntity
{
    public Guid NoteId { get; set; }
    public Note Note { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    /// <summary>Caminho relativo no storage (gerado pelo servidor; nunca derivado do nome enviado).</summary>
    public string StoragePath { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
