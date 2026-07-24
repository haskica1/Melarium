namespace Melarium.Application.Features.Pastures.DTOs;

public class ApiaryMoveDto
{
    public int Id { get; set; }
    public int ApiaryId { get; set; }
    public int? FromPastureId { get; set; }

    /// <summary>Null = "matična lokacija" (the move started from the apiary's original location).</summary>
    public string? FromPastureName { get; set; }

    /// <summary>Null = "matična lokacija" (this move returned the apiary to its original location).</summary>
    public int? ToPastureId { get; set; }
    public string ToPastureName { get; set; } = string.Empty;
    public DateTime MovedAt { get; set; }
    public string? CertificateNumber { get; set; }
    public string? Notes { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
