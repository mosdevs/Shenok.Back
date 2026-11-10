var builder = DistributedApplication.CreateBuilder(args);

var internalApiKey = builder.AddParameter("internal-api-key", secret: true);
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);
var playbackSigningKey = builder.AddParameter("playback-signing-key", secret: true);

var postgres = builder.AddPostgres("postgres");
var identityDb = postgres.AddDatabase("identity");
var contentDb = postgres.AddDatabase("content");
var engagementDb = postgres.AddDatabase("engagement");
var recognitionDb = postgres.AddDatabase("recognition");

var blobs = builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs("blobs");
var rabbit = builder.AddRabbitMQ("rabbitmq");

// Project resource names stay off the connection-string names. A second resource named
// "identity" or "content" would collide with the database resource that emits that key.
var identity = builder.AddProject<Projects.Shenok_Back_IdentityService_Api>("identity-api", options =>
    {
        options.ExcludeLaunchProfile = true;
    })
    .WithHttpEndpoint(name: "http")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("InternalAuth__ApiKey", internalApiKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Database__MigrateOnStartup", "true");

var content = builder.AddDockerfile(
        "content-api",
        "../../..",
        "src/Services/Content/Shenok.Back.ContentService.Api/Dockerfile")
    .WithHttpEndpoint(targetPort: 8080, name: "http", isProxied: true)
    .WithReference(contentDb)
    .WithReference(blobs)
    .WithReference(rabbit)
    .WaitFor(contentDb)
    .WaitFor(blobs)
    .WaitFor(rabbit)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("InternalAuth__ApiKey", internalApiKey)
    .WithEnvironment("Playback__SigningKey", playbackSigningKey)
    .WithEnvironment("Playback__GatewayBaseUrl", "http://localhost:5080")
    .WithEnvironment("Messaging__Enabled", "true")
    .WithEnvironment("Database__MigrateOnStartup", "true");

var engagement = builder.AddProject<Projects.Shenok_Back_EngagementService_Api>("engagement-api", options =>
    {
        options.ExcludeLaunchProfile = true;
    })
    .WithHttpEndpoint(name: "http")
    .WithReference(engagementDb)
    .WaitFor(engagementDb)
    .WaitFor(content)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("InternalAuth__ApiKey", internalApiKey)
    .WithEnvironment("ContentCounters__BaseUrl", content.GetEndpoint("http"))
    .WithEnvironment("Database__MigrateOnStartup", "true");

var recognition = builder.AddProject<Projects.Shenok_Back_RecognitionService_Api>("recognition-api", options =>
    {
        options.ExcludeLaunchProfile = true;
    })
    .WithHttpEndpoint(name: "http")
    .WithReference(recognitionDb)
    .WithReference(rabbit)
    .WaitFor(recognitionDb)
    .WaitFor(rabbit)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("InternalAuth__ApiKey", internalApiKey)
    .WithEnvironment("Messaging__Enabled", "true")
    .WithEnvironment("Database__MigrateOnStartup", "true");

builder.AddProject<Projects.Shenok_Back_Gateway_Api>("gateway", options =>
    {
        options.ExcludeLaunchProfile = true;
    })
    .WithHttpEndpoint(port: 5080, name: "http")
    .WaitFor(identity)
    .WaitFor(content)
    .WaitFor(engagement)
    .WaitFor(recognition)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("InternalAuth__ApiKey", internalApiKey)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Playback__SigningKey", playbackSigningKey)
    .WithEnvironment("Playback__GatewayBaseUrl", "http://localhost:5080")
    .WithEnvironment("Gateway__IdentityBaseUrl", identity.GetEndpoint("http"))
    .WithEnvironment("Gateway__ContentBaseUrl", content.GetEndpoint("http"))
    .WithEnvironment("Gateway__EngagementBaseUrl", engagement.GetEndpoint("http"))
    .WithEnvironment("Gateway__RecognitionBaseUrl", recognition.GetEndpoint("http"))
    .WithEnvironment("Gateway__AllowFixtureDecoder", "false");

builder.Build().Run();
