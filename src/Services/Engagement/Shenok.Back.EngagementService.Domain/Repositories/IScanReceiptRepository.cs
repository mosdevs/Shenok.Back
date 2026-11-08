using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;

namespace Shenok.Back.EngagementService.Domain.Repositories;

public interface IScanReceiptRepository
{
    Task<ScanReceipt?> GetAsync(ScanId scanId, CancellationToken ct = default);
    Task AddAsync(ScanReceipt receipt, CancellationToken ct = default);
}
