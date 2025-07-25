using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

public record LocationServices
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("TraderServerSettings")]
    public TraderServerSettings? TraderServerSettings { get; set; }

    [JsonPropertyName("BTRServerSettings")]
    public BtrServerSettings? BtrServerSettings { get; set; }
}

public record TraderServerSettings
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("TraderServices")]
    public TraderServices? TraderServices { get; set; }
}

public record TraderServices
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("ExUsecLoyalty")]
    public TraderService? ExUsecLoyalty { get; set; }

    [JsonPropertyName("ZryachiyAid")]
    public TraderService? ZryachiyAid { get; set; }

    [JsonPropertyName("CultistsAid")]
    public TraderService? CultistsAid { get; set; }

    [JsonPropertyName("PlayerTaxi")]
    public TraderService? PlayerTaxi { get; set; }

    [JsonPropertyName("BtrItemsDelivery")]
    public TraderService? BtrItemsDelivery { get; set; }

    [JsonPropertyName("BtrBotCover")]
    public TraderService? BtrBotCover { get; set; }

    [JsonPropertyName("TransitItemsDelivery")]
    public TraderService? TransitItemsDelivery { get; set; }
}

public record TraderService
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("TraderId")]
    public string? TraderId { get; set; }

    [JsonPropertyName("TraderServiceType")]
    public TraderServiceType? TraderServiceType { get; set; }

    [JsonPropertyName("Requirements")]
    public ServiceRequirements? Requirements { get; set; }

    [JsonPropertyName("ServiceItemCost")]
    [JsonConverter(typeof(ArrayToObjectFactoryConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public Dictionary<string, ServiceItemCostDetails>? ServiceItemCost { get; set; }

    [JsonPropertyName("UniqueItems")]
    public List<MongoId>? UniqueItems { get; set; }
}

public record ServiceRequirements
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("CompletedQuests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public List<CompletedQuest>? CompletedQuests { get; set; }

    [JsonPropertyName("Standings")]
    [JsonConverter(typeof(ArrayToObjectFactoryConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public Dictionary<string, StandingRequirement>? Standings { get; set; }
}

public record CompletedQuest
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("QuestId")]
    public string? QuestId { get; set; }
}

public record StandingRequirement
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("Value")]
    public double? Value { get; set; }
}

public record ServiceItemCostDetails
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("Count")]
    public int? Count { get; set; }
}

public record BtrServerSettings
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("ChanceSpawn")]
    public double? ChanceSpawn { get; set; }

    [JsonPropertyName("SpawnPeriod")]
    public XYZ? SpawnPeriod { get; set; }

    [JsonPropertyName("MoveSpeed")]
    public float? MoveSpeed { get; set; }

    [JsonPropertyName("ReadyToDepartureTime")]
    public float? ReadyToDepartureTime { get; set; }

    [JsonPropertyName("CheckTurnDistanceTime")]
    public float? CheckTurnDistanceTime { get; set; }

    [JsonPropertyName("TurnCheckSensitivity")]
    public float? TurnCheckSensitivity { get; set; }

    [JsonPropertyName("DecreaseSpeedOnTurnLimit")]
    public double? DecreaseSpeedOnTurnLimit { get; set; }

    [JsonPropertyName("EndSplineDecelerationDistance")]
    public double? EndSplineDecelerationDistance { get; set; }

    [JsonPropertyName("AccelerationSpeed")]
    public double? AccelerationSpeed { get; set; }

    [JsonPropertyName("DecelerationSpeed")]
    public double? DecelerationSpeed { get; set; }

    [JsonPropertyName("PauseDurationRange")]
    public XYZ? PauseDurationRange { get; set; }

    [JsonPropertyName("BodySwingReturnSpeed")]
    public float? BodySwingReturnSpeed { get; set; }

    [JsonPropertyName("BodySwingDamping")]
    public float? BodySwingDamping { get; set; }

    [JsonPropertyName("BodySwingIntensity")]
    public float? BodySwingIntensity { get; set; }

    [JsonPropertyName("ServerMapBTRSettings")]
    public Dictionary<string, ServerMapBtrsettings>? ServerMapBTRSettings { get; set; }
}

public record ServerMapBtrsettings
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("MapID")]
    public string? MapID { get; set; }

    [JsonPropertyName("ChanceSpawn")]
    public double? ChanceSpawn { get; set; }

    [JsonPropertyName("SpawnPeriod")]
    public XYZ? SpawnPeriod { get; set; }

    [JsonPropertyName("MoveSpeed")]
    public float? MoveSpeed { get; set; }

    [JsonPropertyName("ReadyToDepartureTime")]
    public float? ReadyToDepartureTime { get; set; }

    [JsonPropertyName("CheckTurnDistanceTime")]
    public float? CheckTurnDistanceTime { get; set; }

    [JsonPropertyName("TurnCheckSensitivity")]
    public float? TurnCheckSensitivity { get; set; }

    [JsonPropertyName("DecreaseSpeedOnTurnLimit")]
    public float? DecreaseSpeedOnTurnLimit { get; set; }

    [JsonPropertyName("EndSplineDecelerationDistance")]
    public float? EndSplineDecelerationDistance { get; set; }

    [JsonPropertyName("AccelerationSpeed")]
    public float? AccelerationSpeed { get; set; }

    [JsonPropertyName("DecelerationSpeed")]
    public float? DecelerationSpeed { get; set; }

    [JsonPropertyName("PauseDurationRange")]
    public XYZ? PauseDurationRange { get; set; }

    [JsonPropertyName("BodySwingReturnSpeed")]
    public float? BodySwingReturnSpeed { get; set; }

    [JsonPropertyName("BodySwingDamping")]
    public float? BodySwingDamping { get; set; }

    [JsonPropertyName("BodySwingIntensity")]
    public float? BodySwingIntensity { get; set; }
}
