using System.ComponentModel;
using System.Diagnostics;

namespace Shenok.Back.RecognitionService.Infrastructure.Tests;

internal static class DockerProbe
{
    private static readonly Lazy<bool> Running = new(Check);

    public static bool IsRunning => Running.Value;

    private static bool Check()
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
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            stdout.GetAwaiter().GetResult();
            stderr.GetAwaiter().GetResult();
            return process.ExitCode == 0;
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

internal static class PostgresGate
{
    public static bool IsAvailable =>
        DockerProbe.IsRunning
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RECOGNITION_TEST_POSTGRES"));
}

internal sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public RequiresPostgresFactAttribute()
    {
        if (!PostgresGate.IsAvailable)
            Skip = "PostgreSQL is unavailable. Start Docker, or set RECOGNITION_TEST_POSTGRES to an admin connection string.";
    }
}

internal sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (!DockerProbe.IsRunning)
            Skip = "Docker is not running, so the RabbitMQ test container is unavailable.";
    }
}
