using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTarkov.Server.Core.Models.Spt.Fence;

public record CreateFenceAssortsResult
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("sptItems")]
    public List<List<Item>>? SptItems { get; set; }

    [JsonPropertyName("barter_scheme")]
    public Dictionary<MongoId, List<List<BarterScheme>>>? BarterScheme { get; set; }

    [JsonPropertyName("loyal_level_items")]
    public Dictionary<MongoId, int>? LoyalLevelItems { get; set; }
}
