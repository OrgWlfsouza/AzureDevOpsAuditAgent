using AzureDevOpsAuditAgent.Class;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<AzureDevOpsService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapControllers();
app.Run();