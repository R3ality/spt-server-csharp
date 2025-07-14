using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Inventory;

namespace SPTarkov.Server.Core.Models.Eft.Quests;

public record HandoverQuestRequestData : InventoryBaseActionRequestData
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("qid")]
    public MongoId QuestId { get; set; }

    [JsonPropertyName("conditionId")]
    public MongoId ConditionId { get; set; }

    [JsonPropertyName("items")]
    public List<IdWithCount>? Items { get; set; }
}
