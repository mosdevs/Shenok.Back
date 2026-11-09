using Shenok.Back.RecognitionService.Api;
using Shenok.Back.RecognitionService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
RecognitionHost.RequireApiKey(builder.Configuration);
RecognitionHost.AddServices(builder.Services, builder.Configuration, includeInfrastructure: true);

var app = builder.Build();
RecognitionHost.Map(app);
await RecognitionDatabase.MigrateAsync(app.Services);

app.Run();

public partial class Program;
