using FluentValidation;

namespace Shenok.Back.EngagementService.Application.Common;

internal static class ClientTokenRules
{
    public static IRuleBuilderOptions<T, string> ClientToken<T>(
        this IRuleBuilder<T, string> rule,
        int maxLength,
        string name) =>
        rule.Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength)
            .WithMessage($"{name} must be 1 to {maxLength} characters after trim.");
}
