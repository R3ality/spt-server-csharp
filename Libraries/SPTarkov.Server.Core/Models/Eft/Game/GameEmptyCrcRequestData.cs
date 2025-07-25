using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTarkov.Server.Core.Models.Eft.Game;

public record GameEmptyCrcRequestData : IRequestData
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("crc")]
    public int? Crc { get; set; }
}
