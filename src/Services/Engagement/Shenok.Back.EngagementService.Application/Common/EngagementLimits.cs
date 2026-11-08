namespace Shenok.Back.EngagementService.Application.Common;

/// <summary>
/// Request bounds for Engagement. <see cref="MaxVoicesPerRequest"/> matches Content's
/// <c>AudioLimits.MaxVoicesPerPage</c>. This project does not reference Content.
/// </summary>
public static class EngagementLimits
{
    public const int MaxVoicesPerRequest = 500;

    public static string TooManyVoicesMessage =>
        $"A listening-state request cannot list more than {MaxVoicesPerRequest} voices.";
}
