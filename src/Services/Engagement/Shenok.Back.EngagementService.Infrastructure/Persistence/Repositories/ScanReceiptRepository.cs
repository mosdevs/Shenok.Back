using Microsoft.EntityFrameworkCore;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Repositories;

public sealed class ScanReceiptRepository(EngagementDbContext db) : IScanReceiptRepository
{
    public Task<ScanReceipt?> GetAsync(ScanId scanId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scanId);
        return db.ScanReceipts.AsNoTracking()
            .FirstOrDefaultAsync(receipt => receipt.Id == scanId, ct);
    }

    public Task AddAsync(ScanReceipt receipt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return db.ScanReceipts.AddAsync(receipt, ct).AsTask();
    }
}
