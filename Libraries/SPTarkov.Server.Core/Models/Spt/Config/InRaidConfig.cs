using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Spt.Config;

public record InRaidConfig : BaseConfig
{
    [JsonPropertyName("kind")]
    public override string Kind { get; set; } = "spt-inraid";

    /// <summary>
    ///     Overrides to apply to the pre-raid settings screen
    /// </summary>
    [JsonPropertyName("raidMenuSettings")]
    public required RaidMenuSettings RaidMenuSettings { get; set; }

    /// <summary>
    ///     Names of car extracts
    /// </summary>
    [JsonPropertyName("carExtracts")]
    public required HashSet<string> CarExtracts { get; set; }

    /// <summary>
    ///     Names of coop extracts
    /// </summary>
    [JsonPropertyName("coopExtracts")]
    public required HashSet<string> CoopExtracts { get; set; }

    /// <summary>
    ///     Fence rep gain from a single car extract
    /// </summary>
    [JsonPropertyName("carExtractBaseStandingGain")]
    public double CarExtractBaseStandingGain { get; set; }

    /// <summary>
    ///     Fence rep gain from a single coop extract
    /// </summary>
    [JsonPropertyName("coopExtractBaseStandingGain")]
    public double CoopExtractBaseStandingGain { get; set; }

    /// <summary>
    ///     Fence rep gain when successfully extracting as pscav
    /// </summary>
    [JsonPropertyName("scavExtractStandingGain")]
    public double ScavExtractStandingGain { get; set; }

    /// <summary>
    ///     On death should items in your secure keep their Find in raid status regardless of how you finished the raid
    /// </summary>
    [JsonPropertyName("keepFiRSecureContainerOnDeath")]
    public bool KeepFiRSecureContainerOnDeath { get; set; }

    /// <summary>
    ///     If enabled always keep found in raid status on items
    /// </summary>
    [JsonPropertyName("alwaysKeepFoundInRaidOnRaidEnd")]
    public bool AlwaysKeepFoundInRaidOnRaidEnd { get; set; }

    /// <summary>
    ///     Percentage chance a player scav hot is hostile to the player when scavving
    /// </summary>
    [JsonPropertyName("playerScavHostileChancePercent")]
    public double PlayerScavHostileChancePercent { get; set; }
}

public record RaidMenuSettings
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("aiAmount")]
    public required string AiAmount { get; set; }

    [JsonPropertyName("aiDifficulty")]
    public required string AiDifficulty { get; set; }

    [JsonPropertyName("bossEnabled")]
    public bool BossEnabled { get; set; }

    [JsonPropertyName("scavWars")]
    public bool ScavWars { get; set; }

    [JsonPropertyName("taggedAndCursed")]
    public bool TaggedAndCursed { get; set; }

    [JsonPropertyName("enablePve")]
    public bool EnablePve { get; set; }

    [JsonPropertyName("randomWeather")]
    public bool RandomWeather { get; set; }

    [JsonPropertyName("randomTime")]
    public bool RandomTime { get; set; }
}
