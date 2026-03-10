using Microsoft.FeatureManagement;
using AzureDevOpsAuditAgent.Class;
using AzureDevOpsAuditAgent.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Enable detailed logging for debugging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Feature Management
builder.Services.AddFeatureManagement();

// Services
builder.Services.AddHttpClient<AzureDevOpsService>();
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Azure DevOps Audit Agent API",
        Version = "v1",
        Description = "API for Azure DevOps management and auditing.\n\n" +
                     "⚠️ **Note**: Endpoints disabled by feature flags do not appear in this documentation.",
    });

    // IMPORTANT: Register filters in the correct order
    options.OperationFilter<FeatureFlagOperationFilter>();
    options.DocumentFilter<FeatureFlagDocumentFilter>();

    // XML Comments (optional)
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure DevOps Audit Agent API v1");
        options.RoutePrefix = string.Empty;
        options.DocumentTitle = "Azure DevOps Audit Agent";
        options.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
