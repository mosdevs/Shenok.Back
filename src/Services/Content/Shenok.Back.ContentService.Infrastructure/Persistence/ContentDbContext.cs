using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Counters;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;
using Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;

namespace Shenok.Back.ContentService.Infrastructure.Persistence;

public sealed class ContentDbContext : DbContext, IUnitOfWork
{
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<Voice> Voices => Set<Voice>();
    public DbSet<WaveformMapping> WaveformMappings => Set<WaveformMapping>();
    public DbSet<PageCounter> PageCounters => Set<PageCounter>();
    public DbSet<VoiceCounter> VoiceCounters => Set<VoiceCounter>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(PostgresWaveformCodeAllocator.SequenceName)
            .StartsAt(1)
            .IncrementsBy(1)
            .HasMin(1)
            .HasMax(WaveformCode.MaxValue)
            .IsCyclic(false);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
    }

    public override int SaveChanges() =>
        throw new InvalidOperationException("Content changes must be saved asynchronously.");

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsWaveformCodeViolation(exception))
        {
            DetachAddedInserts();
            throw new WaveformCodeAlreadyUsedException();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            DetachAddedInserts();
            throw new ConflictException("A unique value is already in use.");
        }
    }

    private void DetachAddedInserts()
    {
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Added)
                entry.State = EntityState.Detached;
        }
    }

    private static bool IsWaveformCodeViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres
            || postgres.SqlState != PostgresErrorCodes.UniqueViolation)
            return false;

        if (string.Equals(postgres.ConstraintName, WaveformMappingConfiguration.CodeIndexName, StringComparison.Ordinal))
            return true;

        return postgres.Message.Contains(WaveformMappingConfiguration.CodeIndexName, StringComparison.Ordinal);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
