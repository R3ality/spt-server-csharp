using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace SPTarkov.Server.Core.Models.Spt.Bots;

public record FilterPlateModsForSlotByLevelResult
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("result")]
    public Result? Result { get; set; }

    [JsonPropertyName("plateModTpls")]
    public HashSet<MongoId>? PlateModTemplates { get; set; }
}

public enum Result
{
    UNKNOWN_FAILURE = -1,
    SUCCESS = 1,
    NO_DEFAULT_FILTER = 2,
    NOT_PLATE_HOLDING_SLOT = 3,
    LACKS_PLATE_WEIGHTS = 4,
}
