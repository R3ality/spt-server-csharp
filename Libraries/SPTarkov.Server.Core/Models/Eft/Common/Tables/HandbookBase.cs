using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Eft.Common.Tables;

public record HandbookBase
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("Categories")]
    public List<HandbookCategory> Categories { get; set; }

    [JsonPropertyName("Items")]
    public List<HandbookItem> Items { get; set; }
}

public record HandbookCategory
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("Id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("ParentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public MongoId? ParentId { get; set; }

    [JsonPropertyName("Icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string Icon { get; set; }

    [JsonPropertyName("Color")]
    public string Color { get; set; }

    [JsonPropertyName("Order")]
    public string Order { get; set; }
}

public record HandbookItem
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("Id")]
    public MongoId Id { get; set; }

    [JsonPropertyName("ParentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public MongoId ParentId { get; set; }

    [JsonPropertyName("Price")]
    public double? Price { get; set; }
}
