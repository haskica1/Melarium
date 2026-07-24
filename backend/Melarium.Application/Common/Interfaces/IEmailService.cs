namespace Melarium.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, bool suppressErrors);
}
