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
            Detach(added: true, modified: true);
            throw new IndexConcurrencyException(exception);
        }
        catch (DbUpdateException exception) when (IsInboxPrimaryKey(exception))
        {
            Detach(added: true, modified: false);
            throw new InboxAlreadyProcessedException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            Detach(added: true, modified: false);
            throw new ConflictException(DuplicateSentence);
        }
    }

    /// <summary>
    /// Drops the rows this attempt staged. The next load must see the committed row,
    /// not the instance this attempt already changed.
    /// </summary>
    private void Detach(bool added, bool modified)
    {
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (added && entry.State == EntityState.Added)
                entry.State = EntityState.Detached;
            else if (modified && entry.State == EntityState.Modified)
                entry.State = EntityState.Detached;
        }
    }

    private static bool IsInboxPrimaryKey(DbUpdateException exception)
    {
        var postgres = FindPostgres(exception);
        return postgres?.SqlState == PostgresErrorCodes.UniqueViolation
            && (string.Equals(postgres.ConstraintName, InboxMessageConfiguration.PrimaryKeyName, StringComparison.Ordinal)
                || string.Equals(postgres.TableName, InboxMessageConfiguration.TableName, StringComparison.Ordinal));
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
