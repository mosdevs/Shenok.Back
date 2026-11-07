using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Services;

namespace Shenok.Back.ContentService.Infrastructure.Persistence;

public sealed class PostgresWaveformCodeAllocator(ContentDbContext db) : IWaveformCodeAllocator
{
    public const string SequenceName = "waveform_code_seq";

    public async Task<WaveformCode> AllocateAsync(CancellationToken ct = default)
    {
        if (!IsSafeIdentifier(SequenceName))
            throw new InvalidOperationException("Waveform code sequence name is invalid.");

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT nextval('{SequenceName}')";
        if (db.Database.CurrentTransaction is not null)
            command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();

        var scalar = await command.ExecuteScalarAsync(ct);
        if (scalar is null or DBNull)
            throw new InvalidOperationException("Waveform code sequence did not return a value.");

        return WaveformCode.From(Convert.ToInt64(scalar, CultureInfo.InvariantCulture));
    }

    private static bool IsSafeIdentifier(string name)
    {
        if (name.Length is 0 or > 63)
            return false;

        foreach (var character in name)
        {
            if (character is not (>= 'a' and <= 'z' or '_'))
                return false;
        }

        return true;
    }
}
