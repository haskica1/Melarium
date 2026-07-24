namespace Melarium.Application.Common.Services;

public interface IQrCodeService
{
    /// <summary>
    /// Generates a QR code for <paramref name="content"/> and returns it as a Base64-encoded PNG string.
    /// </summary>
    string GeneratePngBase64(string content);
}
