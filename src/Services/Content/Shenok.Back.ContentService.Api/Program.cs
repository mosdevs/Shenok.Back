using Microsoft.EntityFrameworkCore;
using Shenok.Back.Api.Shared;
using Shenok.Back.ContentService.Api;
using Shenok.Back.ContentService.Application;
using Shenok.Back.ContentService.Infrastructure;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using ApplicationDependencyInjection = Shenok.Back.ContentService.Application.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
ContentHost.RequireApiKey(builder.Configuration);
builder.Services.AddContentApplication();
builder.Services.AddContentInfrastructure(builder.Configuration);
builder.Services.AddContentApi(builder.Configuration);
builder.Services.AddShenokApi(typeof(ApplicationDependencyInjection).Assembly);
builder.Services.AddContentSwagger();
builder.Services.AddHealthChecks().AddDbContextCheck<ContentDbContext>();

var app = builder.Build();
app.UseContentSwagger();
app.UseShenokApi();
app.MapControllers();
app.MapHealthChecks("/health");

if (ContentHost.ShouldMigrate(app.Configuration))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
    await database.Database.MigrateAsync();
}

app.Run();

public partial class Program;
