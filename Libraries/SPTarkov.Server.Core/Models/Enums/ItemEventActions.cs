using System.Text.Json.Serialization;

namespace SPTarkov.Server.Core.Models.Enums;

public record ItemEventActions
{
    [JsonExtensionData]
    public Dictionary<string, object> ExtensionData { get; set; }

    public const string MOVE = "Move";
    public const string REMOVE = "Remove";
    public const string SPLIT = "Split";
    public const string MERGE = "Merge";
    public const string TRANSFER = "Transfer";
    public const string SWAP = "Swap";
    public const string FOLD = "Fold";
    public const string TOGGLE = "Toggle";
    public const string TAG = "Tag";
    public const string BIND = "Bind";
    public const string UNBIND = "Unbind";
    public const string EXAMINE = "Examine";
    public const string READ_ENCYCLOPEDIA = "ReadEncyclopedia";
    public const string APPLY_INVENTORY_CHANGES = "ApplyInventoryChanges";
    public const string CREATE_MAP_MARKER = "CreateMapMarker";
    public const string DELETE_MAP_MARKER = "DeleteMapMarker";
    public const string EDIT_MAP_MARKER = "EditMapMarker";
    public const string OPEN_RANDOM_LOOT_CONTAINER = "OpenRandomLootContainer";
    public const string HIDEOUT_QTE_EVENT = "HideoutQuickTimeEvent";
    public const string SAVE_WEAPON_BUILD = "SaveWeaponBuild";
    public const string REMOVE_WEAPON_BUILD = "RemoveWeaponBuild";
    public const string REMOVE_BUILD = "RemoveBuild";
    public const string SAVE_EQUIPMENT_BUILD = "SaveEquipmentBuild";
    public const string REMOVE_EQUIPMENT_BUILD = "RemoveEquipmentBuild";
    public const string REDEEM_PROFILE_REWARD = "RedeemProfileReward";
    public const string SET_FAVORITE_ITEMS = "SetFavoriteItems";
    public const string QUEST_FAIL = "QuestFail";
    public const string PIN_LOCK = "PinLock";
    public const string ADD_NOTE = "AddNote";
    public const string EDIT_NOTE = "EditNote";
    public const string DELETE_NOTE = "DeleteNote";
    public const string REPEATABLE_QUEST_CHANGE = "RepeatableQuestChange";
    public const string QUEST_HANDOVER = "QuestHandover";
    public const string QUEST_COMPLETE = "QuestComplete";
    public const string QUEST_ACCEPT = "QuestAccept";
    public const string RAGFAIR_RENEW_OFFER = "RagFairRenewOffer";
    public const string RAGFAIR_REMOVE_OFFER = "RagFairRemoveOffer";
    public const string RAGFAIR_ADD_OFFER = "RagFairAddOffer";
    public const string TRADER_REPAIR = "TraderRepair";
    public const string REPAIR = "Repair";
    public const string SELL_ALL_FROM_SAVAGE = "SellAllFromSavage";
    public const string RAGFAIR_BUY_OFFER = "RagFairBuyOffer";
    public const string TRADING_CONFIRM = "TradingConfirm";
    public const string BUY_FROM_TRADER = "buy_from_trader";
    public const string SELL_TO_TRADER = "sell_to_trader";
    public const string CHANGE_WISHLIST_ITEM_CATEGORY = "ChangeWishlistItemCategory";
    public const string REMOVE_FROM_WISHLIST = "RemoveFromWishList";
    public const string ADD_TO_WISHLIST = "AddToWishList";
    public const string INSURE = "Insure";
    public const string RESTORE_HEALTH = "RestoreHealth";
    public const string HEAL = "Heal";
    public const string EAT = "Eat";
    public const string CUSTOMIZATION_SET = "CustomizationSet";
    public const string CUSTOMIZATION_BUY = "CustomizationBuy";
}
