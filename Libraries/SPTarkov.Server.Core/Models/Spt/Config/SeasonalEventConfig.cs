using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Spt.Config;

public record SeasonalEventConfig : BaseConfig
{
    [JsonPropertyName("kind")]
    public override string Kind { get; set; } = "spt-seasonalevents";

    [JsonPropertyName("enableSeasonalEventDetection")]
    public bool EnableSeasonalEventDetection { get; set; }

    /// <summary>
    ///     event / botType / equipSlot / itemid
    /// </summary>
    [JsonPropertyName("eventGear")]
    public required Dictionary<
        SeasonalEventType,
        Dictionary<string, Dictionary<string, Dictionary<MongoId, int>>>
    > EventGear { get; set; }

    /// <summary>
    ///     event / bot type / equipSlot / itemid
    /// </summary>
    [JsonPropertyName("eventLoot")]
    public required Dictionary<
        SeasonalEventType,
        Dictionary<string, Dictionary<string, Dictionary<MongoId, int>>>
    > EventLoot { get; set; }

    [JsonPropertyName("events")]
    public required List<SeasonalEvent> Events { get; set; }

    [JsonPropertyName("eventBotMapping")]
    public required Dictionary<string, string> EventBotMapping { get; set; }

    [JsonPropertyName("eventBossSpawns")]
    public required Dictionary<
        string,
        Dictionary<string, List<BossLocationSpawn>>
    > EventBossSpawns { get; set; }

    [JsonPropertyName("eventWaves")]
    public required Dictionary<string, Dictionary<string, List<Wave>>> EventWaves { get; set; }

    [JsonPropertyName("gifterSettings")]
    public required List<GifterSetting> GifterSettings { get; set; }

    /// <summary>
    ///     key = event, second key = map name
    /// </summary>
    [JsonPropertyName("hostilitySettingsForEvent")]
    public required Dictionary<
        string,
        Dictionary<string, List<AdditionalHostilitySettings>>
    > HostilitySettingsForEvent { get; set; }

    /// <summary>
    ///     Ids of containers on locations that only have Christmas loot
    /// </summary>
    [JsonPropertyName("christmasContainerIds")]
    public required List<string> ChristmasContainerIds { get; set; }

    /// <summary>
    ///     Season - botType - location (body/feet/hands/head)
    /// </summary>
    [JsonPropertyName("botAppearanceChanges")]
    public required Dictionary<
        SeasonalEventType,
        Dictionary<string, Dictionary<string, Dictionary<MongoId, int>>>
    > BotAppearanceChanges { get; set; }
}

public record SeasonalEvent
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public SeasonalEventType Type { get; set; }

    [JsonPropertyName("startDay")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public int StartDay { get; set; }

    [JsonPropertyName("startMonth")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public int StartMonth { get; set; }

    [JsonPropertyName("endDay")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public int EndDay { get; set; }

    [JsonPropertyName("endMonth")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public int EndMonth { get; set; }

    [JsonPropertyName("settings")]
    public SeasonalEventSettings? Settings { get; set; }

    [JsonPropertyName("setting")]
    public SeasonalEventSettings? SettingsDoNOTUse
    {
        set { Settings = value; }
    }
}

public record SeasonalEventSettings
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("enableSummoning")]
    public bool? EnableSummoning { get; set; }

    [JsonPropertyName("enableHalloweenHideout")]
    public bool? EnableHalloweenHideout { get; set; }

    [JsonPropertyName("enableChristmasHideout")]
    public bool? EnableChristmasHideout { get; set; }

    [JsonPropertyName("enableSanta")]
    public bool? EnableSanta { get; set; }

    [JsonPropertyName("adjustBotAppearances")]
    public bool? AdjustBotAppearances { get; set; }

    [JsonPropertyName("addEventGearToBots")]
    public bool? AddEventGearToBots { get; set; }

    [JsonPropertyName("addEventLootToBots")]
    public bool? AddEventLootToBots { get; set; }

    [JsonPropertyName("removeEntryRequirement")]
    public List<string>? RemoveEntryRequirement { get; set; }

    [JsonPropertyName("replaceBotHostility")]
    public bool? ReplaceBotHostility { get; set; }

    [JsonPropertyName("forceSeason")]
    public Season? ForceSeason { get; set; }

    [JsonPropertyName("zombieSettings")]
    public ZombieSettings? ZombieSettings { get; set; }

    [JsonPropertyName("disableBosses")]
    public List<string>? DisableBosses { get; set; }

    [JsonPropertyName("disableWaves")]
    public List<string>? DisableWaves { get; set; }
}

public record ZombieSettings
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("mapInfectionAmount")]
    public Dictionary<string, double>? MapInfectionAmount { get; set; }

    [JsonPropertyName("disableBosses")]
    public List<string>? DisableBosses { get; set; }

    [JsonPropertyName("disableWaves")]
    public List<string>? DisableWaves { get; set; }
}

public record GifterSetting
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("map")]
    public string? Map { get; set; }

    [JsonPropertyName("zones")]
    public string? Zones { get; set; }

    [JsonPropertyName("spawnChance")]
    public int? SpawnChance { get; set; }
}
