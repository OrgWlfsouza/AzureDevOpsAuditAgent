namespace AzureDevOpsAuditAgent.Attributes;

/// <summary>
/// Attribute to associate an endpoint with a feature flag
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class FeatureFlagAttribute : Attribute
{
    /// <summary>
    /// Feature flag name
    /// </summary>
    public string FeatureName { get; }

    public FeatureFlagAttribute(string featureName)
    {
        FeatureName = featureName;
    }
}