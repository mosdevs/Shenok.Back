using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Domain.Services;
using Shenok.Back.ContentService.Infrastructure.Packaging;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Infrastructure.Persistence.DomainEvents;
using Shenok.Back.ContentService.Infrastructure.Persistence.Interceptors;
using Shenok.Back.ContentService.Infrastructure.Persistence.Repositories;
using Shenok.Back.ContentService.Infrastructure.Storage;
using Shenok.Back.SharedKernel.DomainEvents;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace Shenok.Back.ContentService.Infrastructure.Tests;

[CollectionDefinition(InfrastructureFixture.Name)]
public sealed class InfrastructureCollection : ICollectionFixture<InfrastructureFixture>;

/// <summary>
/// One PostgreSQL database and one private blob account for every infrastructure test in the collection.
/// Testcontainers starts both when Docker is running. Otherwise the fixture creates a throwaway database
/// on the local server (CONTENT_TEST_POSTGRES, or the design-time credentials on port 5432) and starts Azurite through npx.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    public const string Name = "Shenok.Content.Infrastructure";

    private const string LocalAdminConnectionString =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly List<string> _databases = [];
    private PostgreSqlContainer? _postgres;
    private AzuriteContainer? _azurite;
    private Process? _azuriteProcess;
    private int _azuriteBlobPort;
    private string? _azuriteDirectory;
    private ServiceProvider? _services;
    private string _adminConnectionString = "";

    public string PostgresConnectionString { get; private set; } = "";
    public string BlobConnectionString { get; private set; } = "";
    public RecordingHandler Events { get; } = new();

    public async Task InitializeAsync()
    {
        if (await DockerIsRunningAsync())
            await StartContainersAsync();
        else
            await StartLocalAsync();

        await MigrateAsync(PostgresConnectionString);
        _services = BuildServices(PostgresConnectionString);
        await using var probe = new MemoryStream([1]);
        using var smoke = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await CreateBlobStorage().PutAsync(
            new BlobUpload($"health/{Guid.NewGuid():N}", probe, "audio/wav", 1),
            smoke.Token);
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
        if (_azurite is not null)
            await _azurite.DisposeAsync();

        await StopAzuriteProcessAsync();
    }

    public AsyncServiceScope CreateScope() =>
        (_services ?? throw new InvalidOperationException("Infrastructure fixture is not started.")).CreateAsyncScope();

    public ContentDbContext CreateBareContext() => CreateBareContext(PostgresConnectionString);

    public ContentDbContext CreateBareContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .Options;
        return new ContentDbContext(options);
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

    public async Task MigrateAsync(string connectionString)
    {
        await using var db = CreateBareContext(connectionString);
        await db.Database.MigrateAsync();
    }

    public AzureBlobStorage CreateBlobStorage(string? containerName = null)
    {
        var options = new BlobStorageOptions();
        if (containerName is not null)
            options.ContainerName = containerName;
        return new AzureBlobStorage(new BlobServiceClient(BlobConnectionString), Options.Create(options));
    }

    public FfmpegAudioPackager CreatePackager(AzureBlobStorage? storage = null) =>
        new(
            storage ?? CreateBlobStorage(),
            Options.Create(new PackagingOptions { ProcessTimeoutSeconds = 30 }),
            NullLogger<FfmpegAudioPackager>.Instance);

    public BlobServiceClient CreateBlobClient() => new(BlobConnectionString);

    private async Task StartContainersAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("content")
            .WithUsername("content")
            .WithPassword("content")
            .Build();
        _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.33.0").Build();
        await Task.WhenAll(_postgres.StartAsync(), _azurite.StartAsync());
        PostgresConnectionString = _postgres.GetConnectionString();
        BlobConnectionString = _azurite.GetConnectionString();
        var admin = new NpgsqlConnectionStringBuilder(PostgresConnectionString) { Database = "postgres" };
        _adminConnectionString = admin.ConnectionString;
    }

    private async Task StartLocalAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("CONTENT_TEST_POSTGRES")
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
                "Infrastructure tests could not open PostgreSQL. Start Docker, or set CONTENT_TEST_POSTGRES to an admin connection string.",
                exception);
        }

        _databases.Add(name);
        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = name };
        PostgresConnectionString = builder.ConnectionString;
        BlobConnectionString = await StartAzuriteProcessAsync();
    }

    private ServiceProvider BuildServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Events);
        services.AddSingleton<IDomainEventHandler<WaveformRegisteredDomainEvent>>(provider => provider.GetRequiredService<RecordingHandler>());
        services.AddSingleton<IDomainEventHandler<WaveformRevokedDomainEvent>>(provider => provider.GetRequiredService<RecordingHandler>());
        services.AddSingleton<IDomainEventHandler<PageCreatedDomainEvent>>(provider => provider.GetRequiredService<RecordingHandler>());
        services.AddSingleton<IDomainEventHandler<PagePublishedDomainEvent>>(provider => provider.GetRequiredService<RecordingHandler>());
        services.AddSingleton<IDomainEventHandler<PageArchivedDomainEvent>>(provider => provider.GetRequiredService<RecordingHandler>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<DomainEventDispatcherInterceptor>();
        services.AddDbContext<ContentDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            options.AddInterceptors(provider.GetRequiredService<DomainEventDispatcherInterceptor>());
        });
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ContentDbContext>());
        services.AddScoped<IPageRepository, PageRepository>();
        services.AddScoped<IVoiceRepository, VoiceRepository>();
        services.AddScoped<IWaveformMappingRepository, WaveformMappingRepository>();
        services.AddScoped<IWaveformCodeAllocator, PostgresWaveformCodeAllocator>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private async Task<string> StartAzuriteProcessAsync()
    {
        var blobPort = FreePort();
        var queuePort = FreePort();
        var tablePort = FreePort();
        _azuriteBlobPort = blobPort;
        _azuriteDirectory = Path.Combine(Path.GetTempPath(), "shenok-azurite", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_azuriteDirectory);
        var error = new StringBuilder();
        _azuriteProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npx.cmd"),
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        _azuriteProcess.StartInfo.ArgumentList.Add("--yes");
        _azuriteProcess.StartInfo.ArgumentList.Add("azurite@3.33.0");
        _azuriteProcess.StartInfo.ArgumentList.Add("--silent");
        _azuriteProcess.StartInfo.ArgumentList.Add("--location");
        _azuriteProcess.StartInfo.ArgumentList.Add(_azuriteDirectory);
        _azuriteProcess.StartInfo.ArgumentList.Add("--blobHost");
        _azuriteProcess.StartInfo.ArgumentList.Add("127.0.0.1");
        _azuriteProcess.StartInfo.ArgumentList.Add("--blobPort");
        _azuriteProcess.StartInfo.ArgumentList.Add(blobPort.ToString(CultureInfo.InvariantCulture));
        _azuriteProcess.StartInfo.ArgumentList.Add("--queuePort");
        _azuriteProcess.StartInfo.ArgumentList.Add(queuePort.ToString(CultureInfo.InvariantCulture));
        _azuriteProcess.StartInfo.ArgumentList.Add("--tablePort");
        _azuriteProcess.StartInfo.ArgumentList.Add(tablePort.ToString(CultureInfo.InvariantCulture));
        _azuriteProcess.StartInfo.ArgumentList.Add("--skipApiVersionCheck");
        _azuriteProcess.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
                error.AppendLine(args.Data);
        };
        _azuriteProcess.OutputDataReceived += (_, _) => { };

        try
        {
            if (!_azuriteProcess.Start())
                throw new InvalidOperationException("Azurite did not start.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("npx is required to start Azurite when Docker is not running.", exception);
        }

        _azuriteProcess.BeginOutputReadLine();
        _azuriteProcess.BeginErrorReadLine();
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            if (_azuriteProcess.HasExited)
                throw new InvalidOperationException($"Azurite exited with code {_azuriteProcess.ExitCode}. {error}");

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, blobPort);
                return AzuriteConnectionString(blobPort);
            }
            catch (SocketException)
            {
                await Task.Delay(200);
            }
        }

        throw new TimeoutException($"Azurite did not listen on port {blobPort}. {error}");
    }

    private async Task StopAzuriteProcessAsync()
    {
        if (_azuriteBlobPort != 0)
            KillNodeListeningOn(_azuriteBlobPort);

        if (_azuriteProcess is not null)
        {
            try
            {
                _azuriteProcess.CancelOutputRead();
                _azuriteProcess.CancelErrorRead();
            }
            catch (InvalidOperationException exception)
            {
                Trace.TraceWarning("Azurite output was not being captured: {0}", exception.Message);
            }

            try
            {
                if (!_azuriteProcess.HasExited)
                    _azuriteProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException exception)
            {
                Trace.TraceWarning("Azurite launcher had already exited: {0}", exception.Message);
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await _azuriteProcess.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Trace.TraceWarning("Azurite launcher was still running after the fixture stopped waiting.");
            }

            _azuriteProcess.Dispose();
            _azuriteProcess = null;
        }

        if (_azuriteDirectory is not null && Directory.Exists(_azuriteDirectory))
        {
            try
            {
                Directory.Delete(_azuriteDirectory, recursive: true);
            }
            catch (IOException exception) when (exception.HResult is 32 or 33)
            {
                Trace.TraceWarning("Azurite directory {0} stayed locked after shutdown.", _azuriteDirectory);
            }
        }
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
        var name = "cit" + Guid.NewGuid().ToString("N");
        if (!IsGeneratedName(name))
            throw new InvalidOperationException("Generated database name is not safe.");

        return name;
    }

    private static bool IsGeneratedName(string name) =>
        name.Length == 35 && name.StartsWith("cit", StringComparison.Ordinal) && name.Skip(3).All(char.IsAsciiHexDigit);

    private static void KillNodeListeningOn(int port)
    {
        Process? netstat = null;
        try
        {
            netstat = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano -p tcp",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            if (!netstat.Start())
                return;

            var stderr = netstat.StandardError.ReadToEndAsync();
            var output = netstat.StandardOutput.ReadToEnd();
            if (!netstat.WaitForExit(5000))
            {
                netstat.Kill(entireProcessTree: true);
                return;
            }

            stderr.GetAwaiter().GetResult();
            var marker = ":" + port.ToString(CultureInfo.InvariantCulture);
            foreach (var line in output.Split('\n'))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5 || !parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
                    continue;

                var local = parts[1];
                var colon = local.LastIndexOf(':');
                if (colon < 0 || !local[colon..].Equals(marker, StringComparison.Ordinal))
                    continue;

                if (!int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) || pid <= 0)
                    continue;

                try
                {
                    using var owner = Process.GetProcessById(pid);
                    if (!owner.ProcessName.Equals("node", StringComparison.OrdinalIgnoreCase))
                        continue;

                    owner.Kill(entireProcessTree: true);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
                {
                    Trace.TraceWarning("Azurite node {0} was not stopped: {1}", pid, exception.Message);
                }
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            Trace.TraceWarning("Azurite listener on port {0} could not be queried: {1}", port, exception.Message);
        }
        finally
        {
            netstat?.Dispose();
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string AzuriteConnectionString(int port) =>
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        $"BlobEndpoint=http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/devstoreaccount1;";

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
