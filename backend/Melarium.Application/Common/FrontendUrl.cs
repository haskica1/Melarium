using Microsoft.Extensions.Configuration;

namespace Melarium.Application.Common;

/// <summary>
/// Builds absolute links back into the SPA. Extracted so the fallback chain exists once: it is read
/// by e-mails (password reset, verification) and by the referral share link, and a second copy would
/// drift the day the deployment layout changes.
/// </summary>
public static class FrontendUrl
{
    public static string Build(IConfiguration config, string pathAndQuery)
    {
        var baseUrl = config["FrontendUrl"] ?? config["App:PublicBaseUrl"] ?? "http://localhost:5173";
        return $"{baseUrl.TrimEnd('/')}{pathAndQuery}";
    }
}
