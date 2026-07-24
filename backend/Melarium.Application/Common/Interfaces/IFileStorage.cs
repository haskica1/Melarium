namespace Melarium.Application.Common.Interfaces;

/// <summary>
/// Blob storage abstraction for user-uploaded files (SPEC-05). Implementations:
/// local disk for development, S3-compatible object storage for production
/// (Render's disk is ephemeral). Files are always streamed through the API —
/// storage paths are internal and never exposed as public URLs.
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
