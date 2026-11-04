namespace Shenok.Back.IdentityService.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken);
}
