using Shenok.Back.EngagementService.Api;
using Shenok.Back.EngagementService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
EngagementHost.AddServices(builder.Services, builder.Configuration, includeInfrastructure: true);

var app = builder.Build();
EngagementHost.Map(app);
await EngagementDatabase.MigrateAsync(app.Services);

app.Run();

public partial class Program;
