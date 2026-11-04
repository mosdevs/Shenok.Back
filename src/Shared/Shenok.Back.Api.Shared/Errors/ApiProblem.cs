namespace Shenok.Back.Api.Shared.Errors;

public sealed record ApiProblem(
    string Type,
    string Title,
    int Status,
    string Detail,
    string TraceId,
    IReadOnlyDictionary<string, string[]>? Errors);
