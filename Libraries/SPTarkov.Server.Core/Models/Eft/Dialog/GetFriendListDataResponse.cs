using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Profile;

namespace SPTarkov.Server.Core.Models.Eft.Dialog;

public record GetFriendListDataResponse
{
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonPropertyName("Friends")]
    public List<UserDialogInfo>? Friends { get; set; }

    [JsonPropertyName("Ignore")]
    public List<string>? Ignore { get; set; }

    [JsonPropertyName("InIgnoreList")]
    public List<string>? InIgnoreList { get; set; }
}
