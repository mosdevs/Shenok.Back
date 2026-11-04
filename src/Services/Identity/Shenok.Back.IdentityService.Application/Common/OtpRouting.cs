using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Application.Common;

public static class OtpRouting
{
    public static OtpChannel ResolveChannel(string? email, string? phoneNumber) =>
        string.IsNullOrWhiteSpace(phoneNumber) ? OtpChannel.Email : OtpChannel.Phone;

    public static string ResolveDestination(User user, OtpChannel channel) =>
        channel switch
        {
            OtpChannel.Phone when user.PhoneNumber is not null => user.PhoneNumber.Value,
            OtpChannel.Email when user.Email is not null => user.Email.Value,
            _ => throw new InvalidOperationException("The account does not support that verification channel.")
        };

    public static OtpChannel RegistrationChannel(Email? email, PhoneNumber? phone) =>
        phone is not null ? OtpChannel.Phone : OtpChannel.Email;
}
