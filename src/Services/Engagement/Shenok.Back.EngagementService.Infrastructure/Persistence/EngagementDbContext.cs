using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Infrastructure.Persistence.Configurations;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence;

public sealed class EngagementDbContext : DbContext, IUnitOfWork
{
    public DbSet<UserPageRelationship> UserPageRelationships => Set<UserPageRelationship>();
    public DbSet<ListeningRecord> ListeningRecords => Set<ListeningRecord>();
    public DbSet<ScanReceipt> ScanReceipts => Set<ScanReceipt>();
    public DbSet<PlaySession> PlaySessions => Set<PlaySession>();

    public EngagementDbContext(DbContextOptions<EngagementDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EngagementDbContext).Assembly);

    public override int SaveChanges() =>
        throw new InvalidOperationException("Engagement changes must be saved asynchronously.");

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("Engagement changes must be saved asynchronously.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            DetachFailedAttempt();
            throw new EngagementConcurrencyException();
        }
        catch (DbUpdateException exception) when (TryMapInsertConflict(exception, out var target))
        {
            DetachFailedAttempt();
            throw new EngagementInsertConflictException(target);
        }
    }

    /// <summary>
    /// Drops every row this attempt staged. The transaction is already aborted.
    /// The next get must materialize the committed row, because the use case mutates the instance it loaded.
    /// </summary>
    private void DetachFailedAttempt()
    {
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                entry.State = EntityState.Detached;
        }
    }

    private static bool TryMapInsertConflict(DbUpdateException exception, out EngagementInsertTarget target)
    {
        target = default;
        var postgres = FindPostgresException(exception);
        if (postgres is null || postgres.SqlState != PostgresErrorCodes.UniqueViolation)
            return false;

        return TryMapName(postgres.ConstraintName, out target)
            || TryMapName(postgres.TableName, out target);
    }

    private static bool TryMapName(string? name, out EngagementInsertTarget target)
    {
        switch (name)
        {
            case ScanReceiptConfiguration.PrimaryKeyName:
            case ScanReceiptConfiguration.TableName:
                target = EngagementInsertTarget.ScanReceipt;
                return true;
            case UserPageRelationshipConfiguration.PrimaryKeyName:
            case UserPageRelationshipConfiguration.TableName:
                target = EngagementInsertTarget.UserPageRelationship;
                return true;
            case ListeningRecordConfiguration.PrimaryKeyName:
            case ListeningRecordConfiguration.TableName:
                target = EngagementInsertTarget.ListeningRecord;
                return true;
            case PlaySessionConfiguration.PrimaryKeyName:
            case PlaySessionConfiguration.TableName:
                target = EngagementInsertTarget.PlaySession;
                return true;
            default:
                target = default;
                return false;
        }
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
                return postgres;
        }

        return null;
    }
}
