using Shenok.Back.Gateway.Api;

var builder = WebApplication.CreateBuilder(args);
GatewayHost.AddServices(builder.Services, builder.Configuration);

var app = builder.Build();
GatewayHost.Map(app);

app.Run();

public partial class Program;
