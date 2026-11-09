namespace Shenok.Back.RecognitionService.Application.Common.Interfaces;

/// <summary>
/// Commits one index change and its inbox row together.
/// </summary>
/// <remarks>
/// The Recognition database context is the implementation. Its synchronous SaveChanges throws.
/// Callers use <see cref="SaveChangesAsync"/>. There is no execution-strategy retry around this call.
/// The queue is the retry.
/// </remarks>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
