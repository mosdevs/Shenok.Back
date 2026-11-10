using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.RecognitionService.Application.Common;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Infrastructure.Persistence.Configurations;

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence;

public sealed class RecognitionDbContext : DbContext, IUnitOfWork
{
    public const string DuplicateSentence = "A waveform index value is already stored.";

    public DbSet<WaveformIndexEntry> WaveformIndex => Set<WaveformIndexEntry>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public RecognitionDbContext(DbContextOptions<RecognitionDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecognitionDbContext).Assembly);

    public override int SaveChanges() =>
        throw new InvalidOperationException("Recognition changes must be saved asynchronously.");

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("Recognition changes must be saved asynchronously.");

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
        catch (DbUpdateConcurrencyException exception)
        {
            DetachStaged();
            throw new IndexConcurrencyException(exception);
        }
        catch (DbUpdateException exception) when (IsInboxPrimaryKey(exception))
        {
            DetachStaged();
            throw new InboxAlreadyProcessedException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            DetachStaged();
            throw new ConflictException(DuplicateSentence);
        }
    }

    /// <summary>
    /// Drops every row this attempt staged. The transaction rolled those changes back,
    /// so a later save on this context must not write them. The next load must see the committed row.
    /// </summary>
    private void DetachStaged()
    {
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// True only for <c>pk_inbox_messages</c>. Another unique index on the inbox table is a conflict,
    /// not an event that was already consumed. A driver that omits the constraint name may name the table instead.
    /// </summary>
    private static bool IsInboxPrimaryKey(DbUpdateException exception)
    {
        var postgres = FindPostgres(exception);
        if (postgres?.SqlState != PostgresErrorCodes.UniqueViolation)
            return false;

        if (string.Equals(postgres.ConstraintName, InboxMessageConfiguration.PrimaryKeyName, StringComparison.Ordinal))
            return true;

        return string.IsNullOrEmpty(postgres.ConstraintName)
            && string.Equals(postgres.TableName, InboxMessageConfiguration.TableName, StringComparison.Ordinal);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        var postgres = FindPostgres(exception);
        return postgres?.SqlState == PostgresErrorCodes.UniqueViolation;
    }

    private static PostgresException? FindPostgres(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
                return postgres;
        }

        return null;
    }
}
