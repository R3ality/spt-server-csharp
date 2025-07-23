using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace SPTarkov.Server.Core.Generators;

[Injectable]
public class LocationLootGenerator(
    ISptLogger<LocationLootGenerator> _logger,
    RandomUtil _randomUtil,
    ItemHelper _itemHelper,
    DatabaseService _databaseService,
    PresetHelper _presetHelper,
    ServerLocalisationService _serverLocalisationService,
    SeasonalEventService _seasonalEventService,
    ItemFilterService _itemFilterService,
    ConfigServer _configServer,
    CounterTrackerHelper counterTrackerHelper,
    ICloner _cloner
)
{
    protected readonly LocationConfig _locationConfig = _configServer.GetConfig<LocationConfig>();
    protected readonly SeasonalEventConfig _seasonalEventConfig =
        _configServer.GetConfig<SeasonalEventConfig>();

    /// <summary>
    /// Generate Loot for provided location ()
    /// </summary>
    /// <param name="locationId">Id of location (e.g. bigmap/factory4_day)</param>
    /// <returns>Collection of spawn points with loot</returns>
    public List<SpawnpointTemplate> GenerateLocationLoot(string locationId)
    {
        var result = new List<SpawnpointTemplate>();

        // Get generation details for location from db
        var locationDetails = _databaseService.GetLocation(locationId);
        if (locationDetails is null)
        {
            _logger.Error($"Location: {locationId} not found in database, generated 0 loot items");
            return result;
        }

        // Clone ammo data to ensure any changes don't affect the db values
        var staticAmmoDistClone = _cloner.Clone(locationDetails.StaticAmmo);

        // Pull location-specific spawn limits from db
        var itemsWithSpawnCountLimitsClone = _cloner.Clone(
            _locationConfig.LootMaxSpawnLimits.GetValueOrDefault(locationId.ToLowerInvariant())
        );

        // Store items with spawn count limits inside so they can be accessed later inside static/dynamic loot spawn methods
        if (itemsWithSpawnCountLimitsClone is not null)
        {
            counterTrackerHelper.AddDataToTrack(itemsWithSpawnCountLimitsClone);
        }

        // Create containers with loot
        result.AddRange(
            GenerateStaticContainers(locationId.ToLowerInvariant(), staticAmmoDistClone)
        );

        // Add dynamic loot to output loot
        var dynamicSpawnPoints = GenerateDynamicLoot(
            _cloner.Clone(locationDetails.LooseLoot.Value),
            staticAmmoDistClone,
            locationId.ToLowerInvariant()
        );

        // Merge dynamic spawns into result
        result.AddRange(dynamicSpawnPoints);

        _logger.Success(
            _serverLocalisationService.GetText(
                "location-dynamic_items_spawned_success",
                dynamicSpawnPoints.Count
            )
        );
        _logger.Success(
            _serverLocalisationService.GetText("location-generated_success", locationId)
        );

        // Clean up tracker
        counterTrackerHelper.Clear();

        return result;
    }

    /// Create a list of container objects with randomised loot
    /// <param name="locationId">Location to generate for</param>
    /// <param name="staticAmmoDist">Static ammo distribution</param>
    /// <returns>List of container objects</returns>
    public List<SpawnpointTemplate> GenerateStaticContainers(
        string locationId,
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist
    )
    {
        var staticLootItemCount = 0;
        var result = new List<SpawnpointTemplate>();

        var mapData = _databaseService.GetLocation(locationId);

        var staticWeaponsOnMapClone = _cloner.Clone(mapData.StaticContainers.Value.StaticWeapons);
        if (staticWeaponsOnMapClone is null)
        {
            _logger.Error(
                _serverLocalisationService.GetText(
                    "location-unable_to_find_static_weapon_for_map",
                    locationId
                )
            );
        }

        // Add mounted weapons to output loot
        result.AddRange(staticWeaponsOnMapClone);

        var allStaticContainersOnMapClone = _cloner.Clone(
            mapData.StaticContainers.Value.StaticContainers
        );
        if (allStaticContainersOnMapClone is null)
        {
            _logger.Error(
                _serverLocalisationService.GetText(
                    "location-unable_to_find_static_container_for_map",
                    locationId
                )
            );
        }

        // Containers that MUST be added to map (e.g. quest containers)
        var staticForcedOnMapClone = _cloner.Clone(mapData.StaticContainers.Value.StaticForced);
        if (staticForcedOnMapClone is null)
        {
            _logger.Error(
                _serverLocalisationService.GetText(
                    "location-unable_to_find_forced_static_data_for_map",
                    locationId
                )
            );
        }

        // Remove christmas items from loot data
        if (!_seasonalEventService.ChristmasEventEnabled())
        {
            allStaticContainersOnMapClone = allStaticContainersOnMapClone
                .Where(item =>
                    !_seasonalEventConfig.ChristmasContainerIds.Contains(item.Template.Id)
                )
                .ToList();
        }

        var staticRandomisableContainersOnMap = GetRandomisableContainersOnMap(
            allStaticContainersOnMapClone
        );

        // Keep track of static loot count
        var staticContainerCount = 0;

        // Find all 100% spawn containers
        var staticLootDist = mapData.StaticLoot;
        var guaranteedContainers = GetGuaranteedContainers(allStaticContainersOnMapClone);
        staticContainerCount += guaranteedContainers.Count();

        // Add loot to guaranteed containers and add to result
        foreach (
            var containerWithLoot in guaranteedContainers.Select(container =>
                AddLootToContainer(
                    container,
                    staticForcedOnMapClone,
                    staticLootDist.Value,
                    staticAmmoDist,
                    locationId
                )
            )
        )
        {
            result.Add(containerWithLoot.Template);

            staticLootItemCount += containerWithLoot.Template.Items.Count();
        }

        if (_logger.IsLogEnabled(LogLevel.Debug))
        {
            _logger.Debug($"Added {guaranteedContainers.Count()} guaranteed containers");
        }

        // Randomisation is turned off for location / globally
        if (!LocationRandomisationEnabled(locationId))
        {
            if (_logger.IsLogEnabled(LogLevel.Debug))
            {
                _logger.Debug(
                    $"Container randomisation disabled, Adding: {staticRandomisableContainersOnMap.Count()} containers to: {locationId}"
                );
            }

            foreach (var container in staticRandomisableContainersOnMap)
            {
                var containerWithLoot = AddLootToContainer(
                    container,
                    staticForcedOnMapClone,
                    staticLootDist.Value,
                    staticAmmoDist,
                    locationId
                );
                result.Add(containerWithLoot.Template);

                staticLootItemCount += containerWithLoot.Template.Items.Count();
            }

            _logger.Success($"A total of {staticLootItemCount} static items spawned");

            return result;
        }

        // Group containers by their groupId
        if (mapData.Statics is null)
        {
            _logger.Warning(
                _serverLocalisationService.GetText(
                    "location-unable_to_generate_static_loot",
                    locationId
                )
            );

            return result;
        }

        // For each of the container groups, choose from the pool of containers, hydrate container with loot and add to result array
        var mapping = GetGroupIdToContainerMappings(
            mapData.Statics,
            staticRandomisableContainersOnMap
        );
        foreach (var (key, data) in mapping)
        {
            // Count chosen was 0, skip
            if (data.ChosenCount == 0)
            {
                continue;
            }

            if (data.ContainerIdsWithProbability.Count == 0)
            {
                if (_logger.IsLogEnabled(LogLevel.Debug))
                {
                    _logger.Debug(
                        $"Group: {key} has no containers with < 100 % spawn chance to choose from, skipping"
                    );
                }

                continue;
            }

            // EDGE CASE: These are containers without a group and have a probability < 100%
            if (key == string.Empty)
            {
                var containerIdsCopy = _cloner.Clone(data.ContainerIdsWithProbability);
                // Roll each containers probability, if it passes, it gets added
                data.ContainerIdsWithProbability = new Dictionary<string, double>();
                foreach (var containerId in containerIdsCopy)
                {
                    if (_randomUtil.GetChance100(containerIdsCopy[containerId.Key] * 100))
                    {
                        data.ContainerIdsWithProbability[containerId.Key] = containerIdsCopy[
                            containerId.Key
                        ];
                    }
                }

                // Set desired count to size of array (we want all containers chosen)
                data.ChosenCount = data.ContainerIdsWithProbability.Count;

                // EDGE CASE: chosen container count could be 0
                if (data.ChosenCount == 0)
                {
                    continue;
                }
            }

            // Pass possible containers into function to choose some
            var chosenContainerIds = GetContainersByProbability(key, data);
            foreach (var chosenContainerId in chosenContainerIds)
            {
                // Look up container object from full list of containers on map
                var containerObject = staticRandomisableContainersOnMap.FirstOrDefault(
                    staticContainer => staticContainer.Template.Id == chosenContainerId
                );
                if (containerObject is null)
                {
                    if (_logger.IsLogEnabled(LogLevel.Debug))
                    {
                        _logger.Debug(
                            $"Container: {chosenContainerId} not found in staticRandomisableContainersOnMap, this is bad"
                        );
                    }

                    continue;
                }

                // Add loot to container and push into result object
                var containerWithLoot = AddLootToContainer(
                    containerObject,
                    staticForcedOnMapClone,
                    staticLootDist.Value,
                    staticAmmoDist,
                    locationId
                );
                result.Add(containerWithLoot.Template);
                staticContainerCount++;

                staticLootItemCount += containerWithLoot.Template.Items.Count();
            }
        }

        _logger.Success($"A total of: {staticLootItemCount} static items spawned");
        _logger.Success(
            _serverLocalisationService.GetText(
                "location-containers_generated_success",
                staticContainerCount
            )
        );

        return result;
    }

    protected bool LocationRandomisationEnabled(string locationId)
    {
        return _locationConfig.ContainerRandomisationSettings.Enabled
            && _locationConfig.ContainerRandomisationSettings.Maps.ContainsKey(locationId);
    }

    /// <summary>
    ///     Get containers with a non-100% chance to spawn OR are NOT on the container type randomistion blacklist
    /// </summary>
    /// <param name="staticContainers"></param>
    /// <returns>StaticContainerData array</returns>
    protected IEnumerable<StaticContainerData> GetRandomisableContainersOnMap(
        IEnumerable<StaticContainerData> staticContainers
    )
    {
        return staticContainers.Where(staticContainer =>
            staticContainer.Probability != 1
            && !staticContainer.Template.IsAlwaysSpawn.GetValueOrDefault(false)
            && !_locationConfig.ContainerRandomisationSettings.ContainerTypesToNotRandomise.Contains(
                staticContainer.Template.Items.FirstOrDefault().Template
            )
        );
    }

    /// <summary>
    ///     Get containers with 100% spawn rate or have a type on the randomistion ignore list
    /// </summary>
    /// <param name="staticContainersOnMap"></param>
    /// <returns>IStaticContainerData array</returns>
    protected IEnumerable<StaticContainerData> GetGuaranteedContainers(
        IEnumerable<StaticContainerData> staticContainersOnMap
    )
    {
        return staticContainersOnMap.Where(staticContainer =>
            staticContainer.Probability == 1
            || staticContainer.Template.IsAlwaysSpawn.GetValueOrDefault(false)
            || _locationConfig.ContainerRandomisationSettings.ContainerTypesToNotRandomise.Contains(
                staticContainer.Template.Items.FirstOrDefault().Template
            )
        );
    }

    /// <summary>
    ///     Choose a number of containers based on their probability value to fulfil the desired count in
    ///     containerData.chosenCount
    /// </summary>
    /// <param name="groupId">Name of the group the containers are being collected for</param>
    /// <param name="containerData">Containers and probability values for a groupId</param>
    /// <returns>List of chosen container Ids</returns>
    protected List<string> GetContainersByProbability(
        string groupId,
        ContainerGroupCount containerData
    )
    {
        var chosenContainerIds = new List<string>();

        var containerIds = containerData.ContainerIdsWithProbability.Keys;
        if (containerData.ChosenCount > containerIds.Count)
        {
            if (_logger.IsLogEnabled(LogLevel.Debug))
            {
                _logger.Debug(
                    $"Group: {groupId} wants: {containerData.ChosenCount} containers but pool only has: {containerIds.Count}, add what's available"
                );
            }

            return containerIds.ToList();
        }

        // Create probability array with all possible container ids in this group and their relative probability of spawning
        var containerDistribution = new ProbabilityObjectArray<string, double>(_cloner);
        foreach (var x in containerIds)
        {
            var value = containerData.ContainerIdsWithProbability[x];
            containerDistribution.Add(new ProbabilityObject<string, double>(x, value, value));
        }

        chosenContainerIds.AddRange(containerDistribution.Draw((int)containerData.ChosenCount));

        return chosenContainerIds;
    }

    /// <summary>
    ///     Get a mapping of each groupId and the containers in that group + count of containers to spawn on map
    /// </summary>
    /// <param name="staticContainerGroupData">Container group values</param>
    /// <param name="staticContainersOnMap"></param>
    /// <returns>dictionary keyed by groupId</returns>
    protected Dictionary<string, ContainerGroupCount> GetGroupIdToContainerMappings(
        StaticContainer staticContainerGroupData,
        IEnumerable<StaticContainerData> staticContainersOnMap
    )
    {
        // Create dictionary of all group ids and choose a count of containers the map will spawn of that group
        var mapping = new Dictionary<string, ContainerGroupCount>();
        foreach (var groupKvP in staticContainerGroupData.ContainersGroups)
        {
            mapping[groupKvP.Key] = new ContainerGroupCount
            {
                ContainerIdsWithProbability = new Dictionary<string, double>(),
                ChosenCount = _randomUtil.GetInt(
                    (int)
                        Math.Round(
                            groupKvP.Value.MinContainers.Value
                                * _locationConfig
                                    .ContainerRandomisationSettings
                                    .ContainerGroupMinSizeMultiplier
                        ),
                    (int)
                        Math.Round(
                            groupKvP.Value.MaxContainers.Value
                                * _locationConfig
                                    .ContainerRandomisationSettings
                                    .ContainerGroupMaxSizeMultiplier
                        )
                ),
            };
        }

        // Add an empty group for containers without a group id but still have a < 100% chance to spawn
        // Likely bad BSG data, will be fixed...eventually, example of the groupIds: `NEED_TO_BE_FIXED1`,`NEED_TO_BE_FIXED_SE02`, `NEED_TO_BE_FIXED_NW_01`
        mapping.Add(
            string.Empty,
            new ContainerGroupCount
            {
                ContainerIdsWithProbability = new Dictionary<string, double>(),
                ChosenCount = -1,
            }
        );

        // Iterate over all containers and add to group keyed by groupId
        // Containers without a group go into a group with empty key ""
        foreach (var container in staticContainersOnMap)
        {
            if (
                !staticContainerGroupData.Containers.TryGetValue(
                    container.Template.Id,
                    out var groupData
                )
            )
            {
                _logger.Error(
                    _serverLocalisationService.GetText(
                        "location-unable_to_find_container_in_statics_json",
                        container.Template.Id
                    )
                );

                continue;
            }

            if (container.Probability >= 1)
            {
                if (_logger.IsLogEnabled(LogLevel.Debug))
                {
                    _logger.Debug(
                        $"Container {container.Template.Id} with group: {groupData.GroupId} had 100 % chance to spawn was picked as random container, skipping"
                    );
                }

                continue;
            }

            mapping.TryAdd(
                groupData.GroupId,
                new ContainerGroupCount
                {
                    ChosenCount = 0d,
                    ContainerIdsWithProbability = new Dictionary<string, double>(),
                }
            );
            mapping[groupData.GroupId]
                .ContainerIdsWithProbability.TryAdd(
                    container.Template.Id,
                    container.Probability.Value
                );
        }

        return mapping;
    }

    /// <summary>
    ///     Choose loot to put into a static container based on weighting
    ///     Handle forced items + seasonal item removal when not in season
    /// </summary>
    /// <param name="staticContainer">The container itself we will add loot to</param>
    /// <param name="staticForced">Loot we need to force into the container</param>
    /// <param name="staticLootDist">staticLoot.json</param>
    /// <param name="staticAmmoDist">staticAmmo.json</param>
    /// <param name="locationName">Name of the map to generate static loot for</param>
    /// <returns>StaticContainerData</returns>
    protected StaticContainerData AddLootToContainer(
        StaticContainerData staticContainer,
        IEnumerable<StaticForced>? staticForced,
        Dictionary<string, StaticLootDetails> staticLootDist,
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist,
        string locationName
    )
    {
        var containerClone = _cloner.Clone(staticContainer);
        var containerTpl = containerClone.Template.Items.FirstOrDefault().Template;

        // Create new unique parent id to prevent any collisions
        var parentId = new MongoId();
        containerClone.Template.Root = parentId;
        containerClone.Template.Items.FirstOrDefault().Id = parentId;

        var containerMap = _itemHelper.GetContainerMapping(containerTpl);

        // Choose count of items to add to container
        var itemCountToAdd = GetWeightedCountOfContainerItems(
            containerTpl,
            staticLootDist,
            locationName
        );
        if (itemCountToAdd == 0)
        {
            return containerClone;
        }

        // Get all possible loot items for container
        var containerLootPool = GetPossibleLootItemsForContainer(containerTpl, staticLootDist);

        // Some containers need to have items forced into it (quest keys etc.)
        var tplsForced = staticForced
            .Where(forcedStaticProp => forcedStaticProp.ContainerId == containerClone.Template.Id)
            .Select(x => x.ItemTpl);

        // Draw random loot
        // Allow money to spawn more than once in container
        var failedToFitAttemptCount = 0;
        var lockList = _itemHelper.GetMoneyTpls();

        // Choose items to add to container, factor in weighting + lock money down
        // Filter out items picked that are already in the above `tplsForced` array
        var chosenTpls = containerLootPool
            .Draw(itemCountToAdd, _locationConfig.AllowDuplicateItemsInStaticContainers, lockList)
            .Where(tpl => !tplsForced.Contains(tpl))
            .Where(tpl => !counterTrackerHelper.IncrementCount(tpl));

        // Add forced loot to chosen item pool
        var tplsToAddToContainer = tplsForced.Concat(chosenTpls);
        if (!tplsToAddToContainer.Any())
        {
            _logger.Warning($"Added no items to container: {containerTpl}");
        }

        foreach (var tplToAdd in tplsToAddToContainer)
        {
            var chosenItemWithChildren = CreateStaticLootItem(tplToAdd, staticAmmoDist, parentId);
            if (chosenItemWithChildren is null)
            {
                continue;
            }

            // Check if item should have children removed
            var items = _locationConfig.TplsToStripChildItemsFrom.Contains(tplToAdd)
                ? [chosenItemWithChildren.Items.FirstOrDefault()] // Strip children from parent
                : chosenItemWithChildren.Items;

            // look for open slot to put chosen item into
            var result = containerMap.FindSlotForItem(
                chosenItemWithChildren.Width,
                chosenItemWithChildren.Height
            );
            if (!result.Success.GetValueOrDefault(false))
            {
                if (failedToFitAttemptCount > _locationConfig.FitLootIntoContainerAttempts)
                // x attempts to fit an item, container is probably full, stop trying to add more
                {
                    break;
                }

                // Can't fit item, skip
                failedToFitAttemptCount++;

                continue;
            }

            // Find somewhere for item inside container
            containerMap.FillContainerMapWithItem(
                result.X.Value,
                result.Y.Value,
                chosenItemWithChildren.Width,
                chosenItemWithChildren.Height,
                result.Rotation.GetValueOrDefault(false)
            );

            // Update root item properties with result of position finder
            items[0].SlotId = "main";
            items[0].Location = new ItemLocation
            {
                X = result.X,
                Y = result.Y,
                R = result.Rotation.GetValueOrDefault(false)
                    ? ItemRotation.Vertical
                    : ItemRotation.Horizontal,
            };

            // Add loot to container before returning
            var itemsToAdd = items.Select(item => item.ToLootItem()); // Convert into correct output type first
            containerClone.Template.Items = containerClone.Template.Items.Union(itemsToAdd);
        }

        return containerClone;
    }

    /// <summary>
    ///     Look up a containers itemCountDistribution data and choose an item count based on the found weights
    /// </summary>
    /// <param name="containerTypeId">Container to get item count for</param>
    /// <param name="staticLootDist">staticLoot.json</param>
    /// <param name="locationName">Map name (to get per-map multiplier for from config)</param>
    /// <returns>item count</returns>
    protected int GetWeightedCountOfContainerItems(
        string containerTypeId,
        Dictionary<string, StaticLootDetails> staticLootDist,
        string locationName
    )
    {
        // Create probability array to calculate the total count of lootable items inside container
        var itemCountArray = new ProbabilityObjectArray<int, float?>(_cloner);
        var countDistribution = staticLootDist[containerTypeId]?.ItemCountDistribution;
        if (countDistribution is null)
        {
            _logger.Warning(
                _serverLocalisationService.GetText(
                    "location-unable_to_find_count_distribution_for_container",
                    new { containerId = containerTypeId, locationName }
                )
            );

            return 0;
        }

        foreach (var itemCountDistribution in countDistribution)
        {
            // Add each count of items into array
            itemCountArray.Add(
                new ProbabilityObject<int, float?>(
                    itemCountDistribution.Count.Value,
                    itemCountDistribution.RelativeProbability.Value,
                    null
                )
            );
        }

        return (int)
            Math.Round(GetStaticLootMultiplierForLocation(locationName) * itemCountArray.Draw()[0]);
    }

    /// <summary>
    ///     Get all possible loot items that can be placed into a container
    ///     Do not add seasonal items if found + current date is inside seasonal event
    /// </summary>
    /// <param name="containerTypeId">Container to get possible loot for</param>
    /// <param name="staticLootDist">staticLoot.json</param>
    /// <returns>ProbabilityObjectArray of item tpls + probability</returns>
    protected ProbabilityObjectArray<MongoId, float?> GetPossibleLootItemsForContainer(
        string containerTypeId,
        Dictionary<string, StaticLootDetails> staticLootDist
    )
    {
        var seasonalEventActive = _seasonalEventService.SeasonalEventEnabled();
        var seasonalItemTplBlacklist = _seasonalEventService.GetInactiveSeasonalEventItems();

        var itemDistribution = new ProbabilityObjectArray<MongoId, float?>(_cloner);

        var itemContainerDistribution = staticLootDist[containerTypeId]?.ItemDistribution;
        if (itemContainerDistribution is null)
        {
            _logger.Warning(
                _serverLocalisationService.GetText(
                    "location-missing_item_distribution_data",
                    containerTypeId
                )
            );

            return itemDistribution;
        }

        foreach (var icd in itemContainerDistribution)
        {
            if (!seasonalEventActive && seasonalItemTplBlacklist.Contains(icd.Tpl))
            {
                // Skip seasonal event items if they're not enabled
                continue;
            }

            if (_itemFilterService.IsLootableItemBlacklisted(icd.Tpl))
            {
                // Ensure no blacklisted lootable items are in pool
                continue;
            }

            itemDistribution.Add(
                new ProbabilityObject<MongoId, float?>(icd.Tpl, icd.RelativeProbability.Value, null)
            );
        }

        return itemDistribution;
    }

    protected double GetLooseLootMultiplierForLocation(string location)
    {
        return _locationConfig.LooseLootMultiplier.TryGetValue(location, out var value)
            ? value
            : _locationConfig.LooseLootMultiplier["default"];
    }

    protected double GetStaticLootMultiplierForLocation(string location)
    {
        return _locationConfig.StaticLootMultiplier.TryGetValue(location, out var value)
            ? value
            : _locationConfig.StaticLootMultiplier["default"];
    }

    /// <summary>
    ///     Create array of loose + forced loot using probability system
    /// </summary>
    /// <param name="dynamicLootDist"></param>
    /// <param name="staticAmmoDist"></param>
    /// <param name="locationName">Location to generate loot for</param>
    /// <returns>Array of spawn points with loot in them</returns>
    public List<SpawnpointTemplate> GenerateDynamicLoot(
        LooseLoot dynamicLootDist,
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist,
        string locationName
    )
    {
        List<SpawnpointTemplate> loot = [];
        List<Spawnpoint> dynamicForcedSpawnPoints = [];

        // Remove christmas items from loot data
        if (!_seasonalEventService.ChristmasEventEnabled())
        {
            dynamicLootDist.Spawnpoints = dynamicLootDist.Spawnpoints.Where(point =>
                !point.Template.Id.StartsWith("christmas", StringComparison.OrdinalIgnoreCase)
            );
            dynamicLootDist.SpawnpointsForced = dynamicLootDist.SpawnpointsForced.Where(point =>
                !point.Template.Id.StartsWith("christmas", StringComparison.OrdinalIgnoreCase)
            );
        }

        // Build the list of forced loot from both `SpawnpointsForced` and any point marked `IsAlwaysSpawn`
        dynamicForcedSpawnPoints.AddRange(dynamicLootDist.SpawnpointsForced);
        dynamicForcedSpawnPoints.AddRange(
            dynamicLootDist.Spawnpoints.Where(point =>
                point.Template.IsAlwaysSpawn.GetValueOrDefault()
            )
        );

        loot.AddRange(GetForcedDynamicLoot(dynamicForcedSpawnPoints, locationName, staticAmmoDist));

        // Draw from random distribution
        var desiredSpawnPointCount = Math.Round(
            GetLooseLootMultiplierForLocation(locationName)
                * _randomUtil.GetNormallyDistributedRandomNumber(
                    dynamicLootDist.SpawnpointCount.Mean,
                    dynamicLootDist.SpawnpointCount.Std
                )
        );

        var blacklistedSpawnPoints = _locationConfig.LooseLootBlacklist.GetValueOrDefault(
            locationName
        );

        // Init empty array to hold spawn points, letting us pick them pseudo-randomly
        var spawnPointArray = new ProbabilityObjectArray<string, Spawnpoint>(_cloner);

        // Positions not in forced but have 100% chance to spawn
        List<Spawnpoint> guaranteedLoosePoints = [];

        var allDynamicSpawnPoints = dynamicLootDist.Spawnpoints;
        foreach (var spawnPoint in allDynamicSpawnPoints)
        {
            // Point is blacklisted, skip
            if (blacklistedSpawnPoints?.Contains(spawnPoint.Template.Id) ?? false)
            {
                if (_logger.IsLogEnabled(LogLevel.Debug))
                {
                    _logger.Debug($"Ignoring loose loot location: {spawnPoint.Template.Id}");
                }

                continue;
            }

            // We've handled IsAlwaysSpawn above, so skip them
            if (spawnPoint.Template.IsAlwaysSpawn ?? false)
            {
                continue;
            }

            // 100%, add it to guaranteed
            if (spawnPoint.Probability == 1)
            {
                guaranteedLoosePoints.Add(spawnPoint);
                continue;
            }

            spawnPointArray.Add(
                new ProbabilityObject<string, Spawnpoint>(
                    spawnPoint.Template.Id,
                    spawnPoint.Probability ?? 0,
                    spawnPoint
                )
            );
        }

        // Select a number of spawn points to add loot to
        // Add ALL loose loot with 100% chance to pool
        List<Spawnpoint> chosenSpawnPoints = [];
        chosenSpawnPoints.AddRange(guaranteedLoosePoints);

        var randomSpawnPointCount = desiredSpawnPointCount - chosenSpawnPoints.Count;
        // Only draw random spawn points if needed
        if (randomSpawnPointCount > 0 && spawnPointArray.Count > 0)
        // Add randomly chosen spawn points
        {
            foreach (var si in spawnPointArray.Draw((int)randomSpawnPointCount, false))
            {
                chosenSpawnPoints.Add(spawnPointArray.Data(si));
            }
        }

        // Filter out duplicate locationIds // prob can be done better
        chosenSpawnPoints = chosenSpawnPoints
            .GroupBy(spawnPoint => spawnPoint.LocationId)
            .Select(group => group.First())
            .ToList();

        // Do we have enough items in pool to fulfill requirement
        var tooManySpawnPointsRequested = desiredSpawnPointCount - chosenSpawnPoints.Count > 0;
        if (tooManySpawnPointsRequested)
        {
            if (_logger.IsLogEnabled(LogLevel.Debug))
            {
                _logger.Debug(
                    _serverLocalisationService.GetText(
                        "location-spawn_point_count_requested_vs_found",
                        new
                        {
                            requested = desiredSpawnPointCount + guaranteedLoosePoints.Count,
                            found = chosenSpawnPoints.Count,
                            mapName = locationName,
                        }
                    )
                );
            }
        }

        // Iterate over spawnPoints
        var seasonalEventActive = _seasonalEventService.SeasonalEventEnabled();
        var seasonalItemTplBlacklist = _seasonalEventService.GetInactiveSeasonalEventItems();
        foreach (var spawnPoint in chosenSpawnPoints)
        {
            // SpawnPoint is invalid, skip it
            if (spawnPoint.Template is null)
            {
                _logger.Warning(
                    _serverLocalisationService.GetText(
                        "location-missing_dynamic_template",
                        spawnPoint.LocationId
                    )
                );

                continue;
            }

            // Ensure no blacklisted lootable items are in pool
            spawnPoint.Template.Items = spawnPoint
                .Template.Items.Where(item =>
                    !_itemFilterService.IsLootableItemBlacklisted(item.Template)
                )
                .ToList();

            // Ensure no seasonal items are in pool if not in-season
            if (!seasonalEventActive)
            {
                spawnPoint.Template.Items = spawnPoint
                    .Template.Items.Where(item => !seasonalItemTplBlacklist.Contains(item.Template))
                    .ToList();
            }

            // Spawn point has no items after filtering, skip
            if (spawnPoint.Template.Items is null || !spawnPoint.Template.Items.Any())
            {
                if (_logger.IsLogEnabled(LogLevel.Debug))
                {
                    _logger.Debug(
                        _serverLocalisationService.GetText(
                            "location-spawnpoint_missing_items",
                            spawnPoint.Template.Id
                        )
                    );
                }

                continue;
            }

            // Get an array of allowed IDs after above filtering has occured
            var validComposedKeys = spawnPoint
                .Template.Items.Select(item => item.ComposedKey)
                .ToHashSet();

            // Construct container to hold above filtered items, letting us pick an item for the spot
            var itemArray = new ProbabilityObjectArray<string, double?>(_cloner);
            foreach (var itemDist in spawnPoint.ItemDistribution)
            {
                if (!validComposedKeys.Contains(itemDist.ComposedKey.Key))
                {
                    continue;
                }

                itemArray.Add(
                    new ProbabilityObject<string, double?>(
                        itemDist.ComposedKey.Key,
                        itemDist.RelativeProbability ?? 0,
                        null
                    )
                );
            }

            if (itemArray.Count == 0)
            {
                _logger.Warning(
                    _serverLocalisationService.GetText(
                        "location-loot_pool_is_empty_skipping",
                        spawnPoint.Template.Id
                    )
                );

                continue;
            }

            // Draw a random item from the spawn points possible items
            var chosenComposedKey = itemArray.Draw().FirstOrDefault();
            var chosenItem = spawnPoint.Template.Items.FirstOrDefault(item =>
                item.ComposedKey == chosenComposedKey
            );
            if (chosenItem is null)
            {
                _logger.Warning(
                    $"Unable to find item with composed key: {chosenComposedKey}, skipping spawn point: {spawnPoint.LocationId} "
                );
                continue;
            }

            var createItemResult = CreateDynamicLootItem(
                chosenItem,
                spawnPoint.Template.Items,
                staticAmmoDist
            );

            // If count reaches max, skip adding item to loot
            if (
                counterTrackerHelper.IncrementCount(
                    createItemResult.Items.FirstOrDefault().Template
                )
            )
            {
                continue;
            }

            // Root id can change when generating a weapon, ensure ids match
            spawnPoint.Template.Root = createItemResult.Items.FirstOrDefault().Id;

            // Convert the processed items into the correct output type
            var convertedItems = createItemResult.Items.Select(item => item.ToLootItem()).ToList();

            // Overwrite entire pool with chosen item
            spawnPoint.Template.Items = convertedItems;

            loot.Add(spawnPoint.Template);
        }

        return loot;
    }

    /// <summary>
    ///     Force items to be added to loot spawn points, primarily quest items
    /// </summary>
    /// <param name="forcedSpawnPoints">Forced loot locations that must be added</param>
    /// <param name="locationName">Name of map currently having force loot created for</param>
    /// <param name="staticAmmoDist"></param>
    /// <returns>Collection of spawn points with forced loot in them</returns>
    protected List<SpawnpointTemplate> GetForcedDynamicLoot(
        IEnumerable<Spawnpoint> forcedSpawnPoints,
        string locationName,
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist
    )
    {
        var result = new List<SpawnpointTemplate>();

        var seasonalEventActive = _seasonalEventService.SeasonalEventEnabled();
        var seasonalItemTplBlacklist = _seasonalEventService.GetInactiveSeasonalEventItems();

        foreach (var forcedLootLocation in forcedSpawnPoints)
        {
            var locationTemplateToAdd = forcedLootLocation.Template;
            var rootItem = locationTemplateToAdd.Items.FirstOrDefault();

            if (counterTrackerHelper.IncrementCount(rootItem.Template))
            {
                continue;
            }

            // Skip adding seasonal items when seasonal event is not active
            if (!seasonalEventActive && seasonalItemTplBlacklist.Contains(rootItem.Template))
            {
                continue;
            }

            var chosenItem = forcedLootLocation.Template.Items.FirstOrDefault(item =>
                item.Id == rootItem.Id
            );
            var createItemResult = CreateDynamicLootItem(
                chosenItem,
                forcedLootLocation.Template.Items,
                staticAmmoDist
            );

            // Update root ID with the above dynamically generated ID
            forcedLootLocation.Template.Root = createItemResult.Items.FirstOrDefault().Id;

            // Convert the processed items into the correct output type
            var convertedItems = createItemResult.Items.Select(item => item.ToLootItem()).ToList();

            forcedLootLocation.Template.Items = convertedItems;

            // Push forced location into array as long as it doesn't exist already
            var existingLocation = result.Any(spawnPoint =>
                spawnPoint.Id == locationTemplateToAdd.Id
            );
            if (!existingLocation)
            {
                result.Add(locationTemplateToAdd);
            }
            else
            {
                if (_logger.IsLogEnabled(LogLevel.Debug))
                {
                    _logger.Debug(
                        $"Attempted to add a forced loot location with Id: {locationTemplateToAdd.Id} to map {locationName} that already has that id in use, skipping"
                    );
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Create array of item (with child items) and return
    /// </summary>
    /// <param name="chosenItem"> Item we want to spawn in the position </param>
    /// <param name="lootItems"> Location loot Template </param>
    /// <param name="staticAmmoDist"> Ammo distributions </param>
    /// <returns> ContainerItem object </returns>
    protected ContainerItem CreateDynamicLootItem(
        SptLootItem chosenItem,
        IEnumerable<SptLootItem> lootItems,
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist
    )
    {
        var chosenTpl = chosenItem.Template;

        var itemDbTemplate = _itemHelper.GetItem(chosenTpl).Value;
        if (itemDbTemplate is null)
        {
            _logger.Error($"Item tpl: {chosenTpl} cannot be found in database");
        }

        // Item array to return
        List<Item> itemWithMods = [];

        // Money/Ammo - don't rely on items in spawnPoint.template.Items so we can randomise it ourselves
        if (_itemHelper.IsOfBaseclasses(chosenTpl, [BaseClasses.MONEY, BaseClasses.AMMO]))
        {
            var stackCount =
                itemDbTemplate.Properties.StackMaxSize == 1
                    ? 1
                    : _randomUtil.GetInt(
                        itemDbTemplate.Properties.StackMinRandom.Value,
                        itemDbTemplate.Properties.StackMaxRandom.Value
                    );

            itemWithMods.Add(
                new Item
                {
                    Id = new MongoId(),
                    Template = chosenTpl,
                    Upd = new Upd { StackObjectsCount = stackCount },
                }
            );
        }
        else if (_itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.AMMO_BOX))
        {
            // Fill with cartridges
            List<Item> ammoBoxItem = [new() { Id = new MongoId(), Template = chosenTpl }];
            _itemHelper.AddCartridgesToAmmoBox(ammoBoxItem, itemDbTemplate);
            itemWithMods.AddRange(ammoBoxItem);
        }
        else if (_itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.MAGAZINE))
        {
            // Create array with just magazine
            List<Item> magazineItem = [new() { Id = new MongoId(), Template = chosenTpl }];

            if (_randomUtil.GetChance100(_locationConfig.StaticMagazineLootHasAmmoChancePercent))
            // Add randomised amount of cartridges
            {
                _itemHelper.FillMagazineWithRandomCartridge(
                    magazineItem,
                    itemDbTemplate, // Magazine template
                    staticAmmoDist,
                    null,
                    _locationConfig.MinFillLooseMagazinePercent / 100d
                );
            }

            itemWithMods.AddRange(magazineItem);
        }
        else
        {
            // Also used by armors to get child mods
            // Get item + children and add into array we return
            var itemWithChildren = lootItems.GetItemWithChildren(chosenItem.Id);

            // Ensure all IDs are unique
            itemWithChildren = _cloner.Clone(itemWithChildren).ReplaceIDs().ToList();

            if (_locationConfig.TplsToStripChildItemsFrom.Contains(chosenItem.Template))
            // Strip children from parent before adding
            {
                itemWithChildren = [itemWithChildren.FirstOrDefault()];
            }

            itemWithMods.AddRange(itemWithChildren);
        }

        // Get inventory size of item
        var size = _itemHelper.GetItemSize(itemWithMods, itemWithMods.FirstOrDefault().Id);

        return new ContainerItem
        {
            Items = itemWithMods,
            Width = size.Width,
            Height = size.Height,
        };
    }

    // TODO: rewrite, BIG yikes
    protected ContainerItem? CreateStaticLootItem(
        string chosenTpl,
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist,
        string? parentId = null
    )
    {
        var itemTemplate = _itemHelper.GetItem(chosenTpl).Value;
        if (itemTemplate.Properties is null)
        {
            _logger.Error($"Unable to process item: {chosenTpl}. it lacks _props");

            return null;
        }

        var width = itemTemplate.Properties.Width;
        var height = itemTemplate.Properties.Height;
        List<Item> items = [new() { Id = new MongoId(), Template = chosenTpl }];
        var rootItem = items.FirstOrDefault();

        // Use passed in parentId as override for new item
        if (!string.IsNullOrEmpty(parentId))
        {
            rootItem.ParentId = parentId;
        }

        if (
            _itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.MONEY)
            || _itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.AMMO)
        )
        {
            // Edge case - some ammos e.g. flares or M406 grenades shouldn't be stacked
            var stackCount =
                itemTemplate.Properties.StackMaxSize == 1
                    ? 1
                    : _randomUtil.GetInt(
                        itemTemplate.Properties.StackMinRandom.Value,
                        itemTemplate.Properties.StackMaxRandom.Value
                    );

            rootItem.Upd = new Upd { StackObjectsCount = stackCount };
        }
        // No spawn point, use default template
        else if (_itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.WEAPON))
        {
            rootItem = CreateWeaponRootAndChildren(chosenTpl, staticAmmoDist, parentId, ref items);

            var size = _itemHelper.GetItemSize(items, rootItem.Id);
            width = size.Width;
            height = size.Height;
        }
        // No spawnPoint to fall back on, generate manually
        else if (_itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.AMMO_BOX))
        {
            _itemHelper.AddCartridgesToAmmoBox(items, itemTemplate);
        }
        else if (_itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.MAGAZINE))
        {
            if (_randomUtil.GetChance100(_locationConfig.MagazineLootHasAmmoChancePercent))
            {
                // Create array with just magazine
                GenerateStaticMagazineItem(staticAmmoDist, rootItem, itemTemplate, items);
            }
        }
        else if (_itemHelper.ArmorItemCanHoldMods(chosenTpl))
        {
            items = GetArmorItems(chosenTpl, rootItem, items, itemTemplate);
        }

        return new ContainerItem
        {
            Items = items,
            Width = width,
            Height = height,
        };
    }

    protected List<Item> GetArmorItems(
        string chosenTpl,
        Item? rootItem,
        List<Item> items,
        TemplateItem armorDbTemplate
    )
    {
        var defaultPreset = _presetHelper.GetDefaultPreset(chosenTpl);
        if (defaultPreset is not null)
        {
            var presetAndModsClone = _cloner.Clone(defaultPreset.Items).ReplaceIDs();
            presetAndModsClone.RemapRootItemId();

            // Use original items parentId otherwise item doesn't get added to container correctly
            presetAndModsClone.FirstOrDefault().ParentId = rootItem.ParentId;
            items = presetAndModsClone.ToList();
        }
        else
        {
            // We make base item in calling method, no need to do it here
            if (
                armorDbTemplate.Properties?.Slots is not null
                && armorDbTemplate.Properties.Slots.Any()
            )
            {
                items = _itemHelper.AddChildSlotItems(
                    items,
                    armorDbTemplate,
                    _locationConfig.EquipmentLootSettings.ModSpawnChancePercent
                );
            }
        }

        return items;
    }

    /// <summary>
    /// Attempt to find default preset for passed in tpl and construct a weapon with children.
    /// If no preset found, return chosenTpl as Item object
    /// </summary>
    /// <param name="chosenTpl">Tpl of item to get preset for</param>
    /// <param name="cartridgePool">Pool of cartridges to pick from</param>
    /// <param name="parentId"></param>
    /// <param name="items">Root item + children</param>
    /// <returns>Root Item</returns>
    protected Item? CreateWeaponRootAndChildren(
        string chosenTpl,
        Dictionary<string, List<StaticAmmoDetails>> cartridgePool,
        string? parentId,
        ref List<Item> items
    )
    {
        List<Item> children = [];

        // Look up a default preset for desired weapon tpl
        var defaultPreset = _cloner.Clone(_presetHelper.GetDefaultPreset(chosenTpl));
        if (defaultPreset?.Items is not null)
        {
            try
            {
                children = _itemHelper.ReparentItemAndChildren(
                    defaultPreset.Items.FirstOrDefault(),
                    defaultPreset.Items
                );
            }
            catch (Exception e)
            {
                // this item already broke it once without being reproducible tpl = "5839a40f24597726f856b511"; AKS-74UB Default
                // 5ea03f7400685063ec28bfa8 // ppsh default
                // 5ba26383d4351e00334c93d9 //mp7_devgru
                _logger.Error(
                    _serverLocalisationService.GetText(
                        "location-preset_not_found",
                        new
                        {
                            tpl = chosenTpl,
                            defaultId = defaultPreset.Id,
                            defaultName = defaultPreset.Name,
                            parentId,
                        }
                    )
                );
                _logger.Error(e.StackTrace);

                throw;
            }
        }
        else
        {
            // RSP30 (62178be9d0050232da3485d9/624c0b3340357b5f566e8766/6217726288ed9f0845317459) doesn't have any default presets and kills this code below as it has no children to re-parent
            if (_logger.IsLogEnabled(LogLevel.Debug))
            {
                _logger.Debug($"createStaticLootItem() No preset found for weapon: {chosenTpl}");
            }
        }

        var rootItem = items.FirstOrDefault();
        if (rootItem is null)
        {
            _logger.Error(
                _serverLocalisationService.GetText(
                    "location-missing_root_item",
                    new { tpl = chosenTpl, parentId }
                )
            );

            throw new Exception(
                _serverLocalisationService.GetText("location-critical_error_see_log")
            );
        }

        try
        {
            if (children?.Count > 0)
            {
                items = _itemHelper.ReparentItemAndChildren(rootItem, children);
            }
        }
        catch (Exception e)
        {
            _logger.Error(
                _serverLocalisationService.GetText(
                    "location-unable_to_reparent_item",
                    new { tpl = chosenTpl, parentId }
                )
            );
            _logger.Error(e.StackTrace);

            throw;
        }

        // Here we should use generalized BotGenerators functions e.g. fillExistingMagazines in the future since
        // it can handle revolver ammo (it's not restructured to be used here yet.)
        // General: Make a WeaponController for Ragfair preset stuff and the generating weapons and ammo stuff from
        // BotGenerator
        var magazine = items.FirstOrDefault(item => item.SlotId == "mod_magazine");
        // some weapon presets come without magazine; only fill the mag if it exists
        if (magazine is not null)
        {
            var magTemplate = _itemHelper.GetItem(magazine.Template).Value;
            var weaponTemplate = _itemHelper.GetItem(chosenTpl).Value;

            // Create array with just magazine
            var defaultWeapon = _itemHelper.GetItem(rootItem.Template).Value;
            List<Item> magazineWithCartridges = [magazine];
            _itemHelper.FillMagazineWithRandomCartridge(
                magazineWithCartridges,
                magTemplate,
                cartridgePool,
                weaponTemplate.Properties.AmmoCaliber,
                0.25,
                defaultWeapon.Properties.DefAmmo,
                defaultWeapon
            );

            // Replace existing magazine with above array
            items.Remove(magazine);
            items.AddRange(magazineWithCartridges);
        }

        return rootItem;
    }

    protected void GenerateStaticMagazineItem(
        Dictionary<string, List<StaticAmmoDetails>> staticAmmoDist,
        Item? rootItem,
        TemplateItem itemTemplate,
        List<Item> items
    )
    {
        List<Item> magazineWithCartridges = [rootItem];
        _itemHelper.FillMagazineWithRandomCartridge(
            magazineWithCartridges,
            itemTemplate,
            staticAmmoDist,
            null,
            _locationConfig.MinFillStaticMagazinePercent / 100d
        );

        // Replace existing magazine with above array
        items.Remove(rootItem);
        items.AddRange(magazineWithCartridges);
    }
}

public record ContainerGroupCount
{
    [JsonPropertyName("containerIdsWithProbability")]
    public Dictionary<string, double>? ContainerIdsWithProbability { get; set; }

    [JsonPropertyName("chosenCount")]
    public double? ChosenCount { get; set; }
}

public class ContainerItem
{
    [JsonPropertyName("items")]
    public List<Item>? Items { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }
}
