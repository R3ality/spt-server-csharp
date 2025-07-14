using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Inventory;

public record OpenRandomLootContainerRequestData : InventoryBaseActionRequestData
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    /// <summary>
    ///     Container item id being opened
    /// </summary>
    [JsonPropertyName("item")]
    public MongoId Item { get; set; }

    [JsonPropertyName("to")]
    public List<ItemEvent.To>? To { get; set; }
}
