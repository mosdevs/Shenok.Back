namespace Shenok.Back.IdentityService.Application.Common.Interfaces;

public interface ISmsSender
{
    Task SendVerificationCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken);
}
