using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.Gateway.Api.Http;

/// <summary>
/// A downstream 400. The shared middleware already maps <see cref="DomainException"/> to HTTP 400.
/// </summary>
internal sealed class DownstreamRequestException(string message) : DomainException(message);
