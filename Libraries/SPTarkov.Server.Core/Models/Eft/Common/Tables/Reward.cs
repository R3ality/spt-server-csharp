using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

public record Reward
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(StringToNumberFactoryConverter))]
    public double? Value { get; set; }

    [JsonPropertyName("id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RewardType? Type { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; } // Can be more than just mongoId

    [JsonPropertyName("items")]
    public List<Item>? Items { get; set; }

    [JsonPropertyName("loyaltyLevel")]
    public int? LoyaltyLevel { get; set; }

    /// <summary>
    ///     Hideout area id
    /// </summary>
    [JsonPropertyName("traderId")]
    public object? TraderId { get; set; } // TODO: string | int

    [JsonPropertyName("isEncoded")]
    public bool? IsEncoded { get; set; }

    [JsonPropertyName("unknown")]
    public bool? Unknown { get; set; }

    [JsonPropertyName("findInRaid")]
    public bool? FindInRaid { get; set; }

    [JsonPropertyName("gameMode")]
    public IEnumerable<string>? GameMode { get; set; }

    /// <summary>
    ///     Game editions whitelisted to get reward
    /// </summary>
    [JsonPropertyName("availableInGameEditions")]
    public HashSet<string>? AvailableInGameEditions { get; set; }

    /// <summary>
    ///     Game editions blacklisted from getting reward
    /// </summary>
    [JsonPropertyName("notAvailableInGameEditions")]
    public HashSet<string>? NotAvailableInGameEditions { get; set; }

    // This is always Null atm in the achievements.json
    [JsonPropertyName("illustrationConfig")]
    public object? IllustrationConfig { get; set; }

    [JsonPropertyName("isHidden")]
    public bool? IsHidden { get; set; }
}
