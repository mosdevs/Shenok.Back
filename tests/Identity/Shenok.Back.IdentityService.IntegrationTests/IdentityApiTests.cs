using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shenok.Back.IdentityService.Application.Auth;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;
using Shenok.Back.IdentityService.Infrastructure.Messaging;

namespace Shenok.Back.IdentityService.IntegrationTests;

public class IdentityApiTests : IAsyncLifetime
{
    private const string Password = "Secret123";
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        var connectionString = await EnsureDatabaseAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:identity", connectionString);
            builder.UseSetting("InternalAuth:ApiKey", "test-key");
            builder.UseSetting("Jwt:SigningKey", "dev-only-signing-key-32-characters");
        });
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Internal-Key", "test-key");
    }

    [Fact]
    public async Task Duplicate_email_returns_409()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var first = await _client!.PostAsJsonAsync("/register", ValidEmailBody(email, "first_user"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/register", ValidEmailBody(email, "second_user"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Duplicate_username_returns_409()
    {
        var username = $"user_{Guid.NewGuid():N}"[..20];
        var first = await _client!.PostAsJsonAsync("/register", ValidEmailBody($"a-{Guid.NewGuid():N}@example.com", username));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/register", ValidEmailBody($"b-{Guid.NewGuid():N}@example.com", username));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Duplicate_phone_returns_409()
    {
        var phone = UniquePhone();
        var first = await _client!.PostAsJsonAsync("/register", ValidPhoneBody(phone, "phone_one"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/register", ValidPhoneBody(phone, "phone_two"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Unverified_password_login_returns_403()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        var register = await _client!.PostAsJsonAsync("/register", ValidEmailBody(email, $"user_{Guid.NewGuid():N}"[..20]));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await _client.PostAsJsonAsync("/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Refresh_for_unverified_user_returns_403()
    {
        var refreshToken = await SeedUnverifiedRefreshTokenAsync();
        var response = await _client!.PostAsJsonAsync("/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotation_revokes_the_previous_token()
    {
        var tokens = await RegisterPhoneAndConfirmOtpAsync();
        var firstRefresh = await _client!.PostAsJsonAsync("/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var rotated = await firstRefresh.Content.ReadFromJsonAsync<AuthTokens>();

        var reuse = await _client.PostAsJsonAsync("/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var secondRefresh = await _client.PostAsJsonAsync("/refresh", new { refreshToken = rotated!.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Pending_phone_rejects_a_second_register_code_until_the_first_expires()
    {
        var phone = UniquePhone();
        var username = $"ph_{Guid.NewGuid():N}"[..20];
        var register = await _client!.PostAsJsonAsync("/register", ValidPhoneBody(phone, username));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var resend = await _client.PostAsJsonAsync("/otp/request", new { phoneNumber = phone, purpose = "register" });
        Assert.Equal(HttpStatusCode.Conflict, resend.StatusCode);
        Assert.Equal("A verification code was already sent.", await ReadDetailAsync(resend));

        await ExpireOtpChallengesAsync();

        var afterExpiry = await _client.PostAsJsonAsync("/otp/request", new { phoneNumber = phone, purpose = "register" });
        Assert.Equal(HttpStatusCode.OK, afterExpiry.StatusCode);

        var sms = _factory!.Services.GetRequiredService<InMemorySmsSender>();
        var code = ExtractOtp(sms.LatestMessage(NormalizePhone(phone))!);
        var confirm = await _client.PostAsJsonAsync("/otp/confirm", new { phoneNumber = phone, code, purpose = "register" });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var tokens = await confirm.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.Equal("true", ReadJwtClaim(tokens!.AccessToken, "verified"));
    }

    [Fact]
    public async Task Verified_phone_register_otp_returns_already_registered()
    {
        var phone = UniquePhone();
        await RegisterPhoneAndConfirmOtpAsync(phone);

        var request = await _client!.PostAsJsonAsync("/otp/request", new { phoneNumber = phone, purpose = "register" });
        Assert.Equal(HttpStatusCode.Conflict, request.StatusCode);
        Assert.Equal("Phone number is already registered.", await ReadDetailAsync(request));

        var confirm = await _client.PostAsJsonAsync("/otp/confirm", new { phoneNumber = phone, code = "000000", purpose = "register" });
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("Phone number is already registered.", await ReadDetailAsync(confirm));

        var loginCode = await _client.PostAsJsonAsync("/otp/request", new { phoneNumber = phone, purpose = "login" });
        Assert.Equal(HttpStatusCode.OK, loginCode.StatusCode);
    }

    [Fact]
    public async Task Verified_email_register_otp_returns_already_registered()
    {
        var email = $"mail-{Guid.NewGuid():N}@example.com";
        var register = await _client!.PostAsJsonAsync("/register", ValidEmailBody(email, $"mail_{Guid.NewGuid():N}"[..20]));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var emailSender = _factory!.Services.GetRequiredService<InMemoryEmailSender>();
        var code = ExtractOtp(emailSender.LatestBody(email)!);
        var confirm = await _client.PostAsJsonAsync("/otp/confirm", new { email, code, purpose = "register" });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var request = await _client.PostAsJsonAsync("/otp/request", new { email, purpose = "register" });
        Assert.Equal(HttpStatusCode.Conflict, request.StatusCode);
        Assert.Equal("Email is already registered.", await ReadDetailAsync(request));
    }

    [Fact]
    public async Task Phone_register_confirm_otp_issues_verified_tokens()
    {
        var tokens = await RegisterPhoneAndConfirmOtpAsync();
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("true", ReadJwtClaim(tokens.AccessToken, "verified"));
    }

    [Fact]
    public async Task Email_register_confirm_otp_issues_verified_tokens()
    {
        var email = $"mail-{Guid.NewGuid():N}@example.com";
        var register = await _client!.PostAsJsonAsync("/register", ValidEmailBody(email, $"mail_{Guid.NewGuid():N}"[..20]));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var emailSender = _factory!.Services.GetRequiredService<InMemoryEmailSender>();
        var body = emailSender.LatestBody(email);
        Assert.NotNull(body);
        var code = ExtractOtp(body!);

        var confirm = await _client.PostAsJsonAsync("/otp/confirm", new { email, code, purpose = "register" });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var tokens = await confirm.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.Equal("true", ReadJwtClaim(tokens!.AccessToken, "verified"));
    }

    [Fact]
    public async Task Internal_grant_role_adds_content_creator()
    {
        var tokens = await RegisterPhoneAndConfirmOtpAsync();
        var userId = ReadJwtClaim(tokens.AccessToken, JwtRegisteredClaimNames.Sub);

        var grant = await _client!.PostAsJsonAsync(
            $"/internal/users/{userId}/roles",
            new { role = nameof(UserRole.ContentCreator) });
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        var body = await grant.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("ContentCreator", body.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("Listener", body.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Swagger_documents_the_caller_id_header()
    {
        var response = await _client!.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var schemes = document.GetProperty("components").GetProperty("securitySchemes");
        Assert.Equal("X-Internal-Key", schemes.GetProperty("InternalKey").GetProperty("name").GetString());
        Assert.Equal("X-User-Id", schemes.GetProperty("UserId").GetProperty("name").GetString());
        Assert.Equal("header", schemes.GetProperty("UserId").GetProperty("in").GetString());
    }

    [Fact]
    public async Task Invalid_body_uses_the_shared_problem_shape()
    {
        var response = await _client!.PostAsJsonAsync("/register", new { displayName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Validation failed", problem.GetProperty("title").GetString());
        Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
    }

    private async Task<AuthTokens> RegisterPhoneAndConfirmOtpAsync(string? phone = null)
    {
        phone ??= UniquePhone();
        var register = await _client!.PostAsJsonAsync("/register", ValidPhoneBody(phone, $"ph_{Guid.NewGuid():N}"[..20]));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var sms = _factory!.Services.GetRequiredService<InMemorySmsSender>();
        var message = sms.LatestMessage(NormalizePhone(phone));
        Assert.NotNull(message);
        var code = ExtractOtp(message!);

        var confirm = await _client.PostAsJsonAsync("/otp/confirm", new { phoneNumber = phone, code, purpose = "register" });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        return (await confirm.Content.ReadFromJsonAsync<AuthTokens>())!;
    }

    private async Task<string> SeedUnverifiedRefreshTokenAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<TokenIssuer>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<Shenok.Back.IdentityService.Application.Common.Interfaces.IUnitOfWork>();

        var user = User.Register(
            null,
            PhoneNumber.Create(UniquePhone()),
            null,
            UserProfile.Create($"seed_{Guid.NewGuid():N}"[..20], "Seed"));
        await users.AddAsync(user);
        var issued = await tokenIssuer.IssueAsync(user, CancellationToken.None);
        await unitOfWork.SaveChanges(CancellationToken.None);
        return issued.Tokens.RefreshToken;
    }

    private async Task ExpireOtpChallengesAsync()
    {
        var connectionString = _factory!.Services.GetRequiredService<IConfiguration>().GetConnectionString("identity");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""UPDATE otp_challenges SET "ExpiresAt" = @expires""", connection);
        command.Parameters.AddWithValue("expires", DateTime.UtcNow.AddMinutes(-1));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("detail").GetString();
    }

    private static string ExtractOtp(string message)
    {
        var match = Regex.Match(message, @"\b(\d{6})\b");
        Assert.True(match.Success, "Expected a six-digit OTP in the sender payload.");
        return match.Groups[1].Value;
    }

    private static string ReadJwtClaim(string accessToken, string claimType)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.Claims.Single(claim => claim.Type == claimType).Value;
    }

    private static string UniquePhone()
    {
        var suffix = Random.Shared.Next(0, 10_000_000).ToString("D7");
        return $"0912{suffix}";
    }

    private static string NormalizePhone(string phone) =>
        PhoneNumber.Create(phone).Value;

    private static object ValidEmailBody(string email, string username) => new
    {
        username,
        displayName = "Listener",
        email,
        password = Password
    };

    private static object ValidPhoneBody(string phoneNumber, string username) => new
    {
        username,
        displayName = "Listener",
        phoneNumber
    };

    private static async Task<string> EnsureDatabaseAsync()
    {
        var database = $"shenok_identity_{Guid.NewGuid():N}";
        var admin = Environment.GetEnvironmentVariable("SHENOK_POSTGRES_ADMIN")
            ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"""CREATE DATABASE "{database}" """, connection);
        await create.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(admin) { Database = database };
        return builder.ConnectionString;
    }
}
