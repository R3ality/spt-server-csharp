using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Helpers.Dialogue.SPTFriend.Commands;

[Injectable]
public class ForceHalloweenMessageHandler(
    ServerLocalisationService _serverLocalisationService,
    MailSendService _mailSendService,
    RandomUtil _randomUtil,
    SeasonalEventService _seasonalEventService
) : IChatMessageHandler
{
    public int GetPriority()
    {
        return 99;
    }

    public bool CanHandle(string message)
    {
        return string.Equals(message, "veryspooky", StringComparison.OrdinalIgnoreCase);
    }

    public void Process(
        string sessionId,
        UserDialogInfo sptFriendUser,
        PmcData? sender,
        object? extraInfo = null
    )
    {
        var enableEventResult = _seasonalEventService.ForceSeasonalEvent(
            SeasonalEventType.Halloween
        );
        if (enableEventResult)
        {
            _mailSendService.SendUserMessageToPlayer(
                sessionId,
                sptFriendUser,
                _randomUtil.GetArrayValue(
                    [
                        _serverLocalisationService.GetText(
                            "chatbot-forced_event_enabled",
                            SeasonalEventType.Halloween
                        ),
                    ]
                ),
                [],
                null
            );
        }
    }
}
