using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using NpgsqlTypes;

namespace Notas.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<NoteVersion> NoteVersions => Set<NoteVersion>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Folder>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.Property(x => x.Color).HasMaxLength(9);
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Tag>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(30).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Note>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasIndex(x => new { x.UserId, x.DeletedAt, x.UpdatedAt });
            e.HasIndex(x => new { x.UserId, x.FolderId });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Folder).WithMany(f => f.Notes).HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Tags).WithMany(t => t.Notes).UsingEntity(j => j.ToTable("NoteTags"));
        });

        b.Entity<NoteVersion>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.NoteId, x.CreatedAt });
            e.HasOne(x => x.Note).WithMany(n => n.Versions).HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Attachment>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(150).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.StoragePath).HasMaxLength(200).IsRequired();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Note).WithMany(n => n.Attachments).HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserPreferences>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.EditorFont).HasMaxLength(32).IsRequired();
            e.Property(x => x.Accent).HasMaxLength(7).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
            e.HasOne<AppUser>().WithOne().HasForeignKey<UserPreferences>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Busca de texto completo (PostgreSQL): coluna gerada + índice GIN.
        // Fica como propriedade "shadow" para manter o domínio livre de tipos do Npgsql.
        if (Database.IsNpgsql())
        {
            b.Entity<Note>()
                .Property<NpgsqlTsVector>("SearchVector")
                .HasComputedColumnSql("to_tsvector('portuguese', coalesce(\"Title\", '') || ' ' || coalesce(\"Content\", ''))", stored: true);
            b.Entity<Note>().HasIndex("SearchVector").HasMethod("GIN");
        }
    }
}
