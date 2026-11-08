using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shenok.Back.EngagementService.Application;
using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.EngagementService.Infrastructure;
using Shenok.Back.EngagementService.Infrastructure.Persistence;
using Shenok.Back.SharedKernel.DomainEvents;
using Testcontainers.PostgreSql;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

[CollectionDefinition(InfrastructureFixture.Name)]
public sealed class InfrastructureCollection : ICollectionFixture<InfrastructureFixture>;

/// <summary>
/// One PostgreSQL database for the infrastructure collection.
/// Testcontainers starts it when Docker is running. Otherwise the fixture creates a throwaway database
/// on the local server (ENGAGEMENT_TEST_POSTGRES, CONTENT_TEST_POSTGRES, or the design-time credentials on port 5432).
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    public const string Name = "Shenok.Engagement.Infrastructure";

    private const string LocalAdminConnectionString =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly List<string> _databases = [];
    private PostgreSqlContainer? _postgres;
    private ServiceProvider? _services;
    private string _adminConnectionString = "";

    public string PostgresConnectionString { get; private set; } = "";
    public RecordingHandler Events { get; } = new();

    public async Task InitializeAsync()
    {
        if (await DockerIsRunningAsync())
            await StartContainerAsync();
        else
            await StartLocalAsync();

        _services = CreateServices(PostgresConnectionString, migrateOnStartup: true);
        await EngagementDatabase.MigrateAsync(_services);
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();

        NpgsqlConnection.ClearAllPools();
        foreach (var name in _databases)
            await DropDatabaseAsync(name);

        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    public AsyncServiceScope CreateScope() =>
        (_services ?? throw new InvalidOperationException("Infrastructure fixture is not started.")).CreateAsyncScope();

    public EngagementDbContext CreateBareContext()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;
        return new EngagementDbContext(options);
    }

    public ServiceProvider CreateServices(string connectionString, bool migrateOnStartup)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = connectionString,
            [EngagementDatabase.MigrateOnStartupKey] = migrateOnStartup ? "true" : "false"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Events);
        services.AddSingleton<IDomainEventHandler<PageUnlockedDomainEvent>>(
            provider => provider.GetRequiredService<RecordingHandler>());
        services.AddEngagementApplication(configuration);
        services.AddEngagementInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var name = NewDatabaseName();
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
        await command.ExecuteNonQueryAsync();
        _databases.Add(name);
        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = name };
        return builder.ConnectionString;
    }

    private async Task StartContainerAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("engagement")
            .WithUsername("engagement")
            .WithPassword("engagement")
            .Build();
        await _postgres.StartAsync();
        PostgresConnectionString = _postgres.GetConnectionString();
        var admin = new NpgsqlConnectionStringBuilder(PostgresConnectionString) { Database = "postgres" };
        _adminConnectionString = admin.ConnectionString;
    }

    private async Task StartLocalAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("ENGAGEMENT_TEST_POSTGRES")
            ?? Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES")
            ?? LocalAdminConnectionString;
        var name = NewDatabaseName();
        try
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception) when (exception is NpgsqlException or SocketException or TimeoutException)
        {
            throw new InvalidOperationException(
                "Infrastructure tests could not open PostgreSQL. Start Docker, or set ENGAGEMENT_TEST_POSTGRES to an admin connection string.",
                exception);
        }

        _databases.Add(name);
        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = name };
        PostgresConnectionString = builder.ConnectionString;
    }

    private async Task DropDatabaseAsync(string name)
    {
        if (!IsGeneratedName(name))
            throw new InvalidOperationException("Refusing to drop a database that this fixture did not create.");

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()",
            connection))
        {
            terminate.Parameters.AddWithValue("name", name);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name}", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private static string NewDatabaseName()
    {
        var name = "eit" + Guid.NewGuid().ToString("N");
        if (!IsGeneratedName(name))
            throw new InvalidOperationException("Generated database name is not safe.");

        return name;
    }

    private static bool IsGeneratedName(string name) =>
        name.Length == 35 && name.StartsWith("eit", StringComparison.Ordinal) && name.Skip(3).All(char.IsAsciiHexDigit);

    private static async Task<bool> DockerIsRunningAsync()
    {
        Process? process = null;
        try
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            if (!process.Start())
                return false;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token);
            await stdout;
            await stderr;
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            if (process is { HasExited: false })
                process.Kill(entireProcessTree: true);
            return false;
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            return false;
        }
        finally
        {
            process?.Dispose();
        }
    }
}
