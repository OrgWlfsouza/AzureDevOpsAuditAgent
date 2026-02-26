using AzureDevOpsAuditAgent.Class;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models; // ✅ ajuste aqui
using Swashbuckle.AspNetCore.SwaggerGen;

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

    // ✅ Garante que cada endpoint tenha operationId baseado no nome do método
    options.CustomOperationIds(apiDesc =>
        apiDesc.TryGetMethodInfo(out var methodInfo) ? methodInfo.Name : null);
});

var app = builder.Build();

// Middleware para responder HEAD em /swagger/v1/swagger.json
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/swagger/v1/swagger.json") &&
        context.Request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        return;
    }
    await next();
});

// Habilitar Swagger em todos os ambientes
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json"; // ✅ garante o caminho correto
});

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure DevOps Audit Agent API v1");
    options.RoutePrefix = string.Empty; // Swagger UI na raiz
});

app.MapControllers();
app.Run();