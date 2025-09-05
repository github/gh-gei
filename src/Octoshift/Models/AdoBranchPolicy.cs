using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octoshift.Models;

/// <summary>
/// Represents an Azure DevOps branch policy configuration
/// </summary>
public class AdoBranchPolicy
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("type")]
    public AdoPolicyType Type { get; set; }

    [JsonProperty("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonProperty("settings")]
    public AdoBranchPolicySettings Settings { get; set; }
}

/// <summary>
/// Represents the type information for an Azure DevOps policy
/// </summary>
public class AdoPolicyType
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("displayName")]
    public string DisplayName { get; set; }
}

/// <summary>
/// Represents the settings for an Azure DevOps branch policy
/// </summary>
public class AdoBranchPolicySettings
{
    [JsonProperty("buildDefinitionId")]
    public string BuildDefinitionId { get; set; }

    [JsonProperty("displayName")]
    public string DisplayName { get; set; }

    [JsonProperty("queueOnSourceUpdateOnly")]
    public bool QueueOnSourceUpdateOnly { get; set; }

    [JsonProperty("manualQueueOnly")]
    public bool ManualQueueOnly { get; set; }

    [JsonProperty("validDuration")]
    public double ValidDuration { get; set; }
}

/// <summary>
/// Represents the response wrapper for Azure DevOps branch policies
/// </summary>
public class AdoBranchPolicyResponse
{
    [JsonProperty("value")]
    public IReadOnlyList<AdoBranchPolicy> Value { get; set; }

    [JsonProperty("count")]
    public int Count { get; set; }
}
