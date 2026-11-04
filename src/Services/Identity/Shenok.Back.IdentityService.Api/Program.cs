using Microsoft.EntityFrameworkCore;
using Shenok.Back.Api.Shared;
using Shenok.Back.IdentityService.Api;
using Shenok.Back.IdentityService.Application;
using Shenok.Back.IdentityService.Infrastructure;
using Shenok.Back.IdentityService.Infrastructure.Persistence;
using ApplicationDependencyInjection = Shenok.Back.IdentityService.Application.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddShenokApi(typeof(ApplicationDependencyInjection).Assembly);
builder.Services.AddIdentitySwagger();
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>();

var app = builder.Build();
app.UseIdentitySwagger();
app.UseShenokApi();
app.MapControllers();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await database.Database.MigrateAsync();
}

app.Run();

public partial class Program;
