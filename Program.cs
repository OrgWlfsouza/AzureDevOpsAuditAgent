using AzureDevOpsAuditAgent.Class;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Annotations;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<AzureDevOpsService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Adicionar Swagger com configuração avançada
builder.Services.AddSwaggerGen(options =>
{
    // Habilitar anotações do Swashbuckle
    options.EnableAnnotations();

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Azure DevOps Audit Agent API",
        Description = "API para auditoria e consulta de informações do Azure DevOps",
        Contact = new OpenApiContact
        {
            Name = "Suporte",
            Email = "suporte@exemplo.com"
        }
    });

    // Adicionar comentários XML para documentação enriquecida
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Gerar Operation IDs baseados no nome do método
    options.CustomOperationIds(apiDesc =>
    {
        return apiDesc.TryGetMethodInfo(out MethodInfo methodInfo)
            ? methodInfo.Name
            : null;
    });

    // Configurar servidor dinamicamente baseado no ambiente Azure
    var websiteHostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
    if (!string.IsNullOrEmpty(websiteHostname))
    {
        // Em produção no Azure App Service
        options.AddServer(new OpenApiServer
        {
            Url = $"https://{websiteHostname}",
            Description = "Servidor de Produção (Azure)"
        });
    }
    else
    {
        // Em desenvolvimento local
        options.AddServer(new OpenApiServer
        {
            Url = "https://localhost:44377",
            Description = "Servidor de Desenvolvimento (HTTPS)"
        });

        options.AddServer(new OpenApiServer
        {
            Url = "http://localhost:44377",
            Description = "Servidor de Desenvolvimento (HTTP)"
        });
    }
});

var app = builder.Build();

// Habilitar Swagger em todos os ambientes
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure DevOps Audit Agent API v1");
    options.RoutePrefix = string.Empty; // Swagger na raiz
    options.DisplayOperationId(); // Exibir Operation IDs na UI
    options.DisplayRequestDuration(); // Exibir duração das requisições
});

app.MapControllers();
app.Run();
