using System.Collections.Concurrent;
using System.Net.Http.Json;
using Kavenegar;
using Kavenegar.Exceptions;
using Kavenegar.Models.Enums;
using Microsoft.Extensions.Options;

namespace Shenok.Back.IdentityService.Infrastructure.Messaging;

public sealed class InMemorySmsSender : Application.Common.Interfaces.ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _messages = new();

    public string? LatestMessage(string phoneNumber) =>
        _messages.TryGetValue(phoneNumber, out var message) ? message : null;

    public Task SendVerificationCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        _messages[phoneNumber] = code;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryEmailSender : Application.Common.Interfaces.IEmailSender
{
    private readonly ConcurrentDictionary<string, string> _messages = new();

    public string? LatestBody(string email) =>
        _messages.TryGetValue(email, out var message) ? message : null;

    public Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        _messages[email] = body;
        return Task.CompletedTask;
    }
}

public sealed class KavenegarOptions
{
    public const string SectionName = "Kavenegar";
    public string ApiKey { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
}

public sealed class KavenegarSmsSender(IKavenegarApi api, IOptions<KavenegarOptions> options) : Application.Common.Interfaces.ISmsSender
{
    public async Task SendVerificationCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        var template = options.Value.Template;
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new InvalidOperationException("Kavenegar:ApiKey is required.");
        if (string.IsNullOrWhiteSpace(template))
            throw new InvalidOperationException("Kavenegar:Template is required.");

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await api.VerifyLookupAsync(KavenegarReceptor.FromE164(phoneNumber), code, template, VerifyLookupType.Sms);
        }
        catch (ApiException exception)
        {
            throw new InvalidOperationException($"Kavenegar lookup failed with status {exception.Code}.");
        }
        catch (HttpException)
        {
            throw new InvalidOperationException("Kavenegar lookup could not be reached.");
        }
    }
}

internal static class KavenegarReceptor
{
    public static string FromE164(string phoneNumber)
    {
        if (phoneNumber.StartsWith("+98", StringComparison.Ordinal) && phoneNumber.Length == 13)
            return string.Concat("0", phoneNumber.AsSpan(3));

        if (phoneNumber.StartsWith('+'))
            return string.Concat("00", phoneNumber.AsSpan(1));

        return phoneNumber;
    }
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string WebhookUrl { get; set; } = string.Empty;
}

public sealed class WebhookEmailSender(HttpClient httpClient, IOptions<EmailOptions> options) : Application.Common.Interfaces.IEmailSender
{
    public async Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.WebhookUrl))
            throw new InvalidOperationException("Email:WebhookUrl is required.");

        using var response = await httpClient.PostAsJsonAsync(options.Value.WebhookUrl, new { email, subject, body }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
