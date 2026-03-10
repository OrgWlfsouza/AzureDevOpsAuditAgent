using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using AzureDevOpsAuditAgent.Attributes;

namespace AzureDevOpsAuditAgent.Swagger;

/// <summary>
/// Swagger filter that marks operations with disabled feature flags
/// </summary>
public class FeatureFlagOperationFilter : IOperationFilter
{
    private readonly IConfiguration _configuration;

    public FeatureFlagOperationFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check if the endpoint has the FeatureFlag attribute
        var featureFlagAttribute = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<FeatureFlagAttribute>()
            .FirstOrDefault();

        // If not found on method, check the class (controller)
        featureFlagAttribute ??= context.MethodInfo.DeclaringType?
            .GetCustomAttributes(true)
            .OfType<FeatureFlagAttribute>()
            .FirstOrDefault();

        if (featureFlagAttribute != null)
        {
            // Read directly from appsettings.json
            var featureKey = $"FeatureManagement:{featureFlagAttribute.FeatureName}";
            var isEnabledStr = _configuration[featureKey];
            var isEnabled = bool.TryParse(isEnabledStr, out var enabled) && enabled;

            Console.WriteLine($"🔍 Swagger Filter - Endpoint: {context.MethodInfo.Name}, Feature: {featureFlagAttribute.FeatureName}, Enabled: {isEnabled}");

            if (!isEnabled)
            {
                // Mark the operation as obsolete to be removed
                operation.Deprecated = true;
                
                // Add description indicating it's disabled
                var warningMessage = $"⚠️ **FEATURE DISABLED**: This operation is temporarily disabled by the feature flag '{featureFlagAttribute.FeatureName}'.";
                operation.Description = string.IsNullOrEmpty(operation.Description) 
                    ? warningMessage 
                    : $"{warningMessage}\n\n{operation.Description}";
                
                // Add special tag
                operation.Tags ??= new List<OpenApiTag>();
                if (!operation.Tags.Any(t => t.Name == "🚫 Disabled"))
                {
                    operation.Tags.Add(new OpenApiTag { Name = "🚫 Disabled" });
                }

                Console.WriteLine($"   🚫 Endpoint {context.MethodInfo.Name} MARKED FOR REMOVAL");
            }
            else
            {
                // Add visual indicator that it's active
                if (!string.IsNullOrEmpty(operation.Summary) && !operation.Summary.StartsWith("✅"))
                {
                    operation.Summary = $"✅ {operation.Summary}";
                }
                Console.WriteLine($"   ✅ Endpoint {context.MethodInfo.Name} ACTIVE");
            }
        }
    }
}