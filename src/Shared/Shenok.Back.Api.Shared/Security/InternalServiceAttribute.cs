namespace Shenok.Back.Api.Shared.Security;

/// <summary>
/// Marks an endpoint as an internal service call.
/// The request path cannot grant this. <see cref="InternalServiceScopeFilter"/> sets it after the API key is accepted.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class InternalServiceAttribute : Attribute;
