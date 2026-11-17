using Microsoft.Extensions.Options;

namespace Notas.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string RootPath { get; set; } = "App_Data/files";
}

/// <summary>Storage em disco. Troque por S3/Azure Blob implementando IFileStorage.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    /// <summary>Resolve e garante que o caminho final permanece dentro da raiz (anti path-traversal).</summary>
    private string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Caminho de armazenamento inválido.");
        return full;
    }

    public async Task SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var full = Resolve(path);
        Stream? s = File.Exists(full)
            ? new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(s);
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var full = Resolve(path);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    public Task DeleteUserDataAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("Usuário inválido.", nameof(userId));
        var dir = Resolve(userId); // sempre uma subpasta da raiz, nunca a raiz
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }
}
