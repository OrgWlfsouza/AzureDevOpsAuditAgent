using AzureDevOpsAuditAgent.Class;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<AzureDevOpsService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Adicionar Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Azure DevOps Audit Agent API",
        Description = "API para auditoria do Azure DevOps",
    });
});

var app = builder.Build();

// Habilitar Swagger em todos os ambientes (remover o if)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure DevOps Audit Agent API v1");
    options.RoutePrefix = string.Empty; // Swagger na raiz
});

app.MapControllers();
app.Run();