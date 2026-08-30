namespace Melarium.Application.Common.Validation;

/// <summary>
/// Image type detection for uploads. The client-supplied content type and the file extension are
/// untrusted — the real type is read from the file's header bytes, so a renamed executable cannot
/// enter storage as an "image".
/// </summary>
public static class ImageRules
{
    /// <summary>Shown to the user when the header bytes match none of the accepted formats.</summary>
    public const string UnsupportedFormatMessage = "Dozvoljeni formati slike su JPEG, PNG i WebP.";

    /// <summary>
    /// Determines the image type from magic bytes (JPEG / PNG / WebP), returning a seekable stream
    /// positioned at 0 alongside it (a non-seekable input is buffered). ContentType is null when the
    /// bytes match no accepted format.
    /// </summary>
    public static async Task<(Stream Stream, string? ContentType)> SniffContentTypeAsync(Stream content)
    {
        var stream = content;
        if (!stream.CanSeek)
        {
            var buffered = new MemoryStream();
            await content.CopyToAsync(buffered);
            stream = buffered;
        }

        stream.Position = 0;
        var header = new byte[12];
        var read = 0;
        while (read < header.Length)
        {
            var n = await stream.ReadAsync(header.AsMemory(read, header.Length - read));
            if (n == 0) break;
            read += n;
        }
        stream.Position = 0;

        return (stream, DetectContentType(header, read));
    }

    public static string? DetectContentType(byte[] header, int length)
    {
        if (length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        if (length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return "image/png";

        // RIFF....WEBP
        if (length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return "image/webp";

        return null;
    }
}
