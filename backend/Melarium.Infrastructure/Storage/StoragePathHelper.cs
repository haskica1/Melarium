namespace Melarium.Infrastructure.Storage;

/// <summary>Shared key generation for blob storage: date-partitioned, extension from content type.</summary>
internal static class StoragePathHelper
{
    /// <summary>e.g. "2026/07/1f9c….jpg" — forward slashes work for both S3 keys and local subfolders.</summary>
    public static string NewKey(string contentType)
    {
        var ext = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png"  => ".png",
            "image/webp" => ".webp",
            _            => ".bin",
        };
        var now = DateTime.UtcNow;
        return $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{ext}";
    }
}
