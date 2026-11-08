using System.Reflection;

namespace Shenok.Back.EngagementService.Domain.Tests;

/// <summary>
/// Sets a private property the way EF Core will when it materializes a row.
/// Used to plant a sentinel timestamp so a second call cannot pass by writing <c>DateTime.UtcNow</c> in the same tick.
/// </summary>
internal static class AggregateAccess
{
    public static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Property {propertyName} was not found on {target.GetType().Name}.");

        property.SetValue(target, value);
    }
}
