using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Inventory;

namespace SPTarkov.Server.Core.Models.Eft.Hideout;

public record HideoutCustomizationSetMannequinPoseRequest : InventoryBaseActionRequestData
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("poses")]
    public Dictionary<MongoId, MongoId>? Poses { get; set; }

    [JsonPropertyName("timestamp")]
    public double? Timestamp { get; set; }
}
