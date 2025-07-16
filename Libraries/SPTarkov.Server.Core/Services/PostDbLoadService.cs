using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SPTarkov.Server.Core.Services;

[Injectable(InjectionType.Singleton)]
public class PostDbLoadService(
    ISptLogger<PostDbLoadService> logger,
    DatabaseService databaseService,
    ServerLocalisationService serverLocalisationService,
    SeasonalEventService seasonalEventService,
    CustomLocationWaveService customLocationWaveService,
    OpenZoneService openZoneService,
    ItemBaseClassService itemBaseClassService,
    RaidWeatherService raidWeatherService,
    ConfigServer configServer,
    ICloner cloner
)
{
    protected readonly BotConfig _botConfig = configServer.GetConfig<BotConfig>();
    protected readonly CoreConfig _coreConfig = configServer.GetConfig<CoreConfig>();
    protected readonly HideoutConfig _hideoutConfig = configServer.GetConfig<HideoutConfig>();
    protected readonly ItemConfig _itemConfig = configServer.GetConfig<ItemConfig>();
    protected readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();
    protected readonly LootConfig _lootConfig = configServer.GetConfig<LootConfig>();
    protected readonly PmcConfig _pmcConfig = configServer.GetConfig<PmcConfig>();
    protected readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();

    public void PerformPostDbLoadActions()
    {
        // Regenerate base cache now mods are loaded and game is starting
        // Mods that add items and use the baseClass service generate the cache including their items, the next mod that
        // add items gets left out,causing warnings
        itemBaseClassService.HydrateItemBaseClassCache();

        // Validate that only mongoIds exist in items, quests, and traders
        // Kill the startup if not.
        // TODO: We can probably remove this in a couple versions
        databaseService.ValidateDatabase();
        if (!databaseService.IsDatabaseValid())
        {
            throw new Exception("Server start failure, database invalid");
        }

        AddCustomLooseLootPositions();

        MergeCustomAchievements();

        AdjustMinReserveRaiderSpawnChance();

        if (_coreConfig.Fixes.FixShotgunDispersion)
        {
            FixShotgunDispersions();
        }

        if (_locationConfig.AddOpenZonesToAllMaps)
        {
            openZoneService.ApplyZoneChangesToAllMaps();
        }

        if (_pmcConfig.RemoveExistingPmcWaves.GetValueOrDefault(false))
        {
            RemoveExistingPmcWaves();
        }

        if (_locationConfig.AddCustomBotWavesToMaps)
        {
            customLocationWaveService.ApplyWaveChangesToAllMaps();
        }

        if (_locationConfig.EnableBotTypeLimits)
        {
            AdjustMapBotLimits();
        }

        AdjustLooseLootSpawnProbabilities();

        AdjustLocationBotValues();

        MergeCustomHideoutAreas();

        if (_locationConfig.RogueLighthouseSpawnTimeSettings.Enabled)
        {
            FixRoguesSpawningInstantlyOnLighthouse();
        }

        AdjustLabsRaiderSpawnRate();

        AdjustHideoutCraftTimes(_hideoutConfig.OverrideCraftTimeSeconds);
        AdjustHideoutBuildTimes(_hideoutConfig.OverrideBuildTimeSeconds);

        UnlockHideoutLootCrateCrafts();

        CloneExistingCraftsAndAddNew();

        RemoveNewBeginningRequirementFromPrestige();

        RemovePraporTestMessage();

        ValidateQuestAssortUnlocksExist();

        if (seasonalEventService.IsAutomaticEventDetectionEnabled())
        {
            seasonalEventService.CacheActiveEvents();
            seasonalEventService.EnableSeasonalEvents();
        }

        // Flea bsg blacklist is off
        if (!_ragfairConfig.Dynamic.Blacklist.EnableBsgList)
        {
            SetAllDbItemsAsSellableOnFlea();
        }

        AddMissingTraderBuyRestrictionMaxValue();

        ApplyFleaPriceOverrides();

        AddCustomItemPresetsToGlobals();

        var currentSeason = seasonalEventService.GetActiveWeatherSeason();
        raidWeatherService.GenerateWeather(currentSeason);

        if (_botConfig.WeeklyBoss.Enabled)
        {
            var chosenBoss = GetWeeklyBoss(
                _botConfig.WeeklyBoss.BossPool,
                _botConfig.WeeklyBoss.ResetDay
            );
            FlagMapAsGuaranteedBoss(chosenBoss);
        }
    }

    /// <summary>
    /// Choose a boss that will spawn at 100% on a map from a predefined collection of bosses
    /// </summary>
    /// <param name="bosses">Pool of bosses to pick from</param>
    /// <param name="bossResetDay">Day of week choice of boss changes</param>
    /// <returns>Boss to spawn for this week</returns>
    protected WildSpawnType GetWeeklyBoss(List<WildSpawnType> bosses, DayOfWeek bossResetDay)
    {
        // Get closest monday to today
        var startOfWeek = DateTime.Today.GetMostRecentPreviousDay(bossResetDay);

        // Create a consistent seed for the week using the year and the day of the year of above monday chosen
        // This results in seed being identical for the week
        var seed = startOfWeek.Year * 1000 + startOfWeek.DayOfYear;

        // Init Random class with unique seed
        var random = new Random(seed);

        // First number generated by random.Next() will always be the same because of the seed
        return bosses[random.Next(0, bosses.Count)];
    }

    /// <summary>
    /// Given the provided boss, flag them as 100% spawn and add skull to the map they spawn on
    /// </summary>
    /// <param name="boss">Boss to flag</param>
    protected void FlagMapAsGuaranteedBoss(WildSpawnType boss)
    {
        // Get the corresponding map for the provided boss
        var locations = databaseService.GetLocations();
        Location? location;
        switch (boss)
        {
            case WildSpawnType.bossBully:
                location = locations.Bigmap;
                break;
            case WildSpawnType.bossGluhar:
                location = locations.RezervBase;
                break;
            case WildSpawnType.bossKilla:
                location = locations.Interchange;
                break;
            case WildSpawnType.bossKojaniy:
                location = locations.Woods;
                break;
            case WildSpawnType.bossSanitar:
                location = locations.Shoreline;
                break;
            case WildSpawnType.bossKolontay:
                location = locations.TarkovStreets;
                break;
            case WildSpawnType.bossKnight:
                location = locations.Lighthouse;
                break;
            case WildSpawnType.bossTagilla:
                location = locations.Factory4Day;
                break;
            default:
                logger.Warning($"Unknown boss type: {boss}. Unable to set as weekly. Skipping");
                return;
        }

        var bossSpawn = location.Base.BossLocationSpawn.FirstOrDefault(x =>
            x.BossName == boss.ToString()
        );
        if (bossSpawn is null)
        {
            logger.Warning($"Boss: {boss} not found on map, unable to set as weekly. Skipping");
            return;
        }

        logger.Debug($"{boss} is boss of the week");
        bossSpawn.BossChance = 100;
        bossSpawn.ShowOnTarkovMap = true;
        bossSpawn.ShowOnTarkovMapPvE = true;
    }

    private void MergeCustomHideoutAreas()
    {
        var hideout = databaseService.GetHideout();
        foreach (var customArea in hideout.CustomAreas)
        {
            // Check if exists
            if (hideout.Areas!.Exists(area => area.Id == customArea.Id))
            {
                logger.Warning(
                    $"Unable to add new hideout area with Id: {customArea.Id} as ID is already in use, skipping"
                );

                continue;
            }

            hideout.Areas.Add(customArea);
        }
    }

    /// <summary>
    ///     Merge custom achievements into achievement db table
    /// </summary>
    protected void MergeCustomAchievements()
    {
        var achievements = databaseService.GetAchievements();
        foreach (var customAchievement in databaseService.GetCustomAchievements())
        {
            if (achievements.Exists(a => a.Id == customAchievement.Id))
            {
                logger.Debug(
                    $"Unable to add custom achievement as id: {customAchievement.Id} already exists"
                );
                continue;
            }

            achievements.Add(customAchievement);
        }
    }

    private void RemoveNewBeginningRequirementFromPrestige()
    {
        var prestigeDb = databaseService.GetTemplates().Prestige;
        var newBeginningQuestId = new HashSet<string>
        {
            "6761f28a022f60bb320f3e95",
            "6761ff17cdc36bd66102e9d0",
        };
        foreach (var prestige in prestigeDb.Elements)
        {
            var itemToRemove = prestige.Conditions?.FirstOrDefault(cond =>
                newBeginningQuestId.Contains(cond.Target?.Item)
            );
            if (itemToRemove is null)
            {
                continue;
            }

            var indexToRemove = prestige.Conditions.IndexOf(itemToRemove);
            if (indexToRemove != -1)
            {
                prestige.Conditions.RemoveAt(indexToRemove);
            }
        }
    }

    private void RemovePraporTestMessage()
    {
        foreach (var (locale, lazyLoad) in databaseService.GetLocales().Global)
        {
            lazyLoad.AddTransformer(lazyloadedData =>
            {
                lazyloadedData["61687e2c3e526901fa76baf9"] = "";

                return lazyloadedData;
            });
        }
    }

    protected void CloneExistingCraftsAndAddNew()
    {
        var hideoutCraftDb = databaseService.GetHideout().Production;
        var craftsToAdd = _hideoutConfig.HideoutCraftsToAdd;
        foreach (var craftToAdd in craftsToAdd)
        {
            var clonedCraft = cloner.Clone(
                hideoutCraftDb.Recipes.FirstOrDefault(x => x.Id == craftToAdd.CraftIdToCopy)
            );
            if (clonedCraft is null)
            {
                logger.Warning(
                    $"Unable to find hideout craft: {craftToAdd.CraftIdToCopy}, skipping"
                );

                continue;
            }

            clonedCraft.Id = craftToAdd.NewId;
            clonedCraft.Requirements = craftToAdd.Requirements;
            clonedCraft.EndProduct = craftToAdd.CraftOutputTpl;

            hideoutCraftDb.Recipes.Add(clonedCraft);
        }
    }

    protected void AdjustMinReserveRaiderSpawnChance()
    {
        // Get reserve base.json
        var reserveBase = databaseService.GetLocation(ELocationName.RezervBase.ToString()).Base;

        // Raiders are bosses, get only those from boss spawn array
        foreach (
            var raiderSpawn in reserveBase.BossLocationSpawn.Where(boss =>
                boss.BossName == "pmcBot"
            )
        )
        {
            var isTriggered = raiderSpawn.TriggerId.Length > 0; // Empty string if not triggered
            var newSpawnChance = isTriggered
                ? _locationConfig.ReserveRaiderSpawnChanceOverrides.Triggered
                : _locationConfig.ReserveRaiderSpawnChanceOverrides.NonTriggered;

            if (newSpawnChance == -1)
            {
                continue;
            }

            if (raiderSpawn.BossChance < newSpawnChance)
            // Desired chance is bigger than existing, override it
            {
                raiderSpawn.BossChance = newSpawnChance;
            }
        }
    }

    protected void AddCustomLooseLootPositions()
    {
        var looseLootPositionsToAdd = _lootConfig.LooseLoot;
        foreach (var (mapId, positionsToAdd) in looseLootPositionsToAdd)
        {
            if (mapId is null)
            {
                logger.Warning(
                    serverLocalisationService.GetText(
                        "location-unable_to_add_custom_loot_position",
                        mapId
                    )
                );

                continue;
            }

            databaseService
                .GetLocation(mapId)
                .LooseLoot.AddTransformer(looselootData =>
                {
                    if (looselootData is null)
                    {
                        logger.Warning(
                            serverLocalisationService.GetText(
                                "location-map_has_no_loose_loot_data",
                                mapId
                            )
                        );

                        return looselootData;
                    }

                    foreach (var positionToAdd in positionsToAdd)
                    {
                        // Exists already, add new items to existing positions pool
                        var existingLootPosition = looselootData.Spawnpoints.FirstOrDefault(x =>
                            x.Template.Id == positionToAdd.Template.Id
                        );

                        if (existingLootPosition is not null)
                        {
                            existingLootPosition.Template.Items.AddRange(
                                positionToAdd.Template.Items
                            );
                            existingLootPosition.ItemDistribution.AddRange(
                                positionToAdd.ItemDistribution
                            );

                            continue;
                        }

                        // New position, add entire object
                        looselootData.Spawnpoints.Add(positionToAdd);
                    }

                    return looselootData;
                });
        }
    }

    // BSG have two values for shotgun dispersion, we make sure both have the same value
    protected void FixShotgunDispersions()
    {
        var itemDb = databaseService.GetItems();

        var shotguns = new List<string>
        {
            Weapons.SHOTGUN_12G_SAIGA_12K,
            Weapons.SHOTGUN_20G_TOZ_106,
            Weapons.SHOTGUN_12G_M870,
            Weapons.SHOTGUN_12G_SAIGA_12K_FA,
        };
        foreach (var shotgunId in shotguns)
        {
            if (itemDb[shotgunId].Properties.ShotgunDispersion.HasValue)
            {
                itemDb[shotgunId].Properties.shotgunDispersion = itemDb[shotgunId]
                    .Properties
                    .ShotgunDispersion;
            }
        }
    }

    protected void RemoveExistingPmcWaves()
    {
        var locations = databaseService.GetLocations().GetDictionary();

        var pmcTypes = new HashSet<string> { "pmcUSEC", "pmcBEAR" };
        foreach (var locationkvP in locations)
        {
            if (locationkvP.Value?.Base?.BossLocationSpawn is null)
            {
                continue;
            }

            locationkvP.Value.Base.BossLocationSpawn = locationkvP
                .Value.Base.BossLocationSpawn.Where(bossSpawn =>
                    !pmcTypes.Contains(bossSpawn.BossName)
                )
                .ToList();
        }
    }

    /// <summary>
    ///     Apply custom limits on bot types as defined in configs/location.json/botTypeLimits
    /// </summary>
    protected void AdjustMapBotLimits()
    {
        var mapsDb = databaseService.GetLocations().GetDictionary();
        if (_locationConfig.BotTypeLimits is null)
        {
            return;
        }

        foreach (var (mapId, limits) in _locationConfig.BotTypeLimits)
        {
            if (!mapsDb.TryGetValue(mapId, out var map))
            {
                logger.Warning(
                    serverLocalisationService.GetText(
                        "bot-unable_to_edit_limits_of_unknown_map",
                        mapId
                    )
                );

                continue;
            }

            foreach (var botToLimit in limits)
            {
                var index = map.Base.MinMaxBots.FindIndex(x => x.WildSpawnType == botToLimit.Type);
                if (index != -1)
                {
                    // Existing bot type found in MinMaxBots array, edit
                    var limitObjectToUpdate = map.Base.MinMaxBots[index];
                    limitObjectToUpdate.Min = botToLimit.Min;
                    limitObjectToUpdate.Max = botToLimit.Max;
                }
                else
                {
                    // Bot type not found, add new object
                    map.Base.MinMaxBots.Add(
                        new MinMaxBot
                        {
                            // Bot type not found, add new object
                            WildSpawnType = botToLimit.Type,
                            Min = botToLimit.Min,
                            Max = botToLimit.Max,
                        }
                    );
                }
            }
        }
    }

    protected void AdjustLooseLootSpawnProbabilities()
    {
        if (_lootConfig.LooseLootSpawnPointAdjustments is null)
        {
            return;
        }

        foreach (var (mapId, mapAdjustments) in _lootConfig.LooseLootSpawnPointAdjustments)
        {
            databaseService
                .GetLocation(mapId)
                .LooseLoot.AddTransformer(looselootData =>
                {
                    if (looselootData is null)
                    {
                        logger.Warning(
                            serverLocalisationService.GetText(
                                "location-map_has_no_loose_loot_data",
                                mapId
                            )
                        );

                        return looselootData;
                    }

                    foreach (var (lootKey, newChanceValue) in mapAdjustments)
                    {
                        var lootPostionToAdjust = looselootData.Spawnpoints.FirstOrDefault(
                            spawnPoint => spawnPoint.Template.Id == lootKey
                        );
                        if (lootPostionToAdjust is null)
                        {
                            logger.Warning(
                                serverLocalisationService.GetText(
                                    "location-unable_to_adjust_loot_position_on_map",
                                    new { lootKey, mapId }
                                )
                            );

                            continue;
                        }

                        lootPostionToAdjust.Probability = newChanceValue;
                    }

                    return looselootData;
                });
        }
    }

    protected void AdjustLocationBotValues()
    {
        var mapsDb = databaseService.GetLocations();
        var mapsDict = mapsDb.GetDictionary();
        foreach (var (key, cap) in _botConfig.MaxBotCap)
        {
            // Keys given are like this: "factory4_night" use GetMappedKey to change to "Factory4Night" which the dictionary contains
            if (!mapsDict.TryGetValue(mapsDb.GetMappedKey(key), out var map))
            {
                continue;
            }

            map.Base.BotMaxPvE = cap;
            map.Base.BotMax = cap;

            // make values no larger than 30 secs
            map.Base.BotStart = Math.Min(map.Base.BotStart, 30);
        }
    }

    /// <summary>
    ///     Make Rogues spawn later to allow for scavs to spawn first instead of rogues filling up all spawn positions
    /// </summary>
    protected void FixRoguesSpawningInstantlyOnLighthouse()
    {
        var rogueSpawnDelaySeconds = _locationConfig
            .RogueLighthouseSpawnTimeSettings
            .WaitTimeSeconds;
        var lighthouse = databaseService.GetLocations().Lighthouse?.Base;
        if (lighthouse is null)
        // Just in case they remove this cursed map
        {
            return;
        }

        // Find Rogues that spawn instantly
        var instantRogueBossSpawns = lighthouse.BossLocationSpawn.Where(spawn =>
            spawn.BossName == "exUsec" && spawn.Time == -1
        );
        foreach (var wave in instantRogueBossSpawns)
        {
            wave.Time = rogueSpawnDelaySeconds;
        }
    }

    /// <summary>
    ///     Make non-trigger-spawned raiders spawn earlier + always
    /// </summary>
    protected void AdjustLabsRaiderSpawnRate()
    {
        var labsBase = databaseService.GetLocations().Laboratory.Base;

        // Find spawns with empty string for triggerId/TriggerName
        var nonTriggerLabsBossSpawns = labsBase.BossLocationSpawn.Where(bossSpawn =>
            bossSpawn.TriggerId is null && bossSpawn.TriggerName is null
        );

        foreach (var boss in nonTriggerLabsBossSpawns)
        {
            boss.BossChance = 100;
            boss.Time /= 10;
        }
    }

    protected void AdjustHideoutCraftTimes(int overrideSeconds)
    {
        if (overrideSeconds == -1)
        {
            return;
        }

        foreach (var craft in databaseService.GetHideout().Production.Recipes)
        // Only adjust crafts ABOVE the override
        {
            craft.ProductionTime = Math.Min(craft.ProductionTime.Value, overrideSeconds);
        }
    }

    /// <summary>
    ///     Adjust all hideout craft times to be no higher than the override
    /// </summary>
    /// <param name="overrideSeconds"> Time in seconds </param>
    protected void AdjustHideoutBuildTimes(int overrideSeconds)
    {
        if (overrideSeconds == -1)
        {
            return;
        }

        foreach (var area in databaseService.GetHideout().Areas)
        foreach (var (_, stage) in area.Stages)
        // Only adjust crafts ABOVE the override
        {
            stage.ConstructionTime = Math.Min(stage.ConstructionTime.Value, overrideSeconds);
        }
    }

    protected void UnlockHideoutLootCrateCrafts()
    {
        var hideoutLootBoxCraftIds = new List<string>
        {
            "66582be04de4820934746cea",
            "6745925da9c9adf0450d5bca",
            "67449c79268737ef6908d636",
        };

        foreach (var craftId in hideoutLootBoxCraftIds)
        {
            var recipe = databaseService
                .GetHideout()
                .Production.Recipes.FirstOrDefault(craft => craft.Id == craftId);
            if (recipe is not null)
            {
                recipe.Locked = false;
            }
        }
    }

    /// <summary>
    ///     Check for any missing assorts inside each traders assort.json data, checking against traders questassort.json
    /// </summary>
    protected void ValidateQuestAssortUnlocksExist()
    {
        var db = databaseService.GetTables();
        var traders = db.Traders;
        var quests = db.Templates.Quests;
        foreach (var (traderId, traderData) in traders)
        {
            var traderAssorts = traderData?.Assort;
            if (traderAssorts is null)
            {
                continue;
            }

            // Merge started/success/fail quest assorts into one dictionary
            var mergedQuestAssorts = new Dictionary<string, string>();
            mergedQuestAssorts = mergedQuestAssorts
                .Concat(traderData.QuestAssort["started"])
                .Concat(traderData.QuestAssort["success"])
                .Concat(traderData.QuestAssort["fail"])
                .ToDictionary();

            // Loop over all assorts for trader
            foreach (var (assortKey, questKey) in mergedQuestAssorts)
            // Does assort key exist in trader assort file
            {
                if (!traderAssorts.LoyalLevelItems.ContainsKey(assortKey))
                {
                    // Reverse lookup of enum key by value
                    var messageValues = new
                    {
                        traderName = traderId,
                        questName = quests[questKey]?.QuestName ?? "UNKNOWN",
                    };
                    logger.Warning(
                        serverLocalisationService.GetText(
                            "assort-missing_quest_assort_unlock",
                            messageValues
                        )
                    );
                }
            }
        }
    }

    protected void SetAllDbItemsAsSellableOnFlea()
    {
        var dbItems = databaseService.GetItems().Values.ToList();
        foreach (
            var item in dbItems.Where(item =>
                string.Equals(item.Type, "Item", StringComparison.OrdinalIgnoreCase)
                && !item.Properties.CanSellOnRagfair.GetValueOrDefault(false)
                && !_ragfairConfig.Dynamic.Blacklist.Custom.Contains(item.Id)
            )
        )
        {
            item.Properties.CanSellOnRagfair = true;
        }
    }

    protected void AddMissingTraderBuyRestrictionMaxValue()
    {
        var restrictions = databaseService
            .GetGlobals()
            .Configuration.TradingSettings.BuyRestrictionMaxBonus;
        restrictions["unheard_edition"] = new BuyRestrictionMaxBonus
        {
            Multiplier = restrictions["edge_of_darkness"].Multiplier,
        };
    }

    protected void ApplyFleaPriceOverrides()
    {
        var fleaPrices = databaseService.GetPrices();
        foreach (var (itemTpl, price) in _ragfairConfig.Dynamic.ItemPriceOverrideRouble)
        {
            fleaPrices[itemTpl] = price;
        }
    }

    protected void AddCustomItemPresetsToGlobals()
    {
        foreach (var presetToAdd in _itemConfig.CustomItemGlobalPresets)
        {
            if (databaseService.GetGlobals().ItemPresets.ContainsKey(presetToAdd.Id))
            {
                logger.Warning(
                    $"Global ItemPreset with Id of: {presetToAdd.Id} already exists, unable to overwrite"
                );
                continue;
            }

            databaseService.GetGlobals().ItemPresets.TryAdd(presetToAdd.Id, presetToAdd);
        }
    }
}
