using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils.Json;

namespace SPTarkov.Server.Core.Models.Eft.Common;

public record Location
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    /// <summary>
    ///     Map meta-data
    /// </summary>
    [JsonPropertyName("base")]
    public LocationBase Base { get; set; }

    /// <summary>
    ///     Loose loot positions and item weights
    /// </summary>
    [JsonPropertyName("looseLoot")]
    public LazyLoad<LooseLoot>? LooseLoot { get; set; }

    /// <summary>
    ///     Static loot item weights
    /// </summary>
    [JsonPropertyName("staticLoot")]
    public LazyLoad<Dictionary<string, StaticLootDetails>>? StaticLoot { get; set; }

    /// <summary>
    ///     Static container positions and item weights
    /// </summary>
    [JsonPropertyName("staticContainers")]
    public LazyLoad<StaticContainerDetails>? StaticContainers { get; set; }

    [JsonPropertyName("staticAmmo")]
    public Dictionary<string, List<StaticAmmoDetails>> StaticAmmo { get; set; }

    /// <summary>
    ///     All possible static containers on map + their assign groupings
    /// </summary>
    [JsonPropertyName("statics")]
    public StaticContainer? Statics { get; set; }

    /// <summary>
    ///     All possible map extracts
    /// </summary>
    [JsonPropertyName("allExtracts")]
    public Exit[] AllExtracts { get; set; }
}

public record StaticContainer
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("containersGroups")]
    public Dictionary<string, ContainerMinMax>? ContainersGroups { get; set; }

    [JsonPropertyName("containers")]
    public Dictionary<string, ContainerData>? Containers { get; set; }
}

public record ContainerMinMax
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("minContainers")]
    public int? MinContainers { get; set; }

    [JsonPropertyName("maxContainers")]
    public int? MaxContainers { get; set; }

    [JsonPropertyName("current")]
    public int? Current { get; set; }

    [JsonPropertyName("chosenCount")]
    public int? ChosenCount { get; set; }
}

public record ContainerData
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }
}

public record StaticLootDetails
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("itemcountDistribution")]
    public ItemCountDistribution[] ItemCountDistribution { get; set; }

    [JsonPropertyName("itemDistribution")]
    public ItemDistribution[] ItemDistribution { get; set; }
}

public record ItemCountDistribution
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("relativeProbability")]
    public float? RelativeProbability { get; set; }
}

public record ItemDistribution
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("tpl")]
    public MongoId Tpl { get; set; }

    [JsonPropertyName("relativeProbability")]
    public float? RelativeProbability { get; set; }
}

public record StaticContainerDetails
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("staticWeapons")]
    public List<SpawnpointTemplate> StaticWeapons { get; set; }

    [JsonPropertyName("staticContainers")]
    public List<StaticContainerData> StaticContainers { get; set; }

    [JsonPropertyName("staticForced")]
    public List<StaticForced> StaticForced { get; set; }
}

public record StaticForced
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("containerId")]
    public string ContainerId { get; set; }

    [JsonPropertyName("itemTpl")]
    public MongoId ItemTpl { get; set; }
}

public record StaticContainerData
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("probability")]
    public float? Probability { get; set; }

    [JsonPropertyName("template")]
    public SpawnpointTemplate? Template { get; set; }
}

public record StaticAmmoDetails
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("tpl")]
    public MongoId? Tpl { get; set; }

    [JsonPropertyName("relativeProbability")]
    public float? RelativeProbability { get; set; }
}
