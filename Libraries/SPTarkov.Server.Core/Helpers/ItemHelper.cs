using System.Collections.Frozen;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Inventory;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace SPTarkov.Server.Core.Helpers;

[Injectable]
public class ItemHelper(
    ISptLogger<ItemHelper> logger,
    RandomUtil randomUtil,
    DatabaseService databaseService,
    HandbookHelper handbookHelper,
    ItemBaseClassService itemBaseClassService,
    ItemFilterService itemFilterService,
    ServerLocalisationService serverLocalisationService,
    LocaleService localeService,
    ICloner cloner
)
{
    protected static readonly FrozenSet<MongoId> _defaultInvalidBaseTypes =
    [
        BaseClasses.LOOT_CONTAINER,
        BaseClasses.MOB_CONTAINER,
        BaseClasses.STASH,
        BaseClasses.SORTING_TABLE,
        BaseClasses.INVENTORY,
        BaseClasses.STATIONARY_CONTAINER,
        BaseClasses.POCKETS,
    ];

    protected static readonly FrozenSet<string> _slotsAsStrings =
    [
        nameof(EquipmentSlots.Headwear),
        nameof(EquipmentSlots.Earpiece),
        nameof(EquipmentSlots.FaceCover),
        nameof(EquipmentSlots.ArmorVest),
        nameof(EquipmentSlots.Eyewear),
        nameof(EquipmentSlots.ArmBand),
        nameof(EquipmentSlots.TacticalVest),
        nameof(EquipmentSlots.Pockets),
        nameof(EquipmentSlots.Backpack),
        nameof(EquipmentSlots.SecuredContainer),
        nameof(EquipmentSlots.FirstPrimaryWeapon),
        nameof(EquipmentSlots.SecondPrimaryWeapon),
        nameof(EquipmentSlots.Holster),
        nameof(EquipmentSlots.Scabbard),
    ];

    protected static readonly FrozenSet<MongoId> _dogTagTpls =
    [
        ItemTpl.BARTER_DOGTAG_BEAR,
        ItemTpl.BARTER_DOGTAG_BEAR_EOD,
        ItemTpl.BARTER_DOGTAG_BEAR_TUE,
        ItemTpl.BARTER_DOGTAG_USEC,
        ItemTpl.BARTER_DOGTAG_USEC_EOD,
        ItemTpl.BARTER_DOGTAG_USEC_TUE,
        ItemTpl.BARTER_DOGTAG_BEAR_PRESTIGE_1,
        ItemTpl.BARTER_DOGTAG_BEAR_PRESTIGE_2,
        ItemTpl.BARTER_DOGTAG_USEC_PRESTIGE_1,
        ItemTpl.BARTER_DOGTAG_USEC_PRESTIGE_2,
    ];

    protected static readonly FrozenSet<string> _softInsertIds =
    [
        "groin",
        "groin_back",
        "soft_armor_back",
        "soft_armor_front",
        "soft_armor_left",
        "soft_armor_right",
        "shoulder_l",
        "shoulder_r",
        "collar",
        "helmet_top",
        "helmet_back",
        "helmet_eyes",
        "helmet_jaw",
        "helmet_ears",
    ];

    protected static readonly FrozenSet<string> _removablePlateSlotIds =
    [
        "front_plate",
        "back_plate",
        "left_side_plate",
        "right_side_plate",
    ];

    protected static readonly FrozenSet<MongoId> _armorSlotsThatCanHoldMods =
    [
        BaseClasses.HEADWEAR,
        BaseClasses.VEST,
        BaseClasses.ARMOR,
    ];

    /// <summary>
    /// Does the provided pool of items contain the desired item
    /// </summary>
    /// <param name="itemPool">Item collection to check</param>
    /// <param name="itemTpl">Item to look for</param>
    /// <param name="slotId">OPTIONAL - slotId of desired item</param>
    /// <returns>True if pool contains item</returns>
    public bool HasItemWithTpl(IEnumerable<Item> itemPool, MongoId itemTpl, string slotId = "")
    {
        // Filter the pool by slotId if provided
        var filteredPool = string.IsNullOrEmpty(slotId)
            ? itemPool
            : itemPool.Where(itemInPool =>
                itemInPool.SlotId?.StartsWith(slotId, StringComparison.OrdinalIgnoreCase) ?? false
            );

        // Check if any item in the filtered pool matches the provided item
        return filteredPool.Any(poolItem => poolItem.Template == itemTpl);
    }

    /// <summary>
    /// Get the first item from provided pool with the desired tpl
    /// </summary>
    /// <param name="itemPool">Item collection to search</param>
    /// <param name="tpl">Item tpl to find</param>
    /// <param name="slotId">OPTIONAL - slotId of desired item</param>
    /// <returns>Item or null if no item found</returns>
    public Item GetItemFromPoolByTpl(IEnumerable<Item> itemPool, MongoId tpl, string slotId = "")
    {
        // Filter the pool by slotId if provided
        var filteredPool = string.IsNullOrEmpty(slotId)
            ? itemPool
            : itemPool.Where(item =>
                item.SlotId?.StartsWith(slotId, StringComparison.OrdinalIgnoreCase) ?? false
            );

        // Check if any item in the filtered pool matches the provided item
        return filteredPool.FirstOrDefault(poolItem => poolItem.Template == tpl);
    }

    /// <summary>
    /// This method will compare two items (with all its children) and see if they are equivalent
    /// This method will NOT compare IDs on the items
    /// </summary>
    /// <param name="item1">first item with all its children to compare</param>
    /// <param name="item2">second item with all its children to compare</param>
    /// <param name="compareUpdProperties">Upd properties to compare between the items</param>
    /// <returns>true if they are the same</returns>
    public bool IsSameItems(
        ICollection<Item> item1,
        ICollection<Item> item2,
        ISet<string>? compareUpdProperties = null
    )
    {
        if (item1.Count != item2.Count)
        {
            // Items have different mod counts
            return false;
        }

        foreach (var itemOf1 in item1)
        {
            var itemOf2 = item2.FirstOrDefault(i2 => i2.Template.Equals(itemOf1.Template));
            if (itemOf2 is null)
            {
                return false;
            }

            if (!itemOf1.IsSameItem(itemOf2, compareUpdProperties))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Helper method to generate an Upd based on a template
    /// </summary>
    /// <param name="itemTemplate">The item template to generate an Upd for</param>
    /// <returns>An Upd with all the default properties set</returns>
    public Upd GenerateUpdForItem(TemplateItem itemTemplate)
    {
        Upd itemProperties = new();

        // Armors, etc
        if (itemTemplate.Properties.MaxDurability is not null)
        {
            itemProperties.Repairable = new UpdRepairable
            {
                Durability = itemTemplate.Properties.MaxDurability,
                MaxDurability = itemTemplate.Properties.MaxDurability,
            };
        }

        if (itemTemplate.Properties.HasHinge ?? false)
        {
            itemProperties.Togglable = new UpdTogglable { On = true };
        }

        if (itemTemplate.Properties.Foldable ?? false)
        {
            itemProperties.Foldable = new UpdFoldable { Folded = false };
        }

        if (itemTemplate.Properties.WeapFireType?.Any() ?? false)
        {
            if (itemTemplate.Properties.WeapFireType.Contains("fullauto"))
            {
                itemProperties.FireMode = new UpdFireMode { FireMode = "fullauto" };
            }
            else
            {
                itemProperties.FireMode = new UpdFireMode
                {
                    FireMode = randomUtil.GetArrayValue(itemTemplate.Properties.WeapFireType),
                };
            }
        }

        if (itemTemplate.Properties.MaxHpResource is not null)
        {
            itemProperties.MedKit = new UpdMedKit
            {
                HpResource = itemTemplate.Properties.MaxHpResource,
            };
        }

        if (
            itemTemplate.Properties.MaxResource is not null
            && itemTemplate.Properties.FoodUseTime is not null
        )
        {
            itemProperties.FoodDrink = new UpdFoodDrink
            {
                HpPercent = itemTemplate.Properties.MaxResource,
            };
        }

        if (itemTemplate.Parent == BaseClasses.FLASHLIGHT)
        {
            itemProperties.Light = new UpdLight { IsActive = false, SelectedMode = 0 };
        }
        else if (itemTemplate.Parent == BaseClasses.TACTICAL_COMBO)
        {
            itemProperties.Light = new UpdLight { IsActive = false, SelectedMode = 0 };
        }

        if (itemTemplate.Parent == BaseClasses.NIGHTVISION)
        {
            itemProperties.Togglable = new UpdTogglable { On = false };
        }

        // Toggleable face shield
        if (
            (itemTemplate.Properties.HasHinge ?? false)
            && (itemTemplate.Properties.FaceShieldComponent ?? false)
        )
        {
            itemProperties.Togglable = new UpdTogglable { On = false };
        }

        return itemProperties;
    }

    /// <summary>
    /// Checks if a tpl is a valid item. Valid meaning that it's an item that can be stored in stash
    /// Valid means:
    /// Not quest item
    /// 'Item' type
    /// Not on the invalid base types array
    /// Price above 0 roubles
    /// </summary>
    /// <param name="tpl">Template id to check</param>
    /// <param name="invalidBaseTypes">OPTIONAL - Base types deemed invalid</param>
    /// <returns>true for items that may be in player possession and not quest items</returns>
    public bool IsValidItem(MongoId tpl, ISet<MongoId>? invalidBaseTypes = null)
    {
        var baseTypes = invalidBaseTypes ?? _defaultInvalidBaseTypes;
        var itemDetails = GetItem(tpl);

        return itemDetails.Key && IsValidItem(itemDetails.Value, baseTypes);
    }

    /// <summary>
    /// Checks if a tpl is a valid item. Valid meaning that it's an item that can be stored in stash
    /// Valid means:
    /// Not quest item
    /// 'Item' type
    /// Not on the invalid base types array
    /// Price above 0 roubles
    /// </summary>
    /// <param name="item">Item from DB to check</param>
    /// <param name="invalidBaseTypes">OPTIONAL - Base types deemed invalid</param>
    /// <returns>true for items that may be in player possession and not quest items</returns>
    public bool IsValidItem(TemplateItem item, ISet<MongoId>? invalidBaseTypes = null)
    {
        var baseTypes = invalidBaseTypes ?? _defaultInvalidBaseTypes;

        return !(item.Properties.QuestItem ?? false)
            && string.Equals(item.Type, "Item", StringComparison.OrdinalIgnoreCase)
            && GetItemPrice(item.Id) > 0
            && !itemFilterService.IsItemBlacklisted(item.Id)
            && baseTypes.All(x => !IsOfBaseclass(item.Id, x));
    }

    /// <summary>
    /// Check if the tpl / template id provided is a descendant of the baseclass
    /// </summary>
    /// <param name="tpl">Item template id to check</param>
    /// <param name="baseClassTpl">Baseclass to check for</param>
    /// <returns>is the tpl a descendant</returns>
    public bool IsOfBaseclass(MongoId tpl, MongoId baseClassTpl)
    {
        return itemBaseClassService.ItemHasBaseClass(tpl, [baseClassTpl]);
    }

    /// <summary>
    /// Check if item has any of the supplied base classes
    /// </summary>
    /// <param name="tpl">Item to check base classes of</param>
    /// <param name="baseClassTpls">Base classes to check for</param>
    /// <returns>True if any supplied base classes match</returns>
    public bool IsOfBaseclasses(MongoId tpl, ICollection<MongoId> baseClassTpls)
    {
        return itemBaseClassService.ItemHasBaseClass(tpl, baseClassTpls);
    }

    /// <summary>
    /// Does the provided item have the chance to require soft armor inserts
    /// Only applies to helmets/vest/armors
    /// Not all headgear needs them
    /// </summary>
    /// <param name="itemTpl">Tpl to check</param>
    /// <returns>Does item have the possibility ot need soft inserts</returns>
    public bool ArmorItemCanHoldMods(MongoId itemTpl)
    {
        return IsOfBaseclasses(itemTpl, _armorSlotsThatCanHoldMods);
    }

    /// <summary>
    /// Does the provided item tpl need soft/removable inserts to function
    /// </summary>
    /// <param name="itemTpl">Armor item</param>
    /// <returns>True if item needs some kind of insert</returns>
    public bool ArmorItemHasRemovableOrSoftInsertSlots(MongoId itemTpl)
    {
        if (!ArmorItemCanHoldMods(itemTpl))
        {
            return false;
        }

        return ArmorItemHasRemovablePlateSlots(itemTpl) || ItemRequiresSoftInserts(itemTpl);
    }

    /// <summary>
    /// Does the provided tpl have ability to hold removable plate items
    /// </summary>
    /// <param name="itemTpl">Item tpl to check for plate support</param>
    /// <returns>True when armor can hold plates</returns>
    public bool ArmorItemHasRemovablePlateSlots(MongoId itemTpl)
    {
        var itemTemplate = GetItem(itemTpl);

        return itemTemplate.Value.Properties.Slots.Any(slot =>
            _removablePlateSlotIds.Contains(slot.Name.ToLowerInvariant())
        );
    }

    /// <summary>
    /// Does the provided item tpl require soft inserts to become a valid armor item
    /// </summary>
    /// <param name="itemTpl">Item tpl to check</param>
    /// <returns>True if it needs armor inserts</returns>
    public bool ItemRequiresSoftInserts(MongoId itemTpl)
    {
        // Not a slot that takes soft-inserts
        if (!ArmorItemCanHoldMods(itemTpl))
        {
            return false;
        }

        // Check is an item
        var itemDbDetails = GetItem(itemTpl);
        if (!itemDbDetails.Key)
        {
            return false;
        }

        // Has no slots
        if (!(itemDbDetails.Value.Properties.Slots ?? []).Any())
        {
            return false;
        }

        // Check if item has slots that match soft insert name ids
        if (
            itemDbDetails.Value.Properties.Slots.Any(slot =>
                IsSoftInsertId(slot.Name.ToLowerInvariant())
            )
        )
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get all soft insert slot ids
    /// </summary>
    /// <returns>A List of soft insert ids (e.g. soft_armor_back, helmet_top)</returns>
    public static FrozenSet<string> GetSoftInsertSlotIds()
    {
        return _softInsertIds;
    }

    /// <summary>
    ///     Does the passed in slot id match a soft insert id
    /// </summary>
    /// <param name="slotId">slotId value to check</param>
    /// <returns></returns>
    public bool IsSoftInsertId(string slotId)
    {
        return _softInsertIds.Contains(slotId);
    }

    /// <summary>
    /// Returns the items total price based on the handbook or as a fallback from the prices.json if the item is not
    /// found in the handbook. If the price can't be found at all return 0
    /// </summary>
    /// <param name="tpls">item tpls to look up the price of</param>
    /// <returns>Total price in roubles</returns>
    public double GetItemAndChildrenPrice(IEnumerable<MongoId> tpls)
    {
        // Run getItemPrice for each tpl in tpls array, return sum
        return tpls.Aggregate(
            0,
            (total, tpl) => total + (int)GetItemPrice(tpl).GetValueOrDefault(0)
        );
    }

    /// <summary>
    ///     Returns the item price based on the handbook or as a fallback from the prices.json if the item is not
    ///     found in the handbook. If the price can't be found at all return 0
    /// </summary>
    /// <param name="tpl">Item to look price up of</param>
    /// <returns>Price in roubles</returns>
    public double? GetItemPrice(MongoId tpl)
    {
        var handbookPrice = GetStaticItemPrice(tpl);
        if (handbookPrice >= 1)
        {
            return handbookPrice;
        }

        return GetDynamicItemPrice(tpl);
    }

    /// <summary>
    ///     Returns the item price based on the handbook or as a fallback from the prices.json if the item is not
    ///     found in the handbook. If the price can't be found at all return 0
    /// </summary>
    /// <param name="tpl">Item to look price up of</param>
    /// <returns>Price in roubles</returns>
    public double GetItemMaxPrice(MongoId tpl)
    {
        var staticPrice = GetStaticItemPrice(tpl);
        var dynamicPrice = GetDynamicItemPrice(tpl);

        return Math.Max(staticPrice, dynamicPrice ?? 0d);
    }

    /// <summary>
    ///     Get the static (handbook) price in roubles for an item by tpl
    /// </summary>
    /// <param name="tpl">Items tpl id to look up price</param>
    /// <returns>Price in roubles (0 if not found)</returns>
    public double GetStaticItemPrice(MongoId tpl)
    {
        var handbookPrice = handbookHelper.GetTemplatePrice(tpl);
        if (handbookPrice >= 1)
        {
            return handbookPrice;
        }

        return 0;
    }

    /// <summary>
    ///     Get the dynamic (flea) price in roubles for an item by tpl
    /// </summary>
    /// <param name="tpl">Items tpl id to look up price</param>
    /// <returns>Price in roubles (undefined if not found)</returns>
    public double? GetDynamicItemPrice(MongoId tpl)
    {
        if (databaseService.GetPrices().TryGetValue(tpl, out var price))
        {
            return price;
        }

        return null;
    }

    /// <summary>
    ///     Get cloned copy of all item data from items.json
    /// </summary>
    /// <returns>List of TemplateItem objects</returns>
    public List<TemplateItem> GetItemsClone()
    {
        return cloner.Clone(databaseService.GetItems().Values.ToList());
    }

    /// <summary>
    /// Gets item data from items.json
    /// </summary>
    /// <param name="itemTpl">template id to look up</param>
    /// <returns>KvP, key = bool, value = template item object</returns>
    public KeyValuePair<bool, TemplateItem?> GetItem(MongoId itemTpl)
    {
        // -> Gets item from <input: _tpl>
        if (databaseService.GetItems().TryGetValue(itemTpl, out var item))
        {
            return new KeyValuePair<bool, TemplateItem?>(true, item);
        }

        return new KeyValuePair<bool, TemplateItem?>(false, null);
    }

    /// <summary>
    /// Checks if the item has slots
    /// </summary>
    /// <param name="itemTpl">Template id of the item to check</param>
    /// <returns>True if the item has slots</returns>
    public bool ItemHasSlots(MongoId itemTpl)
    {
        if (databaseService.GetItems().TryGetValue(itemTpl, out var item))
        {
            return item?.Properties?.Slots?.Count > 0;
        }

        return false;
    }

    /// <summary>
    /// Checks if the item is in the database
    /// </summary>
    /// <param name="itemTpl">Id of the item to check</param>
    /// <returns>true if the item is in the database</returns>
    public bool IsItemInDb(MongoId itemTpl)
    {
        return databaseService.GetItems().ContainsKey(itemTpl);
    }

    /// <summary>
    /// Calculate the average quality of an item and its children
    /// </summary>
    /// <param name="itemWithChildren">An offers item to process</param>
    /// <param name="skipArmorItemsWithoutDurability">Skip over armor items without durability</param>
    /// <returns>% quality modifier between 0 and 1</returns>
    public double GetItemQualityModifierForItems(
        IEnumerable<Item> itemWithChildren,
        bool skipArmorItemsWithoutDurability = false
    )
    {
        if (IsOfBaseclass(itemWithChildren.First().Template, BaseClasses.WEAPON))
        {
            return Math.Round(GetItemQualityModifier(itemWithChildren.First()), 5);
        }

        var qualityModifier = 0D;
        var itemsWithQualityCount = 0D;
        foreach (var item in itemWithChildren)
        {
            var result = GetItemQualityModifier(item, skipArmorItemsWithoutDurability);
            if (Math.Abs(result - (-1)) < 0.001)
            {
                // Is/near zero - Skip
                continue;
            }

            qualityModifier += result;
            itemsWithQualityCount++;
        }

        if (itemsWithQualityCount == 0)
        // Can happen when rigs without soft inserts or plates are listed
        {
            return 1;
        }

        return Math.Min(Math.Round(qualityModifier / itemsWithQualityCount, 5), 1);
    }

    /// <summary>
    /// Get normalized value (0-1) based on item condition
    /// Will return -1 for base armor items with 0 durability
    /// </summary>
    /// <param name="item">Item to check</param>
    /// <param name="skipArmorItemsWithoutDurability">return -1 for armor items that have max durability of 0</param>
    /// <returns>Number between 0 and 1</returns>
    public double GetItemQualityModifier(Item item, bool skipArmorItemsWithoutDurability = false)
    {
        // Default to 100%
        var result = 1d;

        // Is armor and has 0 max durability
        var itemDetails = GetItem(item.Template).Value;
        if (itemDetails?.Properties is null)
        {
            logger.Warning(
                $"Item: {item.Template} lacks properties, cannot ascertain quality level, assuming 100%"
            );

            return 1;
        }

        if (
            skipArmorItemsWithoutDurability
            && IsOfBaseclass(item.Template, BaseClasses.ARMOR)
            && itemDetails?.Properties?.MaxDurability == 0
        )
        {
            return -1;
        }

        if (item.Upd is not null)
        {
            if (item.Upd.MedKit is not null)
            {
                // Meds
                result =
                    (item.Upd.MedKit.HpResource ?? 0) / (itemDetails.Properties.MaxHpResource ?? 0);
            }
            else if (item.Upd.Repairable is not null)
            {
                result = GetRepairableItemQualityValue(itemDetails, item.Upd.Repairable, item);
            }
            else if (item.Upd.FoodDrink is not null)
            {
                result =
                    (item.Upd.FoodDrink.HpPercent ?? 0) / (itemDetails.Properties.MaxResource ?? 0);
            }
            else if (
                item.Upd.Key?.NumberOfUsages > 0
                && itemDetails.Properties.MaximumNumberOfUsage > 0
            )
            {
                // keys - keys count upwards, not down like everything else
                var maxNumOfUsages = itemDetails.Properties.MaximumNumberOfUsage;
                result =
                    (maxNumOfUsages ?? 0 - item.Upd.Key.NumberOfUsages ?? 0) / maxNumOfUsages ?? 0;
            }
            else if (item.Upd.Resource?.UnitsConsumed > 0)
            {
                // E.g. fuel tank
                result = (item.Upd.Resource.Value ?? 0) / (itemDetails.Properties.MaxResource ?? 0);
            }
            else if (item.Upd.RepairKit is not null)
            {
                result =
                    (item.Upd.RepairKit.Resource ?? 0)
                    / (itemDetails.Properties.MaxRepairResource ?? 0);
            }

            if (result == 0)
            // make item non-zero but still very low
            {
                result = 0.01;
            }
        }

        return result;
    }

    /// <summary>
    /// Get a quality value based on a repairable item's current state between current and max durability
    /// </summary>
    /// <param name="itemDetails">Db details for item we want quality value for</param>
    /// <param name="repairable">Repairable properties</param>
    /// <param name="item">Item quality value is for</param>
    /// <returns>number between 0 and 1</returns>
    protected double GetRepairableItemQualityValue(
        TemplateItem itemDetails,
        UpdRepairable repairable,
        Item item
    )
    {
        // Edge case, durability above max
        if (repairable.Durability > repairable.MaxDurability)
        {
            logger.Debug(
                $"Max durability: {repairable.MaxDurability} for item id: {item.Id} was below durability: {repairable.Durability}, adjusting values to match"
            );
            repairable.MaxDurability = repairable.Durability;
        }

        // Attempt to get the max durability from _props. If not available, use Repairable max durability value instead.
        var maxPossibleDurability =
            itemDetails.Properties?.MaxDurability ?? repairable.MaxDurability;
        var durability = repairable.Durability / maxPossibleDurability;

        if (durability == 0)
        {
            logger.Error(
                serverLocalisationService.GetText(
                    "item-durability_value_invalid_use_default",
                    item.Template
                )
            );

            return 1;
        }

        return Math.Sqrt(durability ?? 0);
    }

    /// <summary>
    /// Find children of the item in a given assort (weapons parts for example, need recursive loop function)
    /// </summary>
    /// <param name="itemIdToFind">Template id of item to check for</param>
    /// <param name="assort">List of items to check in</param>
    /// <returns>List of children of requested item</returns>
    public List<Item> FindAndReturnChildrenByAssort(MongoId itemIdToFind, List<Item> assort)
    {
        List<Item> list = [];
        var itemIdToFindString = itemIdToFind.ToString();
        foreach (var itemFromAssort in assort)
        {
            // Parent matches desired item + all items in list do not match
            if (
                itemFromAssort.ParentId == itemIdToFindString
                && list.All(item => itemFromAssort.Id != item.Id)
            )
            {
                list.Add(itemFromAssort);
                list = list.Concat(FindAndReturnChildrenByAssort(itemFromAssort.Id, assort))
                    .ToList();
            }
        }

        return list;
    }

    /// <summary>
    ///     Checks if the passed template id is a dog tag.
    /// </summary>
    /// <param name="tpl">Template id to check.</param>
    /// <returns>True if it is a dogtag.</returns>
    public bool IsDogtag(MongoId tpl)
    {
        return _dogTagTpls.Contains(tpl);
    }

    /// <summary>
    ///     Checks if the passed item can be stacked.
    /// </summary>
    /// <param name="tpl">Item to check.</param>
    /// <returns>True if it can be stacked.</returns>
    public bool? IsItemTplStackable(MongoId tpl)
    {
        if (!databaseService.GetItems().TryGetValue(tpl, out var item))
        {
            return null;
        }

        return item.Properties.StackMaxSize > 1;
    }

    /// <summary>
    ///     Splits the item stack if it exceeds its items StackMaxSize property into child items of the passed parent.
    /// </summary>
    /// <param name="itemToSplit">Item to split into smaller stacks.</param>
    /// <returns>List of root item + children.</returns>
    public List<Item> SplitStack(Item itemToSplit)
    {
        if (itemToSplit?.Upd?.StackObjectsCount is null)
        {
            return [itemToSplit];
        }

        var maxStackSize = GetItem(itemToSplit.Template).Value.Properties.StackMaxSize;
        var remainingCount = itemToSplit.Upd.StackObjectsCount;
        List<Item> rootAndChildren = [];

        // If the current count is already equal or less than the max
        // return the item as is.
        if (remainingCount <= maxStackSize)
        {
            rootAndChildren.Add(cloner.Clone(itemToSplit));

            return rootAndChildren;
        }

        while (remainingCount.Value != 0)
        {
            var amount = Math.Min(remainingCount ?? 0, maxStackSize ?? 0);
            var newStackClone = cloner.Clone(itemToSplit);

            newStackClone.Id = new MongoId();
            newStackClone.Upd.StackObjectsCount = amount;
            remainingCount -= amount;
            rootAndChildren.Add(newStackClone);
        }

        return rootAndChildren;
    }

    /// <summary>
    ///     Splits the item stack if it exceeds its items StackMaxSize property into child items of the passed parent.
    ///     TODO: untested
    /// </summary>
    /// <param name="itemWithChildren">Item (with children) to split into smaller stacks.</param>
    /// <returns>List of root item + children.</returns>
    public List<List<Item>> SplitStack(List<Item> itemWithChildren)
    {
        var originRootItem = itemWithChildren.FirstOrDefault();
        if (originRootItem?.Upd?.StackObjectsCount is null)
        {
            return [itemWithChildren];
        }

        var maxStackSize = GetItem(originRootItem.Template).Value.Properties.StackMaxSize;
        var remainingCount = originRootItem.Upd.StackObjectsCount;
        List<List<Item>> result = [];

        // If the current count is already equal or less than the max
        // return the item as is.
        if (remainingCount <= maxStackSize)
        {
            result.Add(itemWithChildren);

            return result;
        }

        while (remainingCount.Value != 0)
        {
            // Clone item and make IDs unique
            var itemWithChildrenClone = cloner.Clone(itemWithChildren).ReplaceIDs().ToList();

            // Set stack count to new value
            var amount = Math.Min(remainingCount ?? 0, maxStackSize ?? 0);
            itemWithChildrenClone[0].Upd.StackObjectsCount = amount;
            remainingCount -= amount;
            result.Add(itemWithChildrenClone);
        }

        return result;
    }

    /// <summary>
    ///     Turns items like money into separate stacks that adhere to max stack size.
    /// </summary>
    /// <param name="itemToSplit">Item to split into smaller stacks.</param>
    /// <returns>List of separate item stacks.</returns>
    public List<List<Item>> SplitStackIntoSeparateItems(Item itemToSplit)
    {
        var itemTemplate = GetItem(itemToSplit.Template).Value;
        var itemMaxStackSize = itemTemplate.Properties.StackMaxSize ?? 1;

        // item already within bounds of stack size, return it
        if (itemToSplit.Upd?.StackObjectsCount <= itemMaxStackSize)
        {
            return
            [
                [itemToSplit],
            ];
        }

        // Split items stack into chunks
        List<List<Item>> result = [];
        var remainingCount = itemToSplit.Upd.StackObjectsCount;
        while (remainingCount != 0)
        {
            var amount = Math.Min(remainingCount ?? 0, itemMaxStackSize);
            var newItemClone = cloner.Clone(itemToSplit);

            newItemClone.Id = new MongoId();
            newItemClone.Upd.StackObjectsCount = amount;
            remainingCount -= amount;
            result.Add([newItemClone]);
        }

        return result;
    }

    /// <summary>
    ///     Finds Barter items from a list of items.
    /// </summary>
    /// <param name="by">Tpl or id.</param>
    /// <param name="itemsToSearch">Array of items to iterate over.</param>
    /// <param name="desiredBarterItemIds">List of desired barter item ids.</param>
    /// <returns>List of Item objects.</returns>
    public List<Item> FindBarterItems(
        string by,
        List<Item> itemsToSearch,
        List<MongoId> desiredBarterItemIds
    )
    {
        // Find required items to take after buying (handles multiple items)
        List<Item> matchingItems = [];
        foreach (var barterId in desiredBarterItemIds)
        {
            var filteredResult = itemsToSearch.Where(item =>
                by == "tpl" ? item.Template.Equals(barterId) : item.Id.Equals(barterId)
            );

            if (!filteredResult.Any())
            {
                logger.Warning(
                    serverLocalisationService.GetText("item-helper_no_items_for_barter", barterId)
                );
                continue;
            }

            matchingItems.AddRange(filteredResult);
        }

        return matchingItems;
    }

    /// <summary>
    ///     Replaces the _id value for the base item + all children that are children of it.
    ///     REPARENTS ROOT ITEM ID, NOTHING ELSE.
    /// </summary>
    /// <param name="itemWithChildren">Item with mods to update.</param>
    /// <param name="newId">New id to add on children of base item.</param>
    public void ReplaceRootItemID(List<Item> itemWithChildren, string newId = "")
    {
        // original id on base item
        var oldId = itemWithChildren[0].Id;

        // Update base item to use new id
        itemWithChildren[0].Id = newId;

        // Update all parentIds of items attached to base item to use new id
        foreach (var item in itemWithChildren)
        {
            if (item.ParentId == oldId)
            {
                item.ParentId = newId;
            }
        }
    }

    /// <summary>
    /// TODO - Write
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="insuredItems"></param>
    public void ReplaceProfileInventoryIds(
        BotBaseInventory inventory,
        List<InsuredItem>? insuredItems = null
    )
    {
        // Blacklist
        var itemIdBlacklist = new HashSet<MongoId>();
        itemIdBlacklist.UnionWith(
            new List<MongoId>
            {
                inventory.Equipment.Value,
                inventory.QuestRaidItems.Value,
                inventory.QuestStashItems.Value,
                inventory.SortingTable.Value,
                inventory.Stash.Value,
                inventory.HideoutCustomizationStashId.Value,
            }
        );
        itemIdBlacklist.UnionWith(inventory.HideoutAreaStashes.Values);

        // Add insured items ids to blacklist
        if (insuredItems is not null)
        {
            itemIdBlacklist.UnionWith(insuredItems.Select(x => x.ItemId.Value));
        }

        foreach (var item in inventory.Items)
        {
            if (itemIdBlacklist.Contains(item.Id))
            {
                continue;
            }

            // Generate new id
            var newId = new MongoId();

            // Keep copy of original id
            var originalId = item.Id;

            // Update items id to new one we generated
            item.Id = newId;

            // Find all children of item and update their parent ids to match
            var childItems = inventory.Items.Where(x => x.ParentId == originalId);
            foreach (var childItem in childItems)
            {
                childItem.ParentId = newId;
            }

            // Also replace in quick slot if the old ID exists.
            if (inventory.FastPanel is null)
            {
                continue;
            }

            // Update quickslot id
            if (inventory.FastPanel.ContainsKey(originalId))
            {
                inventory.FastPanel[originalId] = newId;
            }
        }
    }

    /// <summary>
    ///     Regenerate all GUIDs with new IDs, except special item types (e.g. quest, sorting table, etc.) This
    ///     function will not mutate the original items list, but will return a new list with new GUIDs.
    /// </summary>
    /// <param name="originalItems">Items to adjust the IDs of</param>
    /// <param name="pmcData">Player profile</param>
    /// <param name="insuredItems">Insured items that should not have their IDs replaced</param>
    /// <param name="fastPanel">Quick slot panel</param>
    /// <returns>Items</returns>
    public List<Item> ReplaceIDs(
        List<Item> originalItems,
        PmcData? pmcData,
        List<InsuredItem>? insuredItems = null,
        Dictionary<string, string>? fastPanel = null
    )
    {
        // Blacklist
        var itemIdBlacklist = new HashSet<MongoId>();

        if (pmcData != null)
        {
            itemIdBlacklist.UnionWith(
                new List<MongoId>
                {
                    pmcData.Inventory.Equipment.Value,
                    pmcData.Inventory.QuestRaidItems.Value,
                    pmcData.Inventory.QuestStashItems.Value,
                    pmcData.Inventory.SortingTable.Value,
                    pmcData.Inventory.Stash.Value,
                    pmcData.Inventory.HideoutCustomizationStashId.Value,
                }
            );
            itemIdBlacklist.UnionWith(pmcData.Inventory.HideoutAreaStashes.Values);
        }

        // Add insured items ids to blacklist
        if (insuredItems is not null)
        {
            itemIdBlacklist.UnionWith(insuredItems.Select(x => x.ItemId.Value));
        }

        foreach (var item in originalItems)
        {
            if (itemIdBlacklist.Contains(item.Id))
            {
                continue;
            }

            // Generate new id
            var newId = new MongoId();

            // Keep copy of original id
            var originalId = item.Id;

            // Update items id to new one we generated
            item.Id = newId;

            // Find all children of item and update their parent ids to match
            var childItems = originalItems.Where(x => x.ParentId == originalId);
            foreach (var childItem in childItems)
            {
                childItem.ParentId = newId;
            }

            // Also replace in quick slot if the old ID exists.
            if (pmcData.Inventory.FastPanel is null)
            {
                continue;
            }

            // Update quickslot id
            if (pmcData.Inventory.FastPanel.ContainsKey(originalId))
            {
                pmcData.Inventory.FastPanel[originalId] = newId;
            }
        }

        return originalItems;
    }

    /// <summary>
    ///     Mark the passed in list of items as found in raid.
    ///     Modifies passed in items
    ///     Will not flag ammo or currency as FiR
    /// </summary>
    /// <param name="items">The list of items to mark as FiR</param>
    public void SetFoundInRaid(List<Item> items)
    {
        foreach (var item in items)
        {
            if (IsOfBaseclasses(item.Template, [BaseClasses.MONEY, BaseClasses.AMMO]))
            {
                if (item.Upd is not null)
                {
                    item.Upd.SpawnedInSession = null;
                }

                continue;
            }

            item.Upd ??= new Upd();
            item.Upd.SpawnedInSession = true;
        }
    }

    /// <summary>
    ///     Mark the passed in list of items as found in raid.
    ///     Modifies passed in items
    /// </summary>
    /// <param name="item">The list of items to mark as FiR</param>
    /// <param name="excludeCurrency">Skip adding FiR status to currency items</param>
    public void SetFoundInRaid(Item item, bool excludeCurrency = true)
    {
        if (excludeCurrency && IsOfBaseclass(item.Template, BaseClasses.MONEY))
        {
            return;
        }

        item.Upd ??= new Upd();
        item.Upd.SpawnedInSession = true;
    }

    /// <summary>
    ///     WARNING, SLOW. Recursively loop down through an items hierarchy to see if any of the ids match the supplied list, return true if any do
    /// </summary>
    /// <param name="tpl">Items tpl to check parents of</param>
    /// <param name="tplsToCheck">Tpl values to check if parents of item match</param>
    /// <returns>bool Match found</returns>
    public bool DoesItemOrParentsIdMatch(MongoId tpl, List<MongoId> tplsToCheck)
    {
        var (itemExists, item) = GetItem(tpl);

        // not an item, drop out
        if (!itemExists || item == null)
        {
            return false;
        }

        // no parent to check
        if (item.Parent == null)
        {
            return false;
        }

        // Does templateId match any values in tplsToCheck array
        if (tplsToCheck.Contains(item.Id))
        {
            return true;
        }

        // check items parent with same method
        if (tplsToCheck.Contains(item.Parent))
        {
            return true;
        }

        return DoesItemOrParentsIdMatch(item.Parent, tplsToCheck);
    }

    /// <summary>
    ///     Checks to see if the item is *actually* moddable in-raid. Checks include the items existence in the database, the
    ///     parent items existence in the database, the existence (and value) of the items `RaidModdable` property, and that
    ///     the parents slot-required property exists, matches that of the item, and its value.
    /// </summary>
    /// <param name="item">The item to be checked</param>
    /// <param name="parent">The parent of the item to be checked</param>
    /// <returns>True if the item is actually moddable, false if it is not, and null if the check cannot be performed.</returns>
    public bool? IsRaidModdable(Item item, Item parent)
    {
        // This check requires the item to have the slotId property populated.
        if (item.SlotId == null)
        {
            return null;
        }

        var itemTemplate = GetItem(item.Template);
        var parentTemplate = GetItem(parent.Template);

        // Check for RaidModdable property on the item template.
        var isNotRaidModdable = false;
        if (itemTemplate.Key)
        {
            isNotRaidModdable = itemTemplate.Value?.Properties?.RaidModdable == false;
        }

        // Check to see if the slot that the item is attached to is marked as required in the parent item's template.
        var isRequiredSlot = false;
        if (parentTemplate.Key && parentTemplate.Value?.Properties?.Slots != null)
        {
            isRequiredSlot =
                parentTemplate.Value?.Properties?.Slots?.Any(slot =>
                    slot?.Name == item?.SlotId && (slot?.Required ?? false)
                ) ?? false;
        }

        return itemTemplate.Key && parentTemplate.Key && !(isNotRaidModdable || isRequiredSlot);
    }

    /// <summary>
    ///     Retrieves the main parent item for a given attachment item.
    ///     This method traverses up the hierarchy of items starting from a given `itemId`, until it finds the main parent
    ///     item that is not an attached attachment itself. In other words, if you pass it an item id of a suppressor, it
    ///     will traverse up the muzzle brake, barrel, upper receiver, and return the gun that the suppressor is ultimately
    ///     attached to, even if that gun is located within multiple containers.
    ///     It's important to note that traversal is expensive, so this method requires that you pass it a Map of the items
    ///     to traverse, where the keys are the item IDs and the values are the corresponding Item objects. This alleviates
    ///     some of the performance concerns, as it allows for quick lookups of items by ID.
    /// </summary>
    /// <param name="itemId">The unique identifier of the item for which to find the main parent.</param>
    /// <param name="itemsMap">A Dictionary containing item IDs mapped to their corresponding Item objects for quick lookup.</param>
    /// <returns>The Item object representing the top-most parent of the given item, or null if no such parent exists.</returns>
    public Item? GetAttachmentMainParent(MongoId itemId, Dictionary<MongoId, Item> itemsMap)
    {
        var currentItem = itemsMap.FirstOrDefault(x => x.Key == itemId).Value;

        while (currentItem != null && IsAttachmentAttached(currentItem))
        {
            currentItem = itemsMap.FirstOrDefault(x => x.Key == currentItem.ParentId).Value;
            if (currentItem == null)
            {
                return null;
            }
        }

        return currentItem;
    }

    /// <summary>
    /// Determines if an item is an attachment that is currently attached to its parent item
    /// </summary>
    /// <param name="item">The item to check</param>
    /// <returns>true if the item is attached attachment, otherwise false</returns>
    public bool IsAttachmentAttached(Item item)
    {
        HashSet<string> check = ["hideout", "main"];

        return !(
            check.Contains(item.SlotId) // Is root item
            || _slotsAsStrings.Contains(item.SlotId) // Is root item in equipment slot e.g. `Headwear`
            || int.TryParse(item.SlotId, out _)
        ); // Has int as slotId, is inside container. e.g. cartridges
    }

    /// <summary>
    /// Retrieves the equipment parent item for a given item.
    ///
    /// This method traverses up the hierarchy of items starting from a given `itemId`, until it finds the equipment
    /// parent item. In other words, if you pass it an item id of a suppressor, it will traverse up the muzzle brake,
    /// barrel, upper receiver, gun, nested backpack, and finally return the backpack Item that is equipped.
    ///
    /// It's important to note that traversal is expensive, so this method requires that you pass it a Dictionary of the items
    /// to traverse, where the keys are the item IDs and the values are the corresponding Item objects. This alleviates
    /// some of the performance concerns, as it allows for quick lookups of items by ID.
    /// </summary>
    /// <param name="itemId">The unique identifier of the item for which to find the equipment parent.</param>
    /// <param name="itemsMap">A Dictionary containing item IDs mapped to their corresponding Item objects for quick lookup.</param>
    /// <returns>The Item object representing the equipment parent of the given item, or `null` if no such parent exists</returns>
    public Item? GetEquipmentParent(MongoId itemId, Dictionary<MongoId, Item> itemsMap)
    {
        var currentItem = itemsMap.GetValueOrDefault(itemId);

        while (currentItem is not null && !_slotsAsStrings.Contains(currentItem.SlotId))
        {
            currentItem = itemsMap.GetValueOrDefault(currentItem.ParentId);
            if (currentItem is null)
            {
                return null;
            }
        }

        return currentItem;
    }

    /// <summary>
    /// Get the inventory size of an item
    /// </summary>
    /// <param name="items">Item with children</param>
    /// <param name="rootItemId">The base items root id</param>
    /// <returns>ItemSize object (width and height)</returns>
    public ItemSize GetItemSize(ICollection<Item> items, MongoId rootItemId)
    {
        var rootTemplate = GetItem(items.FirstOrDefault(x => x.Id == rootItemId).Template).Value;
        var width = rootTemplate.Properties.Width;
        var height = rootTemplate.Properties.Height;

        var sizeUp = 0;
        var sizeDown = 0;
        var sizeLeft = 0;
        var sizeRight = 0;

        var forcedUp = 0;
        var forcedDown = 0;
        var forcedLeft = 0;
        var forcedRight = 0;

        var children = items.GetItemWithChildren(rootItemId);
        foreach (var child in children)
        {
            var itemTemplate = GetItem(child.Template).Value;

            // Calculating child ExtraSize
            if (itemTemplate.Properties.ExtraSizeForceAdd ?? false)
            {
                forcedUp += itemTemplate.Properties.ExtraSizeUp.Value;
                forcedDown += itemTemplate.Properties.ExtraSizeDown.Value;
                forcedLeft += itemTemplate.Properties.ExtraSizeLeft.Value;
                forcedRight += itemTemplate.Properties.ExtraSizeRight.Value;
            }
            else
            {
                sizeUp =
                    sizeUp < itemTemplate.Properties.ExtraSizeUp
                        ? itemTemplate.Properties.ExtraSizeUp.Value
                        : sizeUp;
                sizeDown =
                    sizeDown < itemTemplate.Properties.ExtraSizeDown
                        ? itemTemplate.Properties.ExtraSizeDown.Value
                        : sizeDown;
                sizeLeft =
                    sizeLeft < itemTemplate.Properties.ExtraSizeLeft
                        ? itemTemplate.Properties.ExtraSizeLeft.Value
                        : sizeLeft;
                sizeRight =
                    sizeRight < itemTemplate.Properties.ExtraSizeRight
                        ? itemTemplate.Properties.ExtraSizeRight.Value
                        : sizeRight;
            }
        }

        return new ItemSize
        {
            Width = (width ?? 0) + sizeLeft + sizeRight + forcedLeft + forcedRight,
            Height = (height ?? 0) + sizeUp + sizeDown + forcedUp + forcedDown,
        };
    }

    /// <summary>
    /// Get a random cartridge from an items Filter property
    /// </summary>
    /// <param name="item">Db item template to look up Cartridge filter values from</param>
    /// <returns>Valid caliber for cartridge</returns>
    public MongoId? GetRandomCompatibleCaliberTemplateId(TemplateItem item)
    {
        var cartridges = item
            ?.Properties?.Cartridges?.FirstOrDefault()
            ?.Props?.Filters?.FirstOrDefault()
            ?.Filter;
        if (cartridges is null)
        {
            logger.Warning($"Failed to find cartridge for item: {item?.Id} {item?.Name}");
            return null;
        }

        return randomUtil.GetArrayValue(cartridges);
    }

    /// <summary>
    ///     Add cartridges to the ammo box with correct max stack sizes
    /// </summary>
    /// <param name="ammoBox">Box to add cartridges to</param>
    /// <param name="ammoBoxDetails">Item template from items db</param>
    public void AddCartridgesToAmmoBox(List<Item> ammoBox, TemplateItem ammoBoxDetails)
    {
        var ammoBoxMaxCartridgeCount = ammoBoxDetails.Properties.StackSlots[0].MaxCount;
        var cartridgeTpl = ammoBoxDetails
            .Properties.StackSlots[0]
            .Props.Filters[0]
            .Filter.FirstOrDefault();
        var cartridgeDetails = GetItem(cartridgeTpl);
        var cartridgeMaxStackSize = cartridgeDetails.Value.Properties.StackMaxSize;

        // Exit early if ammo already exists in box
        if (ammoBox.Any(item => item.Template.Equals(cartridgeTpl)))
        {
            return;
        }

        // Add new stack-size-correct items to ammo box
        double? currentStoredCartridgeCount = 0;
        var maxPerStack = Math.Min(ammoBoxMaxCartridgeCount ?? 0, cartridgeMaxStackSize ?? 0);
        // Find location based on Max ammo box size
        var location = Math.Ceiling(ammoBoxMaxCartridgeCount / maxPerStack ?? 0) - 1;

        while (currentStoredCartridgeCount < ammoBoxMaxCartridgeCount)
        {
            var remainingSpace = ammoBoxMaxCartridgeCount - currentStoredCartridgeCount;
            var cartridgeCountToAdd = remainingSpace < maxPerStack ? remainingSpace : maxPerStack;

            // Add cartridge item into items array
            var cartridgeItemToAdd = CreateCartridges(
                ammoBox[0].Id,
                cartridgeTpl,
                (int)cartridgeCountToAdd,
                location
            );

            // In live no ammo box has the first cartridge item with a location
            if (location == 0)
            {
                cartridgeItemToAdd.Location = null;
            }

            ammoBox.Add(cartridgeItemToAdd);

            currentStoredCartridgeCount += cartridgeCountToAdd;
            location--;
        }
    }

    /// <summary>
    /// Add a single stack of cartridges to the ammo box
    /// </summary>
    /// <param name="ammoBox">Box item to add cartridges to</param>
    /// <param name="ammoBoxDetails">Item template from items db</param>
    public void AddSingleStackCartridgesToAmmoBox(List<Item> ammoBox, TemplateItem ammoBoxDetails)
    {
        var ammoBoxMaxCartridgeCount = ammoBoxDetails.Properties?.StackSlots?[0].MaxCount ?? 0;
        var cartridgeTpl = ammoBoxDetails.Properties?.StackSlots?[0].Props?.Filters?[
            0
        ].Filter?.FirstOrDefault();
        ammoBox.Add(
            CreateCartridges(ammoBox[0].Id, cartridgeTpl.Value, (int)ammoBoxMaxCartridgeCount, 0)
        );
    }

    /// <summary>
    /// Add child items (cartridges) to a magazine
    /// </summary>
    /// <param name="magazine">Magazine to add child items to</param>
    /// <param name="magTemplate">Db template of magazine</param>
    /// <param name="staticAmmoDist">Cartridge distribution</param>
    /// <param name="caliber">Caliber of cartridge to add to magazine</param>
    /// <param name="minSizePercent">OPTIONAL - % the magazine must be filled to</param>
    /// <param name="defaultCartridgeTpl">OPTIONAL -Cartridge to use when none found</param>
    /// <param name="weapon">OPTIONAL -Weapon the magazine will be used for (if passed in uses Chamber as whitelist)</param>
    public void FillMagazineWithRandomCartridge(
        List<Item> magazine,
        TemplateItem magTemplate,
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist,
        string? caliber = null,
        double minSizePercent = 0.25,
        MongoId? defaultCartridgeTpl = null,
        TemplateItem? weapon = null
    )
    {
        var chosenCaliber = caliber ?? GetRandomValidCaliber(magTemplate);

        // Edge case - Klin pp-9 has a typo in its ammo caliber
        if (chosenCaliber == "Caliber9x18PMM")
        {
            chosenCaliber = "Caliber9x18PM";
        }

        // Chose a randomly weighted cartridge that fits
        var cartridgeTpl = DrawAmmoTpl(
            chosenCaliber,
            staticAmmoDist,
            defaultCartridgeTpl,
            weapon?.Properties?.Chambers?.FirstOrDefault()?.Props?.Filters?.FirstOrDefault()?.Filter
                ?? null
        );
        if (cartridgeTpl is null)
        {
            if (logger.IsLogEnabled(LogLevel.Debug))
            {
                logger.Debug(
                    $"Unable to fill item: {magazine.FirstOrDefault().Id} {magTemplate.Name} with cartridges, none found."
                );
            }

            return;
        }

        FillMagazineWithCartridge(magazine, magTemplate, cartridgeTpl.Value, minSizePercent);
    }

    /// <summary>
    ///     Add child items to a magazine of a specific cartridge
    /// </summary>
    /// <param name="magazineWithChildCartridges">Magazine to add child items to</param>
    /// <param name="magTemplate">Db template of magazine</param>
    /// <param name="cartridgeTpl">Cartridge to add to magazine</param>
    /// <param name="minSizeMultiplier">% the magazine must be filled to</param>
    public void FillMagazineWithCartridge(
        List<Item> magazineWithChildCartridges,
        TemplateItem magTemplate,
        MongoId cartridgeTpl,
        double minSizeMultiplier = 0.25
    )
    {
        var isUBGL = IsOfBaseclass(magTemplate.Id, BaseClasses.UBGL);
        if (isUBGL)
        // UBGL don't have mags
        {
            return;
        }

        // Get cartridge properties and max allowed stack size
        var cartridgeDetails = GetItem(cartridgeTpl);
        if (!cartridgeDetails.Key)
        {
            logger.Error(serverLocalisationService.GetText("item-invalid_tpl_item", cartridgeTpl));
        }

        var cartridgeMaxStackSize = cartridgeDetails.Value?.Properties?.StackMaxSize;
        if (cartridgeMaxStackSize is null)
        {
            logger.Error($"Item with tpl: {cartridgeTpl} lacks a _props or StackMaxSize property");
        }

        // Get max number of cartridges in magazine, choose random value between min/max
        var magProps = magTemplate.Properties;
        var magazineCartridgeMaxCount = IsOfBaseclass(
            magTemplate.Id,
            BaseClasses.SPRING_DRIVEN_CYLINDER
        )
            ? magProps?.Slots?.Count // Edge case for rotating grenade launcher magazine
            : magProps?.Cartridges?.FirstOrDefault()?.MaxCount;

        if (magazineCartridgeMaxCount is null)
        {
            logger.Warning(
                $"Magazine: {magTemplate.Id} {magTemplate.Name} lacks a Cartridges array, unable to fill magazine with ammo"
            );

            return;
        }

        var desiredStackCount = randomUtil.GetInt(
            (int)Math.Round(minSizeMultiplier * magazineCartridgeMaxCount ?? 0),
            (int)magazineCartridgeMaxCount
        );

        if (magazineWithChildCartridges.Count > 1)
        {
            logger.Warning(
                $"Magazine {magTemplate.Name} already has cartridges defined,  this may cause issues"
            );
        }

        // Loop over cartridge count and add stacks to magazine
        var currentStoredCartridgeCount = 0;
        var location = 0;
        while (currentStoredCartridgeCount < desiredStackCount)
        {
            // Get stack size of cartridges
            var cartridgeCountToAdd =
                desiredStackCount <= cartridgeMaxStackSize
                    ? desiredStackCount
                    : cartridgeMaxStackSize;

            // Ensure we don't go over the max stackCount size
            var remainingSpace = desiredStackCount - currentStoredCartridgeCount;
            if (cartridgeCountToAdd > remainingSpace)
            {
                cartridgeCountToAdd = remainingSpace;
            }

            // Add cartridge item object into items array
            magazineWithChildCartridges.Add(
                CreateCartridges(
                    magazineWithChildCartridges[0].Id,
                    cartridgeTpl,
                    cartridgeCountToAdd ?? 0,
                    location
                )
            );

            currentStoredCartridgeCount += cartridgeCountToAdd.Value;
            location++;
        }

        // Only one cartridge stack added, remove location property as it's only used for 2 or more stacks
        if (location == 1)
        {
            magazineWithChildCartridges[1].Location = null;
        }
    }

    /// <summary>
    ///     Choose a random bullet type from the list of possible a magazine has
    /// </summary>
    /// <param name="magTemplate">Magazine template from Db</param>
    /// <returns>Tpl of cartridge</returns>
    protected string? GetRandomValidCaliber(TemplateItem magTemplate)
    {
        var ammoTpls = magTemplate.Properties.Cartridges[0].Props.Filters[0].Filter;
        var calibers = ammoTpls
            .Where(x => GetItem(x).Key)
            .Select(x => GetItem(x).Value.Properties.Caliber)
            .ToList();

        return randomUtil.DrawRandomFromList(calibers).FirstOrDefault();
    }

    /// <summary>
    ///     Chose a randomly weighted cartridge that fits
    /// </summary>
    /// <param name="caliber">Desired caliber</param>
    /// <param name="staticAmmoDist">Cartridges and their weights</param>
    /// <param name="fallbackCartridgeTpl">If a cartridge cannot be found in the above staticAmmoDist param, use this instead</param>
    /// <param name="cartridgeWhitelist">OPTIONAL whitelist for cartridges</param>
    /// <returns>Tpl of cartridge</returns>
    protected MongoId? DrawAmmoTpl(
        string caliber,
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist,
        MongoId? fallbackCartridgeTpl = null,
        ICollection<MongoId>? cartridgeWhitelist = null
    )
    {
        var ammos = staticAmmoDist.GetValueOrDefault(caliber, []);
        if (ammos.Count == 0 && fallbackCartridgeTpl is not null)
        {
            logger.Warning(
                $"Unable to pick a cartridge for caliber: {caliber}, staticAmmoDist has no data. using fallback value of {fallbackCartridgeTpl}"
            );

            return fallbackCartridgeTpl;
        }

        if (ammos.Count == 0 && fallbackCartridgeTpl is null)
        {
            logger.Warning(
                $"Unable to pick a cartridge for caliber: {caliber}, staticAmmoDist has no data. No fallback value provided"
            );

            return null;
        }

        var ammoArray = new ProbabilityObjectArray<MongoId, float?>(cloner);
        foreach (var icd in ammos)
        {
            // Whitelist exists and tpl not inside it, skip
            // Fixes 9x18mm kedr issues
            if (cartridgeWhitelist is not null && !cartridgeWhitelist.Contains(icd.Tpl.Value))
            {
                continue;
            }

            ammoArray.Add(
                new ProbabilityObject<MongoId, float?>(
                    icd.Tpl.Value,
                    (double)icd.RelativeProbability,
                    null
                )
            );
        }

        return ammoArray.Draw().FirstOrDefault();
    }

    /// <summary>
    ///     Create a basic cartridge object
    /// </summary>
    /// <param name="parentId">container cartridges will be placed in</param>
    /// <param name="ammoTpl">Cartridge to insert</param>
    /// <param name="stackCount">Count of cartridges inside parent</param>
    /// <param name="location">Location inside parent (e.g. 0, 1)</param>
    /// <returns>Item</returns>
    public Item CreateCartridges(MongoId parentId, MongoId ammoTpl, int stackCount, double location)
    {
        return new Item
        {
            Id = new MongoId(),
            Template = ammoTpl,
            ParentId = parentId,
            SlotId = "cartridges",
            Location = location,
            Upd = new Upd { StackObjectsCount = stackCount },
        };
    }

    /// <summary>
    ///     Get the name of an item from the locale file using the item tpl
    /// </summary>
    /// <param name="itemTpl">Tpl of item to get name of</param>
    /// <returns>Full name, short name if not found</returns>
    public string GetItemName(MongoId itemTpl)
    {
        var localeDb = localeService.GetLocaleDb();
        var result = localeDb[$"{itemTpl} Name"];
        if (result?.Length > 0)
        {
            return result;
        }

        return localeDb[$"{itemTpl} ShortName"];
    }

    /// <summary>
    ///     Get all item tpls with a desired base type
    /// </summary>
    /// <param name="desiredBaseType">Item base type wanted</param>
    /// <returns>Array of tpls</returns>
    public IEnumerable<MongoId> GetItemTplsOfBaseType(string desiredBaseType)
    {
        return databaseService
            .GetItems()
            .Values.Where(item => item.Parent == desiredBaseType)
            .Select(item => item.Id);
    }

    /// <summary>
    ///     Add child slot items to an item, chooses random child item if multiple choices exist
    /// </summary>
    /// <param name="itemToAdd">array with single object (root item)</param>
    /// <param name="itemToAddTemplate">Db template for root item</param>
    /// <param name="modSpawnChanceDict">Optional dictionary of mod name + % chance mod will be included in item (e.g. front_plate: 100)</param>
    /// <param name="requiredOnly">Only add required mods</param>
    /// <returns>Item with children</returns>
    public List<Item> AddChildSlotItems(
        List<Item> itemToAdd,
        TemplateItem itemToAddTemplate,
        Dictionary<string, double>? modSpawnChanceDict = null,
        bool requiredOnly = false
    )
    {
        var result = itemToAdd;
        HashSet<MongoId> incompatibleModTpls = [];
        foreach (var slot in itemToAddTemplate.Properties.Slots)
        {
            // If only required mods is requested, skip non-essential
            if (requiredOnly && !(slot.Required ?? false))
            {
                continue;
            }

            // Roll chance for non-required slot mods
            if (modSpawnChanceDict is not null && !(slot.Required ?? false))
            {
                // only roll chance to not include mod if dict exists and has value for this mod type (e.g. front_plate)
                if (modSpawnChanceDict.ContainsKey(slot.Name.ToLowerInvariant()))
                {
                    if (!randomUtil.GetChance100(modSpawnChanceDict[slot.Name.ToLowerInvariant()]))
                    {
                        continue;
                    }
                }
            }

            var itemPool = slot.Props.Filters.FirstOrDefault().Filter ?? [];
            if (itemPool.Count == 0)
            {
                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug(
                        $"Unable to choose a mod for slot: {slot.Name} on item: {itemToAddTemplate.Id} {itemToAddTemplate.Name}, parents' 'Filter' array is empty, skipping"
                    );
                }

                continue;
            }

            var chosenTpl = GetCompatibleTplFromArray(itemPool, incompatibleModTpls);
            if (chosenTpl is null)
            {
                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug(
                        $"Unable to choose a mod for slot: {slot.Name} on item: {itemToAddTemplate.Id} {itemToAddTemplate.Name}, no compatible tpl found in pool of {itemPool.Count}, skipping"
                    );
                }

                continue;
            }

            // Create basic item structure ready to add to weapon array
            Item modItemToAdd = new()
            {
                Id = new MongoId(),
                Template = chosenTpl.Value,
                ParentId = result[0].Id,
                SlotId = slot.Name,
            };

            // Add chosen item to weapon array
            result.Add(modItemToAdd);

            var modItemDbDetails = GetItem(modItemToAdd.Template).Value;

            // Include conflicting items of newly added mod in pool to be used for next mod choice
            incompatibleModTpls.UnionWith(modItemDbDetails.Properties.ConflictingItems);
        }

        return result;
    }

    /// <summary>
    ///     Get a compatible tpl from the array provided where it is not found in the provided incompatible mod tpls parameter
    /// </summary>
    /// <param name="possibleTpls">Tpls to randomly choose from</param>
    /// <param name="incompatibleModTpls">Incompatible tpls to not allow</param>
    /// <returns>Chosen tpl or undefined</returns>
    public MongoId? GetCompatibleTplFromArray(
        HashSet<MongoId> possibleTpls,
        HashSet<MongoId> incompatibleModTpls
    )
    {
        if (!possibleTpls.Any())
        {
            return null;
        }

        MongoId? chosenTpl = null;
        var count = 0;
        while (chosenTpl is null)
        {
            // Loop over choosing a random tpl until one is found or count variable reaches the same size as the possible tpls array
            var tpl = randomUtil.GetArrayValue(possibleTpls);
            if (incompatibleModTpls.Contains(tpl))
            {
                // Incompatible tpl was chosen, try again
                count++;
                if (count >= possibleTpls.Count)
                {
                    return null;
                }

                continue;
            }

            chosenTpl = tpl;
        }

        return chosenTpl;
    }

    /// <summary>
    ///     Is the provided item._props.Slots._name property a plate slot
    /// </summary>
    /// <param name="slotName">Name of slot (_name) of Items Slot array</param>
    /// <returns>True if it is a slot that holds a removable plate</returns>
    public bool IsRemovablePlateSlot(string slotName)
    {
        return GetRemovablePlateSlotIds().Contains(slotName.ToLowerInvariant());
    }

    /// <summary>
    /// Get a list of slot names that hold removable plates
    /// </summary>
    /// <returns>Array of slot ids (e.g. front_plate)</returns>
    public FrozenSet<string> GetRemovablePlateSlotIds()
    {
        return _removablePlateSlotIds;
    }

    /// <summary>
    /// Generate new unique ids for child items while preserving hierarchy
    /// </summary>
    /// <param name="rootItem">Base/primary item</param>
    /// <param name="itemWithChildren">Primary item + children of primary item</param>
    /// <returns>Item array with updated IDs</returns>
    public List<Item> ReparentItemAndChildren(Item rootItem, List<Item> itemWithChildren)
    {
        var oldRootId = itemWithChildren[0].Id;
        Dictionary<string, MongoId> idMappings = [];

        idMappings[oldRootId.ToString()] = rootItem.Id;

        foreach (var mod in itemWithChildren)
        {
            if (!idMappings.ContainsKey(mod.Id))
            {
                idMappings[mod.Id.ToString()] = new MongoId();
            }

            // Has parentId + no remapping exists for its parent
            if (
                mod.ParentId != null
                && (!idMappings.ContainsKey(mod.ParentId) || idMappings?[mod.ParentId] is null)
            )
            // Make remapping for items parentId
            {
                idMappings[mod.ParentId] = new MongoId();
            }

            mod.Id = idMappings[mod.Id.ToString()];
            if (mod.ParentId != null)
            {
                mod.ParentId = idMappings[mod.ParentId];
            }
        }

        // Force item's details into first location of presetItems
        if (itemWithChildren[0].Template != rootItem.Template)
        {
            logger.Warning(
                $"Reassigning root item from {itemWithChildren[0].Template} to {rootItem.Template}"
            );
        }

        itemWithChildren[0] = rootItem;

        return itemWithChildren;
    }

    // Add a blank upd object to passed in item if it does not exist already
    // item to add upd to
    // text to write to log when upd object was not found
    // Returns True when upd object was added
    public bool AddUpdObjectToItem(Item item, string? warningMessageWhenMissing = null)
    {
        if (item.Upd is not null)
        {
            // Already exists, exit early
            return false;
        }

        item.Upd = new Upd();

        if (warningMessageWhenMissing is not null)
        {
            if (logger.IsLogEnabled(LogLevel.Debug))
            {
                logger.Debug(warningMessageWhenMissing);
            }
        }

        return true;
    }

    // Return all tpls from Money enum
    // Returns string tpls
    public List<MongoId> GetMoneyTpls()
    {
        return [Money.ROUBLES, Money.DOLLARS, Money.EUROS, Money.GP];
    }

    // Get a randomised stack size for the passed in ammo
    // Ammo to get stack size for
    // Default: Limit to 60 to prevent crazy values when players use stack increase mods
    // Returns number
    public int GetRandomisedAmmoStackSize(TemplateItem ammoItemTemplate, int maxLimit = 60)
    {
        return ammoItemTemplate.Properties?.StackMaxSize == 1
            ? 1
            : randomUtil.GetInt(
                ammoItemTemplate.Properties?.StackMinRandom ?? 1,
                Math.Min(ammoItemTemplate.Properties?.StackMaxRandom ?? 1, maxLimit)
            );
    }

    public MongoId? GetItemBaseType(MongoId tpl, bool rootOnly = true)
    {
        var result = GetItem(tpl);
        if (!result.Key)
        // Not an item
        {
            return null;
        }

        var currentItem = result.Value;
        while (currentItem is not null)
        {
            if (currentItem.Type == "Node" && !rootOnly)
            // Hit first base type
            {
                return currentItem.Id;
            }

            if (currentItem.Parent.IsEmpty())
            // No parent, reached root
            {
                return currentItem.Id;
            }

            // Get parent item and start loop again
            currentItem = GetItem(tpl).Value;
        }

        return null;
    }

    /// <summary>
    ///     Get a 2D grid of a container's item slots
    /// </summary>
    /// <param name="containerTpl">Tpl id of the container</param>
    public int[,] GetContainerMapping(MongoId containerTpl)
    {
        // Get template from db
        var containerTemplate = GetItem(containerTpl).Value;

        // Get height/width
        var height = containerTemplate.Properties.Grids[0].Props.CellsV;
        var width = containerTemplate.Properties.Grids[0].Props.CellsH;

        return GetBlankContainerMap(width.Value, height.Value);
    }

    /// <summary>
    ///     Get a blank two-dimensional representation of a container
    /// </summary>
    /// <param name="horizontalSizeX">Width of container (columns)</param>
    /// <param name="verticalSizeY">Height of container (rows)</param>
    /// <returns>Two-dimensional representation of container</returns>
    public int[,] GetBlankContainerMap(int horizontalSizeX, int verticalSizeY)
    {
        // Rows / Columns
        return new int[verticalSizeY, horizontalSizeX];
    }
}
