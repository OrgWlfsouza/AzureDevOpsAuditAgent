using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AzureDevOpsAuditAgent.Swagger;

/// <summary>
/// Document filter that completely removes endpoints with disabled features
/// </summary>
public class FeatureFlagDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        Console.WriteLine("🔧 Document Filter - Starting removal of disabled endpoints...");

        var pathsToRemove = new List<string>();

        foreach (var path in swaggerDoc.Paths.ToList())
        {
            Console.WriteLine($"   📂 Checking path: {path.Key}");
            
            var operationsToRemove = new List<OperationType>();

            foreach (var operation in path.Value.Operations.ToList())
            {
                // If the operation is marked as deprecated (disabled feature), remove it
                if (operation.Value.Deprecated)
                {
                    Console.WriteLine($"      ❌ Removing operation {operation.Key} (deprecated)");
                    operationsToRemove.Add(operation.Key);
                }
                else
                {
                    Console.WriteLine($"      ✅ Keeping operation {operation.Key}");
                }
            }

            // Remove disabled operations
            foreach (var operationType in operationsToRemove)
            {
                path.Value.Operations.Remove(operationType);
            }

            // If no operations remain in the path, mark path for removal
            if (!path.Value.Operations.Any())
            {
                Console.WriteLine($"   🗑️ Path {path.Key} will be removed (no operations)");
                pathsToRemove.Add(path.Key);
            }
        }

        // Remove empty paths
        foreach (var path in pathsToRemove)
        {
            Console.WriteLine($"🗑️ Removing path: {path}");
            swaggerDoc.Paths.Remove(path);
        }

        // Remove "Disabled" tag if it exists
        if (swaggerDoc.Tags != null)
        {
            var disabledTag = swaggerDoc.Tags.FirstOrDefault(t => t.Name == "🚫 Desabilitado");
            if (disabledTag != null)
            {
                swaggerDoc.Tags.Remove(disabledTag);
                Console.WriteLine($"🗑️ Removed tag '🚫 Desabilitado'");
            }
        }

        Console.WriteLine("✅ Document Filter - Completed!");
    }
}