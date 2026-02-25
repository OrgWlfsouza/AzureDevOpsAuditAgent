using AzureDevOpsAuditAgent.Class;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<AzureDevOpsService>();
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
