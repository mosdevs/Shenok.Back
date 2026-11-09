using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRegistered;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRevoked;
using Shenok.Back.RecognitionService.Application.Recognition.UseCases.DecodeWaveform;

namespace Shenok.Back.RecognitionService.Application.Tests;

public class RecognitionApplicationDependencyInjectionTests
{
    [Fact]
    public void AddRecognitionApplication_registers_each_use_case_once_and_leaves_validators_to_the_host()
    {
        var services = new ServiceCollection();

        services.AddRecognitionApplication();
        services.AddRecognitionApplication();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DecodeWaveformUseCase));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ApplyWaveformRegisteredUseCase));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ApplyWaveformRevokedUseCase));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IUseCase<DecodeWaveformRequest, DecodeWaveformResponse>));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IUseCase<ApplyWaveformRegisteredRequest>));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IUseCase<ApplyWaveformRevokedRequest>));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Name.Contains("Validator", StringComparison.Ordinal));
    }
}
