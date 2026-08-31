namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Blob storage abstraction for user-uploaded files (SPEC-05). The only implementation is
/// <c>LocalDiskFileStorage</c> — on the VPS that disk is a persistent Docker volume. The
/// abstraction stays because files are always streamed through the API: storage paths are
/// internal and never exposed as public URLs, so swapping in object storage later is a
/// one-class change with no impact on callers.
/// </summary>
public interface IFileStorage
{
    /// <summary>Persists the stream and returns the storage path (opaque key) for later reads/deletes.</summary>
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Opens the stored blob for reading. Throws <see cref="FileNotFoundException"/> when missing.</summary>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes the blob. Missing blobs are ignored (idempotent).</summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
