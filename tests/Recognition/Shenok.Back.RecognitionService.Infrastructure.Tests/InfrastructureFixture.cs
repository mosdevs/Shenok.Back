using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shenok.Back.RecognitionService.Application;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Domain.Repositories;
using Shenok.Back.RecognitionService.Infrastructure.Persistence;
using Shenok.Back.RecognitionService.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Shenok.Back.RecognitionService.Infrastructure.Tests;

[CollectionDefinition(InfrastructureFixture.Name)]
public sealed class InfrastructureCollection : ICollectionFixture<InfrastructureFixture>;

/// <summary>
/// One recognition database for the persistence tests. RabbitMQ starts only when Docker is running.
/// Tests skip, instead of failing the collection, when neither Docker nor RECOGNITION_TEST_POSTGRES is available.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    public const string Name = "Shenok.Recognition.Infrastructure";

    private readonly List<string> _databases = [];
    private PostgreSqlContainer? _postgres;
    private RabbitMqContainer? _rabbit;
    private ServiceProvider? _services;
    private string _adminConnectionString = "";
    private string? _postgresConnectionString;

    public string PostgresConnectionString =>
        _postgresConnectionString ?? throw new InvalidOperationException("Recognition PostgreSQL was not started.");

    public string? RabbitConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        if (!PostgresGate.IsAvailable)
            return;

        if (DockerProbe.IsRunning)
            await StartContainersAsync();
        else
            await StartLocalAsync();

        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<RecognitionDbContext>(options => options.UseNpgsql(PostgresConnectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<RecognitionDbContext>());
        services.AddScoped<IWaveformIndexRepository, WaveformIndexRepository>();
        services.AddScoped<IInbox, InboxRepository>();
        services.AddRecognitionApplication();
        _services = services.BuildServiceProvider(validateScopes: true);
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
        if (_rabbit is not null)
            await _rabbit.DisposeAsync();
    }

    public AsyncServiceScope CreateScope() =>
        (_services ?? throw new InvalidOperationException("Recognition infrastructure fixture is not started.")).CreateAsyncScope();

    public RecognitionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RecognitionDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;
        return new RecognitionDbContext(options);
    }

    private async Task StartContainersAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("recognition")
            .WithUsername("recognition")
            .WithPassword("recognition")
            .Build();
        _rabbit = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());
        _postgresConnectionString = _postgres.GetConnectionString();
        RabbitConnectionString = _rabbit.GetConnectionString();
        var admin = new NpgsqlConnectionStringBuilder(PostgresConnectionString) { Database = "postgres" };
        _adminConnectionString = admin.ConnectionString;
    }

    private async Task StartLocalAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("RECOGNITION_TEST_POSTGRES")
            ?? throw new InvalidOperationException("RECOGNITION_TEST_POSTGRES is required when Docker is not running.");

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
                "Recognition tests could not open PostgreSQL. Start Docker, or set RECOGNITION_TEST_POSTGRES to an admin connection string.",
                exception);
        }

        _databases.Add(name);
        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = name };
        _postgresConnectionString = builder.ConnectionString;
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
        var name = "rit" + Guid.NewGuid().ToString("N");
        if (!IsGeneratedName(name))
            throw new InvalidOperationException("Generated database name is not safe.");

        return name;
    }

    private static bool IsGeneratedName(string name) =>
        name.Length == 35 && name.StartsWith("rit", StringComparison.Ordinal) && name.Skip(3).All(char.IsAsciiHexDigit);
}
