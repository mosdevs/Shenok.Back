namespace Shenok.Back.ContentService.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChanges(CancellationToken ct = default);
}
