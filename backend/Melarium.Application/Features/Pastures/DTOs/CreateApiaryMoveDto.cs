namespace Melarium.Application.Features.Pastures.DTOs;

public class CreateApiaryMoveDto
{
    public int ToPastureId { get; set; }
    public DateTime MovedAt { get; set; }
    public string? CertificateNumber { get; set; }
    public string? Notes { get; set; }
}
