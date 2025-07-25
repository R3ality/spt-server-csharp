using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Inventory;

public record InventoryDeleteMarkerRequestData : InventoryBaseActionRequestData
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("item")]
    public MongoId? Item { get; set; }

    [JsonPropertyName("X")]
    public int? X { get; set; }

    [JsonPropertyName("Y")]
    public int? Y { get; set; }
}
