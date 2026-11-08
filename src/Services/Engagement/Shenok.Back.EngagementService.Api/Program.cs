using Shenok.Back.Api.Shared;
using Shenok.Back.EngagementService.Api;
using Shenok.Back.EngagementService.Application;
using Shenok.Back.EngagementService.Infrastructure;
using Shenok.Back.EngagementService.Infrastructure.Persistence;
using ApplicationDependencyInjection = Shenok.Back.EngagementService.Application.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
EngagementHost.RequireApiKey(builder.Configuration);
builder.Services.AddEngagementApplication(builder.Configuration);
builder.Services.AddEngagementInfrastructure(builder.Configuration);
builder.Services.AddShenokApi(typeof(ApplicationDependencyInjection).Assembly);
builder.Services.AddHealthChecks().AddDbContextCheck<EngagementDbContext>();

var app = builder.Build();
app.UseShenokApi();
app.MapControllers();
app.MapHealthChecks("/health");

await EngagementDatabase.MigrateAsync(app.Services);

app.Run();

public partial class Program;
