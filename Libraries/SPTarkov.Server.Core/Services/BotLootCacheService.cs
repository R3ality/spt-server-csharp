using System.Collections.Concurrent;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace SPTarkov.Server.Core.Services;

[Injectable(InjectionType.Singleton)]
public class BotLootCacheService(
    ISptLogger<BotLootCacheService> _logger,
    ItemHelper _itemHelper,
    PMCLootGenerator _pmcLootGenerator,
    LocalisationService _localisationService,
    ICloner _cloner
)
{
    protected ConcurrentDictionary<string, BotLootCache> _lootCache = new();
    private readonly Lock _drugLock = new();
    private readonly Lock _foodLock = new();
    private readonly Lock _drinkLock = new();
    private readonly Lock _currencyLock = new();
    private readonly Lock _stimLock = new();
    private readonly Lock _grenadeLock = new();
    private readonly Lock _specialLock = new();
    private readonly Lock _healingLock = new();

    /// <summary>
    ///     Remove cached bot loot data
    /// </summary>
    public void ClearCache()
    {
        _lootCache.Clear();
    }

    /// <summary>
    ///     Get the fully created loot array, ordered by price low to high
    /// </summary>
    /// <param name="botRole">bot to get loot for</param>
    /// <param name="isPmc">is the bot a pmc</param>
    /// <param name="lootType">what type of loot is needed (backpack/pocket/stim/vest etc)</param>
    /// <param name="botJsonTemplate">Base json db file for the bot having its loot generated</param>
    /// <param name="itemPriceMinMax">OPTIONAL - item price min and max value filter</param>
    /// <remarks>THIS IS NOT A THREAD SAFE METHOD</remarks>
    /// <returns>dictionary</returns>
    public Dictionary<string, double> GetLootFromCache(
        string botRole,
        bool isPmc,
        string lootType,
        BotType botJsonTemplate,
        MinMax<double>? itemPriceMinMax = null)
    {
        if (!BotRoleExistsInCache(botRole))
        {
            InitCacheForBotRole(botRole);
            AddLootToCache(botRole, isPmc, botJsonTemplate);
        }

        Dictionary<string, double> result = null;
        BotLootCache botRoleCache;

        botRoleCache = _lootCache[botRole];

        switch (lootType)
        {
            case LootCacheType.Special:
                result = botRoleCache.SpecialItems;
                break;
            case LootCacheType.Backpack:
                result = botRoleCache.BackpackLoot;
                break;
            case LootCacheType.Pocket:
                result = botRoleCache.PocketLoot;
                break;
            case LootCacheType.Vest:
                result = botRoleCache.VestLoot;
                break;
            case LootCacheType.Secure:
                result = botRoleCache.SecureLoot;
                break;
            case LootCacheType.Combined:
                result = botRoleCache.CombinedPoolLoot;
                break;
            case LootCacheType.HealingItems:
                result = botRoleCache.HealingItems;
                break;
            case LootCacheType.GrenadeItems:
                result = botRoleCache.GrenadeItems;
                break;
            case LootCacheType.DrugItems:
                result = botRoleCache.DrugItems;
                break;
            case LootCacheType.FoodItems:
                result = botRoleCache.FoodItems;
                break;
            case LootCacheType.DrinkItems:
                result = botRoleCache.DrinkItems;
                break;
            case LootCacheType.CurrencyItems:
                result = botRoleCache.CurrencyItems;
                break;
            case LootCacheType.StimItems:
                result = botRoleCache.StimItems;
                break;
            default:
                _logger.Error(
                    _localisationService.GetText(
                        "bot-loot_type_not_found",
                        new
                        {
                            lootType,
                            botRole,
                            isPmc
                        }
                    )
                );
                break;
        }

        if (itemPriceMinMax is not null)
        {
            var filteredResult = result.Where(i =>
                {
                    var itemPrice = _itemHelper.GetItemPrice(i.Key);
                    if (itemPriceMinMax?.Min is not null && itemPriceMinMax?.Max is not null)
                    {
                        return itemPrice >= itemPriceMinMax?.Min && itemPrice <= itemPriceMinMax?.Max;
                    }

                    if (itemPriceMinMax?.Min is not null && itemPriceMinMax?.Max is null)
                    {
                        return itemPrice >= itemPriceMinMax?.Min;
                    }

                    if (itemPriceMinMax?.Min is null && itemPriceMinMax?.Max is not null)
                    {
                        return itemPrice <= itemPriceMinMax?.Max;
                    }

                    return false;
                }
            );

            return _cloner.Clone(filteredResult.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        return _cloner.Clone(result);
    }

    /// <summary>
    ///     Generate loot for a bot and store inside a private class property
    /// </summary>
    /// <param name="botRole">bots role (assault / pmcBot etc)</param>
    /// <param name="isPmc">Is the bot a PMC (alters what loot is cached)</param>
    /// <param name="botJsonTemplate">db template for bot having its loot generated</param>
    protected void AddLootToCache(string botRole, bool isPmc, BotType botJsonTemplate)
    {
        // Full pool of loot we use to create the various sub-categories with
        var lootPool = botJsonTemplate.BotInventory.Items;

        // Flatten all individual slot loot pools into one big pool, while filtering out potentially missing templates
        Dictionary<string, double> specialLootPool = new();
        Dictionary<string, double> backpackLootPool = new();
        Dictionary<string, double> pocketLootPool = new();
        Dictionary<string, double> vestLootPool = new();
        Dictionary<string, double> secureLootPool = new();
        Dictionary<string, double> combinedLootPool = new();

        if (isPmc)
        {
            // Replace lootPool from bot json with our own generated list for PMCs
            lootPool.Backpack = _cloner.Clone(_pmcLootGenerator.GeneratePMCBackpackLootPool(botRole));
            lootPool.Pockets = _cloner.Clone(_pmcLootGenerator.GeneratePMCPocketLootPool(botRole));
            lootPool.TacticalVest = _cloner.Clone(_pmcLootGenerator.GeneratePMCVestLootPool(botRole));
        }

        // Backpack/Pockets etc
        var poolsToProcess =
            new Dictionary<string, Dictionary<string, double>>
            {
                { "Backpack", lootPool.Backpack },
                { "Pockets", lootPool.Pockets },
                { "SecuredContainer", lootPool.SecuredContainer },
                { "SpecialLoot", lootPool.SpecialLoot },
                { "TacticalVest", lootPool.TacticalVest }
            };


        foreach (var (containerType, itemPool) in poolsToProcess)
        {
            // No items to add, skip
            if (itemPool.Count == 0)
            {
                continue;
            }

            // Sort loot pool into separate buckets
            switch (containerType)
            {
                case "SpecialLoot":
                    AddItemsToPool(specialLootPool, itemPool);
                    break;
                case "Pockets":
                    AddItemsToPool(pocketLootPool, itemPool);
                    break;
                case "TacticalVest":
                    AddItemsToPool(vestLootPool, itemPool);
                    break;
                case "SecuredContainer":
                    AddItemsToPool(secureLootPool, itemPool);
                    break;
                case "Backpack":
                    AddItemsToPool(backpackLootPool, itemPool);
                    break;
                default:
                    _logger.Warning($"How did you get here {containerType}");
                    break;
            }

            // Add all items (if any) to combined pool (excluding secure)
            if (itemPool.Count > 0 && containerType.Equals("securedcontainer", StringComparison.OrdinalIgnoreCase))
            {
                AddItemsToPool(combinedLootPool, itemPool);
            }
        }

        // Assign whitelisted special items to bot if any exist
        var specialLootItems = GetGenerationWeights(botJsonTemplate.BotGeneration?.Items?.SpecialItems?.Whitelist);

        // No whitelist, find and assign from combined item pool
        if (!specialLootItems.Any())
            // key = tpl, value = weight
        {
            foreach (var itemKvP in specialLootPool)
            {
                var itemTemplate = _itemHelper.GetItem(itemKvP.Key).Value;
                if (!(IsBulletOrGrenade(itemTemplate.Properties) || IsMagazine(itemTemplate.Properties)))
                {
                    lock (_specialLock)
                    {
                        specialLootItems.TryAdd(itemKvP.Key, itemKvP.Value);
                    }
                }
            }
        }

        // Assign whitelisted healing items to bot if any exist
        var healingItems = GetGenerationWeights(botJsonTemplate.BotGeneration?.Items?.Healing?.Whitelist);

        // No whitelist, find and assign from combined item pool
        if (!healingItems.Any())
            // key = tpl, value = weight
        {
            foreach (var itemKvP in combinedLootPool)
            {
                var itemTemplate = _itemHelper.GetItem(itemKvP.Key).Value;
                if (
                    IsMedicalItem(itemTemplate.Properties) &&
                    itemTemplate.Parent != BaseClasses.STIMULATOR &&
                    itemTemplate.Parent != BaseClasses.DRUGS
                )
                {
                    lock (_healingLock)
                    {
                        healingItems.TryAdd(itemKvP.Key, itemKvP.Value);
                    }
                }
            }
        }

        // Assign whitelisted drugs to bot if any exist
        var drugItems = GetGenerationWeights(botJsonTemplate.BotGeneration?.Items?.Drugs?.Whitelist);

        // no drugs whitelist, find and assign from combined item pool
        if (!drugItems.Any())
        {
            foreach (var itemKvP in combinedLootPool)
            {
                var itemTemplate = _itemHelper.GetItem(itemKvP.Key).Value;
                if (!IsMedicalItem(itemTemplate.Properties) || itemTemplate.Parent != BaseClasses.DRUGS)
                {
                    // Not a drug/medical item, skip
                    continue;
                }

                lock (_drugLock)
                {
                    drugItems.TryAdd(itemKvP.Key, itemKvP.Value);
                }
            }
        }

        // Assign whitelisted food to bot if any exist
        var foodItems = GetGenerationWeights(botJsonTemplate.BotGeneration?.Items?.Food?.Whitelist);
        // No food whitelist, find and assign from combined item pool
        if (!foodItems.Any())
        {
            foreach (var itemKvP in combinedLootPool)
            {
                var itemTemplate = _itemHelper.GetItem(itemKvP.Key).Value;
                if (!_itemHelper.IsOfBaseclass(itemTemplate.Id, BaseClasses.FOOD))
                {
                    // Not food, skip
                    continue;
                }

                lock (_foodLock)
                {
                    foodItems.TryAdd(itemKvP.Key, itemKvP.Value);
                }
            }
        }

        // Assign whitelisted drink to bot if any exist
        var drinkItems = GetGenerationWeights(botJsonTemplate.BotGeneration?.Items?.Food?.Whitelist);

        // No drink whitelist, find and assign from combined item pool
        if (!drinkItems.Any())
        {
            foreach (var itemKvP in combinedLootPool)
            {
                var itemTemplate = _itemHelper.GetItem(itemKvP.Key).Value;
                if (!_itemHelper.IsOfBaseclass(itemTemplate.Id, BaseClasses.DRINK))
                {
                    // Not a drink, skip
                    continue;
                }

                lock (_drinkLock)
                {
                    drinkItems.TryAdd(itemKvP.Key, itemKvP.Value);
                }
            }
        }

        // Assign whitelisted currency to bot if any exist
        var currencyItems = GetGenerationWeights(botJsonTemplate.BotGeneration?.Items?.Currency?.Whitelist);

        // No currency whitelist, find and assign from combined item pool
        if (!currencyItems.Any())
        {
            foreach (var itemKvP in combinedLootPool)
            {
                var itemTemplate = _itemHelper.GetItem(itemKvP.Key).Value;
                if (!_itemHelper.IsOfBaseclass(itemTemplate.Id, BaseClasses.MONEY))
                {
                    continue;
                }

                lock (_currencyLock)
                {
                    currencyItems.TryAdd(itemKvP.Key, itemKvP.Value);
                }
            }
        }

        // Assign whitelisted stims to bot if any exist
        var stimItems = GetGenerationWeights(botJsonTemplate.BotGeneration?.Items?.Stims?.Whitelist);
        // No whitelist, find and assign from combined item pool
        if (!stimItems.Any())
        {
            foreach (var itemKvP in combinedLootPool)
            {
                var itemTemplate = _itemHelper.GetItem(itemKvP.Key).Value;
                if (!IsMedicalItem(itemTemplate.Properties) || itemTemplate.Parent != BaseClasses.STIMULATOR)
                {
                    continue;
                }

                lock (_stimLock)
                {
                    stimItems.TryAdd(itemKvP.Key, itemKvP.Value);
                }
            }
        }

        // Assign whitelisted grenades to bot if any exist
        var grenadeItems = GetGenerationWeights(botJsonTemplate.BotGeneration?.Items?.Grenades?.Whitelist);
        // no whitelist, find and assign from combined item pool
        if (!grenadeItems.Any())
        {
            foreach (var itemKvP in combinedLootPool)
            {
                var itemTemplate = _itemHelper.GetItem(itemKvP.Key).Value;
                if (!IsGrenade(itemTemplate.Properties))
                {
                    continue;
                }

                lock (_grenadeLock)
                {
                    grenadeItems.TryAdd(itemKvP.Key, itemKvP.Value);
                }
            }
        }

        // Get backpack loot (excluding magazines, bullets, grenades, drink, food and healing/stim items)
        var filteredBackpackItems = new Dictionary<string, double>();
        foreach (var itemKvP in backpackLootPool)
        {
            var itemResult = _itemHelper.GetItem(itemKvP.Key);
            if (itemResult.Value is null)
            {
                continue;
            }

            var itemTemplate = itemResult.Value;
            if (
                    IsBulletOrGrenade(itemTemplate.Properties) ||
                    IsMagazine(itemTemplate.Properties) ||
                    IsMedicalItem(itemTemplate.Properties) ||
                    IsGrenade(itemTemplate.Properties) ||
                    IsFood(itemTemplate.Id) ||
                    IsDrink(itemTemplate.Id) ||
                    IsCurrency(itemTemplate.Id)
                )
                // Is type we don't want as backpack loot, skip
            {
                continue;
            }

            filteredBackpackItems.TryAdd(itemKvP.Key, itemKvP.Value);
        }

        // Get pocket loot (excluding magazines, bullets, grenades, drink, food medical and healing/stim items)
        var filteredPocketItems = new Dictionary<string, double>();
        foreach (var itemKvP in pocketLootPool)
        {
            var itemResult = _itemHelper.GetItem(itemKvP.Key);
            if (itemResult.Value is null)
            {
                continue;
            }

            var itemTemplate = itemResult.Value;
            if (
                IsBulletOrGrenade(itemTemplate.Properties) ||
                IsMagazine(itemTemplate.Properties) ||
                IsMedicalItem(itemTemplate.Properties) ||
                IsGrenade(itemTemplate.Properties) ||
                IsFood(itemTemplate.Id) ||
                IsDrink(itemTemplate.Id) ||
                IsCurrency(itemTemplate.Id) ||
                itemTemplate.Properties.Height is null || // lacks height
                itemTemplate.Properties.Width is null // lacks width
            )
            {
                continue;
            }

            filteredPocketItems.TryAdd(itemKvP.Key, itemKvP.Value);
        }

        // Get vest loot (excluding magazines, bullets, grenades, medical and healing/stim items)
        var filteredVestItems = new Dictionary<string, double>();
        foreach (var itemKvP in vestLootPool)
        {
            var itemResult = _itemHelper.GetItem(itemKvP.Key);
            if (itemResult.Value is null)
            {
                continue;
            }

            var itemTemplate = itemResult.Value;
            if (
                IsBulletOrGrenade(itemTemplate.Properties) ||
                IsMagazine(itemTemplate.Properties) ||
                IsMedicalItem(itemTemplate.Properties) ||
                IsGrenade(itemTemplate.Properties) ||
                IsFood(itemTemplate.Id) ||
                IsDrink(itemTemplate.Id) ||
                IsCurrency(itemTemplate.Id)
            )
            {
                continue;
            }

            filteredVestItems.TryAdd(itemKvP.Key, itemKvP.Value);
        }

        // Get secure loot (excluding magazines, bullets)
        var filteredSecureLoot = new Dictionary<string, double>();
        foreach (var itemKvP in secureLootPool)
        {
            var itemResult = _itemHelper.GetItem(itemKvP.Key);
            if (itemResult.Value is null)
            {
                continue;
            }

            var itemTemplate = itemResult.Value;
            if (IsBulletOrGrenade(itemTemplate.Properties) || IsMagazine(itemTemplate.Properties))
            {
                continue;
            }

            filteredSecureLoot.TryAdd(itemKvP.Key, itemKvP.Value);
        }

        var cacheForRole = _lootCache[botRole];
        cacheForRole.HealingItems = healingItems;
        cacheForRole.DrugItems = drugItems;
        cacheForRole.FoodItems = foodItems;
        cacheForRole.DrinkItems = drinkItems;
        cacheForRole.CurrencyItems = currencyItems;
        cacheForRole.StimItems = stimItems;
        cacheForRole.GrenadeItems = grenadeItems;
        cacheForRole.SpecialItems = specialLootItems;
        cacheForRole.BackpackLoot = filteredBackpackItems;
        cacheForRole.PocketLoot = filteredPocketItems;
        cacheForRole.VestLoot = filteredVestItems;
        cacheForRole.SecureLoot = filteredSecureLoot;
    }

    /// <summary>
    /// Return provided weights or an empty dictionary
    /// </summary>
    /// <param name="weights">Weights to return</param>
    /// <returns>Dictionary</returns>
    protected static Dictionary<string, double> GetGenerationWeights(Dictionary<string, double>? weights)
    {
        return weights ?? [];
    }

    protected void AddItemsToPool(Dictionary<string, double> poolToAddTo, Dictionary<string, double> poolOfItemsToAdd)
    {
        foreach (var tpl in poolOfItemsToAdd)
        {
            // Skip adding items that already exist
            if (poolToAddTo.ContainsKey(tpl.Key))
            {
                continue;
            }

            poolToAddTo.TryAdd(tpl.Key, poolOfItemsToAdd[tpl.Key]);
        }
    }

    /// <summary>
    ///     Ammo/grenades have this property
    /// </summary>
    /// <param name="props"></param>
    /// <returns></returns>
    protected bool IsBulletOrGrenade(Props props)
    {
        return props.AmmoType is not null;
    }

    /// <summary>
    ///     Internal and external magazine have this property
    /// </summary>
    /// <param name="props"></param>
    /// <returns></returns>
    protected bool IsMagazine(Props props)
    {
        return props.ReloadMagType is not null;
    }

    /// <summary>
    ///     Medical use items (e.g. morphine/lip balm/grizzly)
    /// </summary>
    /// <param name="props"></param>
    /// <returns></returns>
    protected bool IsMedicalItem(Props props)
    {
        return props.MedUseTime is not null;
    }

    /// <summary>
    ///     Grenades have this property (e.g. smoke/frag/flash grenades)
    /// </summary>
    /// <param name="props"></param>
    /// <returns></returns>
    protected bool IsGrenade(Props props)
    {
        return props.ThrowType is not null;
    }

    protected bool IsFood(string tpl)
    {
        return _itemHelper.IsOfBaseclass(tpl, BaseClasses.FOOD);
    }

    protected bool IsDrink(string tpl)
    {
        return _itemHelper.IsOfBaseclass(tpl, BaseClasses.DRINK);
    }

    protected bool IsCurrency(string tpl)
    {
        return _itemHelper.IsOfBaseclass(tpl, BaseClasses.MONEY);
    }

    /// <summary>
    ///     Check if a bot type exists inside the loot cache
    /// </summary>
    /// <param name="botRole">role to check for</param>
    /// <returns>true if they exist</returns>
    protected bool BotRoleExistsInCache(string botRole)
    {
        return _lootCache.ContainsKey(botRole);
    }

    /// <summary>
    ///     If lootcache is undefined, init with empty property arrays
    /// </summary>
    /// <param name="botRole">Bot role to hydrate</param>
    protected void InitCacheForBotRole(string botRole)
    {
        if (
            !_lootCache.TryAdd(
                botRole,
                new BotLootCache
                {
                    BackpackLoot = new Dictionary<string, double>(),
                    PocketLoot = new Dictionary<string, double>(),
                    VestLoot = new Dictionary<string, double>(),
                    SecureLoot = new Dictionary<string, double>(),
                    CombinedPoolLoot = new Dictionary<string, double>(),

                    SpecialItems = new Dictionary<string, double>(),
                    GrenadeItems = new Dictionary<string, double>(),
                    DrugItems = new Dictionary<string, double>(),
                    FoodItems = new Dictionary<string, double>(),
                    DrinkItems = new Dictionary<string, double>(),
                    CurrencyItems = new Dictionary<string, double>(),
                    HealingItems = new Dictionary<string, double>(),
                    StimItems = new Dictionary<string, double>()
                }
            )
        )
        {
            if (_logger.IsLogEnabled(LogLevel.Debug))
            {
                _logger.Debug($"Unable to add loot cache for bot role: {botRole} - already exists");
            }
        }
    }

    /// <summary>
    ///     Compares two item prices by their flea (or handbook if that doesn't exist) price
    /// </summary>
    /// <param name="itemAPrice"></param>
    /// <param name="itemBPrice"></param>
    /// <returns></returns>
    protected int CompareByValue(int itemAPrice, int itemBPrice)
    {
        // If item A has no price, it should be moved to the back when sorting
        if (itemAPrice is 0)
        {
            return 1;
        }

        if (itemBPrice is 0)
        {
            return -1;
        }

        if (itemAPrice < itemBPrice)
        {
            return -1;
        }

        if (itemAPrice > itemBPrice)
        {
            return 1;
        }

        return 0;
    }
}
