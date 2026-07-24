using QRCoder;

namespace Melarium.Application.Common.Services;

public class QrCodeService : IQrCodeService
{
    public string GeneratePngBase64(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data      = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var code      = new PngByteQRCode(data);

        // 10 px per module → roughly 250×250 px for a UUID payload
        var png = code.GetGraphic(pixelsPerModule: 10);
        return Convert.ToBase64String(png);
    }
}
