using Melarium.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Melarium.Infrastructure.Storage;

/// <summary>
/// Development file storage on the local disk. Root comes from <c>Storage:LocalPath</c>
/// (default <c>./uploads</c>, git-ignored). Not for production — Render's disk is ephemeral.
/// </summary>
public class LocalDiskFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalDiskFileStorage(IConfiguration config)
    {
        _root = Path.GetFullPath(config["Storage:LocalPath"] ?? "./uploads");
    }

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var key = StoragePathHelper.NewKey(contentType);
        var fullPath = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(storagePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Stored file not found: {storagePath}");

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(storagePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    /// <summary>Maps a storage key to an absolute path, refusing traversal outside the root.</summary>
    private string Resolve(string key)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, key));
        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Storage key escapes the storage root: {key}");
        return fullPath;
    }
}
