using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace SPTarkov.Server.Core.Services;

[Injectable(InjectionType.Singleton)]
public class LocationLifecycleService
{
    protected ProfileActivityService _profileActivityService;
    protected BotGenerationCacheService _botGenerationCacheService;
    protected BotLootCacheService _botLootCacheService;
    protected BotNameService _botNameService;
    protected ICloner _cloner;
    protected ConfigServer _configServer;
    protected DatabaseService _databaseService;
    protected HealthHelper _healthHelper;
    protected HideoutConfig _hideoutConfig;
    protected InRaidConfig _inRaidConfig;
    protected InRaidHelper _inRaidHelper;
    protected InsuranceService _insuranceService;
    protected ServerLocalisationService _serverLocalisationService;
    protected LocationConfig _locationConfig;
    protected LocationLootGenerator _locationLootGenerator;
    protected ISptLogger<LocationLifecycleService> _logger;
    protected LootGenerator _lootGenerator;
    protected MailSendService _mailSendService;
    protected MatchBotDetailsCacheService _matchBotDetailsCacheService;
    protected PlayerScavGenerator _playerScavGenerator;
    protected PmcChatResponseService _pmcChatResponseService;
    protected PmcConfig _pmcConfig;
    protected PmcWaveGenerator _pmcWaveGenerator;
    protected ProfileHelper _profileHelper;
    protected QuestHelper _questHelper;
    protected RagfairConfig _ragfairConfig;
    protected RaidTimeAdjustmentService _raidTimeAdjustmentService;
    protected RandomUtil _randomUtil;
    protected RewardHelper _rewardHelper;
    protected SaveServer _saveServer;
    protected TimeUtil _timeUtil;
    protected TraderConfig _traderConfig;
    protected TraderHelper _traderHelper;
    protected BtrDeliveryService _btrDeliveryService;
    private readonly CounterTrackerHelper _counterTrackerHelper;

    public LocationLifecycleService(
        ISptLogger<LocationLifecycleService> logger,
        RewardHelper rewardHelper,
        ConfigServer configServer,
        TimeUtil timeUtil,
        DatabaseService databaseService,
        ProfileHelper profileHelper,
        HashUtil hashUtil,
        ProfileActivityService profileActivityService,
        BotGenerationCacheService botGenerationCacheService,
        BotNameService botNameService,
        ICloner cloner,
        RaidTimeAdjustmentService raidTimeAdjustmentService,
        LocationLootGenerator locationLootGenerator,
        ServerLocalisationService localisationService,
        BotLootCacheService botLootCacheService,
        LootGenerator lootGenerator,
        MailSendService mailSendService,
        TraderHelper traderHelper,
        RandomUtil randomUtil,
        InRaidHelper inRaidHelper,
        PlayerScavGenerator playerScavGenerator,
        SaveServer saveServer,
        HealthHelper healthHelper,
        PmcChatResponseService pmcChatResponseService,
        PmcWaveGenerator pmcWaveGenerator,
        QuestHelper questHelper,
        InsuranceService insuranceService,
        MatchBotDetailsCacheService matchBotDetailsCacheService,
        BtrDeliveryService btrDeliveryService,
        CounterTrackerHelper counterTrackerHelper
    )
    {
        _logger = logger;
        _rewardHelper = rewardHelper;
        _configServer = configServer;
        _timeUtil = timeUtil;
        _databaseService = databaseService;
        _profileHelper = profileHelper;
        _profileActivityService = profileActivityService;
        _botGenerationCacheService = botGenerationCacheService;
        _botNameService = botNameService;
        _cloner = cloner;
        _raidTimeAdjustmentService = raidTimeAdjustmentService;
        _locationLootGenerator = locationLootGenerator;
        _serverLocalisationService = localisationService;
        _botLootCacheService = botLootCacheService;
        _lootGenerator = lootGenerator;
        _mailSendService = mailSendService;
        _traderHelper = traderHelper;
        _randomUtil = randomUtil;
        _inRaidHelper = inRaidHelper;
        _playerScavGenerator = playerScavGenerator;
        _saveServer = saveServer;
        _healthHelper = healthHelper;
        _pmcChatResponseService = pmcChatResponseService;
        _pmcWaveGenerator = pmcWaveGenerator;
        _questHelper = questHelper;
        _insuranceService = insuranceService;
        _matchBotDetailsCacheService = matchBotDetailsCacheService;
        _btrDeliveryService = btrDeliveryService;
        _counterTrackerHelper = counterTrackerHelper;

        _locationConfig = _configServer.GetConfig<LocationConfig>();
        _inRaidConfig = _configServer.GetConfig<InRaidConfig>();
        _traderConfig = _configServer.GetConfig<TraderConfig>();
        _ragfairConfig = _configServer.GetConfig<RagfairConfig>();
        _hideoutConfig = _configServer.GetConfig<HideoutConfig>();
        _pmcConfig = _configServer.GetConfig<PmcConfig>();
    }

    /// <summary>
    ///     Handle client/match/local/start
    /// </summary>
    public virtual StartLocalRaidResponseData StartLocalRaid(
        string sessionId,
        StartLocalRaidRequestData request
    )
    {
        _logger.Debug($"Starting: {request.Location}");

        var playerProfile = _profileHelper.GetFullProfile(sessionId);

        // Remove skill fatigue values
        ResetSkillPointsEarnedDuringRaid(
            string.Equals(request.PlayerSide, "pmc", StringComparison.OrdinalIgnoreCase)
                ? playerProfile.CharacterData.PmcData.Skills.Common
                : playerProfile.CharacterData.ScavData.Skills.Common
        );

        // Raid is starting, adjust run times to reduce server load while player is in raid
        _ragfairConfig.RunIntervalSeconds = _ragfairConfig.RunIntervalValues.InRaid;
        _hideoutConfig.RunIntervalSeconds = _hideoutConfig.RunIntervalValues.InRaid;

        var result = new StartLocalRaidResponseData
        {
            // PVE_OFFLINE_xxxxxxxx_27_06_2025_20_20_44
            ServerId = $"{request.Location}.{request.PlayerSide} {_timeUtil.GetTimeStamp()}", // Only used for metrics in client
            ServerSettings = _databaseService.GetLocationServices(), // TODO - is this per map or global?
            Profile = new ProfileInsuredItems
            {
                InsuredItems = playerProfile.CharacterData.PmcData.InsuredItems,
            },
            LocationLoot = GenerateLocationAndLoot(
                sessionId,
                request.Location,
                !request.ShouldSkipLootGeneration ?? true
            ),
            TransitionType = TransitionType.NONE,
            Transition = new Transition
            {
                TransitionType = TransitionType.NONE,
                TransitionRaidId = new MongoId(),
                TransitionCount = 0,
                VisitedLocations = [],
            },
            ExcludedBosses = [],
        };

        // Only has value when transitioning into map from previous one
        if (request.Transition is not null)
        // TODO - why doesn't the raid after transit have any transit data?
        {
            result.Transition = request.Transition;
        }

        // Get data stored at end of previous raid (if any)
        var transitionData = _profileActivityService
            .GetProfileActivityRaidData(sessionId)
            ?.LocationTransit;

        if (transitionData is not null)
        {
            _logger.Success($"Player: {sessionId} is in transit to {request.Location}");
            result.Transition.TransitionType = TransitionType.COMMON;
            result.Transition.TransitionRaidId = transitionData.TransitionRaidId;
            result.Transition.TransitionCount += 1;

            // Used by client to determine infil location - client adds the map player is transiting to later
            result.Transition.VisitedLocations.Add(transitionData.SptLastVisitedLocation);

            // Complete, clean up as no longer needed
            _profileActivityService.GetProfileActivityRaidData(sessionId).LocationTransit = null;
        }

        // Apply changes from pmcConfig to bot hostility values
        AdjustBotHostilitySettings(result.LocationLoot);

        AdjustExtracts(request.PlayerSide, request.Location, result.LocationLoot);

        // Clear bot cache ready for bot generation call that occurs after this
        _botGenerationCacheService.ClearStoredBots();
        _botNameService.ClearNameCache();

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

        return result;
    }

    /// <summary>
    ///     Replace map exits with scav exits when player is scavving
    /// </summary>
    /// <param name="playerSide"> Players side (savage/usec/bear) </param>
    /// <param name="location"> ID of map being loaded </param>
    /// <param name="locationData"> Maps location base data </param>
    protected void AdjustExtracts(string playerSide, string location, LocationBase locationData)
    {
        var playerIsScav = string.Equals(playerSide, "savage", StringComparison.OrdinalIgnoreCase);
        if (!playerIsScav)
        {
            return;
        }

        // Get relevant extract data for map
        var mapExtracts = _databaseService.GetLocation(location)?.AllExtracts;
        if (mapExtracts is null)
        {
            _logger.Warning($"Unable to find map: {location} extract data, no adjustments made");

            return;
        }

        // Find only scav extracts and overwrite existing exits with them
        var scavExtracts = mapExtracts
            .Where(extract =>
                string.Equals(extract.Side, "scav", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();
        if (scavExtracts.Count > 0)
        // Scav extracts found, use them
        {
            locationData.Exits.AddRange(scavExtracts);
        }
    }

    /// <summary>
    ///     Adjust the bot hostility values prior to entering a raid
    /// </summary>
    /// <param name="location"> Map to adjust values of </param>
    protected void AdjustBotHostilitySettings(LocationBase location)
    {
        foreach (var botId in _pmcConfig.HostilitySettings)
        {
            var configHostilityChanges = _pmcConfig.HostilitySettings[botId.Key];
            var locationBotHostilityDetails =
                location.BotLocationModifier.AdditionalHostilitySettings.FirstOrDefault(
                    botSettings =>
                        string.Equals(
                            botSettings.BotRole,
                            botId.Key,
                            StringComparison.OrdinalIgnoreCase
                        )
                );

            // No matching bot in config, skip
            if (locationBotHostilityDetails is null)
            {
                _logger.Warning(
                    $"No bot: {botId} hostility values found on: {location.Id}, can only edit existing. Skipping"
                );

                continue;
            }

            // Add new permanent enemies if they don't already exist
            if (configHostilityChanges.AdditionalEnemyTypes is not null)
            {
                foreach (var enemyTypeToAdd in configHostilityChanges.AdditionalEnemyTypes)
                {
                    if (!locationBotHostilityDetails.AlwaysEnemies.Contains(enemyTypeToAdd))
                    {
                        locationBotHostilityDetails.AlwaysEnemies.Add(enemyTypeToAdd);
                    }
                }
            }

            // Add/edit chance settings
            if (configHostilityChanges.ChancedEnemies is not null)
            {
                locationBotHostilityDetails.ChancedEnemies = [];
                foreach (var chanceDetailsToApply in configHostilityChanges.ChancedEnemies)
                {
                    var locationBotDetails =
                        locationBotHostilityDetails.ChancedEnemies.FirstOrDefault(botChance =>
                            botChance.Role == chanceDetailsToApply.Role
                        );
                    if (locationBotDetails is not null)
                    // Existing
                    {
                        locationBotDetails.EnemyChance = chanceDetailsToApply.EnemyChance;
                    }
                    else
                    // Add new
                    {
                        locationBotHostilityDetails.ChancedEnemies.Add(chanceDetailsToApply);
                    }
                }
            }

            // Add new permanent friends if they don't already exist
            if (configHostilityChanges.AdditionalFriendlyTypes is not null)
            {
                locationBotHostilityDetails.AlwaysFriends = [];
                foreach (var friendlyTypeToAdd in configHostilityChanges.AdditionalFriendlyTypes)
                {
                    if (!locationBotHostilityDetails.AlwaysFriends.Contains(friendlyTypeToAdd))
                    {
                        locationBotHostilityDetails.AlwaysFriends.Add(friendlyTypeToAdd);
                    }
                }
            }

            // Adjust vs bear hostility chance
            if (configHostilityChanges.BearEnemyChance is not null)
            {
                locationBotHostilityDetails.BearEnemyChance =
                    configHostilityChanges.BearEnemyChance;
            }

            // Adjust vs usec hostility chance
            if (configHostilityChanges.UsecEnemyChance is not null)
            {
                locationBotHostilityDetails.UsecEnemyChance =
                    configHostilityChanges.UsecEnemyChance;
            }

            // Adjust vs savage hostility chance
            if (configHostilityChanges.SavageEnemyChance is not null)
            {
                locationBotHostilityDetails.SavageEnemyChance =
                    configHostilityChanges.SavageEnemyChance;
            }

            // Adjust vs scav hostility behaviour
            if (configHostilityChanges.SavagePlayerBehaviour is not null)
            {
                locationBotHostilityDetails.SavagePlayerBehaviour =
                    configHostilityChanges.SavagePlayerBehaviour;
            }
        }
    }

    /// <summary>
    ///     Generate a maps base location (cloned) and loot
    /// </summary>
    /// <param name="sessionId"> Session/Player id </param>
    /// <param name="name"> Map name </param>
    /// <param name="generateLoot"> OPTIONAL - Should loot be generated for the map before being returned </param>
    /// <returns>LocationBase with loot</returns>
    public virtual LocationBase GenerateLocationAndLoot(
        string sessionId,
        string name,
        bool generateLoot = true
    )
    {
        var location = _databaseService.GetLocation(name);
        var locationBaseClone = _cloner.Clone(location.Base);

        // Update datetime property to now
        locationBaseClone.UnixDateTime = _timeUtil.GetTimeStamp();

        // Don't generate loot for hideout
        if (string.Equals(name, "hideout", StringComparison.OrdinalIgnoreCase))
        {
            return locationBaseClone;
        }

        // Only requested base data, not loot
        if (!generateLoot)
        {
            return locationBaseClone;
        }

        // Add custom PMCs to map every time its run
        _pmcWaveGenerator.ApplyWaveChangesToMap(locationBaseClone);

        // Adjust raid values based raid type (e.g. Scav or PMC)
        LocationConfig? locationConfigClone = null;
        var raidAdjustments = _profileActivityService
            .GetProfileActivityRaidData(sessionId)
            ?.RaidAdjustments;
        if (raidAdjustments is not null)
        {
            locationConfigClone = _cloner.Clone(_locationConfig); // Clone values so they can be used to reset originals later
            _raidTimeAdjustmentService.MakeAdjustmentsToMap(raidAdjustments, locationBaseClone);
        }

        // Generate loot for location
        locationBaseClone.Loot = _locationLootGenerator.GenerateLocationLoot(name);

        // Reset loot multipliers back to original values
        if (raidAdjustments is not null && locationConfigClone is not null)
        {
            _logger.Debug("Resetting loot multipliers back to their original values");
            _locationConfig.StaticLootMultiplier = locationConfigClone.StaticLootMultiplier;
            _locationConfig.LooseLootMultiplier = locationConfigClone.LooseLootMultiplier;

            _profileActivityService.GetProfileActivityRaidData(sessionId).RaidAdjustments = null;
        }

        return locationBaseClone;
    }

    /// <summary>
    ///     Handle client/match/local/end
    /// </summary>
    public virtual void EndLocalRaid(string sessionId, EndLocalRaidRequestData request)
    {
        // Clear bot loot cache
        _botLootCacheService.ClearCache();

        var fullProfile = _profileHelper.GetFullProfile(sessionId);
        var pmcProfile = fullProfile.CharacterData.PmcData;
        var scavProfile = fullProfile.CharacterData.ScavData;

        if (_logger.IsLogEnabled(LogLevel.Debug))
        {
            _logger.Debug($"Raid: {request.ServerId} outcome: {request.Results.Result}");
        }

        // Reset flea interval time to out-of-raid value
        _ragfairConfig.RunIntervalSeconds = _ragfairConfig.RunIntervalValues.OutOfRaid;
        _hideoutConfig.RunIntervalSeconds = _hideoutConfig.RunIntervalValues.OutOfRaid;

        // ServerId has various info stored in it, delimited by a period
        var serverDetails = request.ServerId.Split(".");

        var locationName = serverDetails[0].ToLowerInvariant();
        var isPmc = serverDetails[1].ToLowerInvariant().Contains("pmc");
        var isDead = IsPlayerDead(request.Results);
        var isTransfer = IsMapToMapTransfer(request.Results);
        var isSurvived = IsPlayerSurvived(request.Results);

        // Handle items transferred via BTR or transit to player mailbox
        _btrDeliveryService.HandleItemTransferEvent(sessionId, request);

        // Player is moving between maps
        if (isTransfer && request.LocationTransit is not null)
        {
            // Manually store the map player just left
            request.LocationTransit.SptLastVisitedLocation = locationName;
            // TODO - Persist each players last visited location history over multiple transits, e.g. using InMemoryCacheService, need to take care to not let data get stored forever
            // Store transfer data for later use in `startLocalRaid()` when next raid starts
            request.LocationTransit.SptExitName = request.Results.ExitName;
            _profileActivityService.GetProfileActivityRaidData(sessionId).LocationTransit =
                request.LocationTransit;
        }

        if (!isPmc)
        {
            HandlePostRaidPlayerScav(
                sessionId,
                pmcProfile,
                scavProfile,
                isDead,
                isTransfer,
                isSurvived,
                request
            );

            return;
        }

        HandlePostRaidPmc(
            sessionId,
            fullProfile,
            scavProfile,
            isDead,
            isSurvived,
            isTransfer,
            request,
            locationName
        );

        // Handle car extracts
        if (ExtractWasViaCar(request.Results.ExitName))
        {
            HandleCarExtract(request.Results.ExitName, pmcProfile, sessionId);
        }

        // Handle coop exit
        if (
            request.Results.ExitName is not null
            && ExtractTakenWasCoop(request.Results.ExitName)
            && _traderConfig.Fence.CoopExtractGift.SendGift
        )
        {
            HandleCoopExtract(sessionId, pmcProfile, request.Results.ExitName);
            SendCoopTakenFenceMessage(sessionId);
        }
    }

    /// <summary>
    /// After taking a COOP extract, send player a gift via mail
    /// </summary>
    /// <param name="sessionId">Player/Session id</param>
    protected void SendCoopTakenFenceMessage(string sessionId)
    {
        // Generate randomised reward for taking coop extract
        var loot = _lootGenerator.CreateRandomLoot(_traderConfig.Fence.CoopExtractGift);

        var parentId = new MongoId();
        foreach (var itemAndChildren in loot)
        {
            // Set all root items parent to new id
            itemAndChildren.FirstOrDefault().ParentId = parentId;
        }

        // Flatten
        List<Item> mailableLoot = [.. loot.SelectMany(x => x)];

        // Send message from fence giving player reward generated above
        _mailSendService.SendLocalisedNpcMessageToPlayer(
            sessionId,
            Traders.FENCE,
            MessageType.MessageWithItems,
            _randomUtil.GetArrayValue(_traderConfig.Fence.CoopExtractGift.MessageLocaleIds),
            mailableLoot,
            _timeUtil.GetHoursAsSeconds(_traderConfig.Fence.CoopExtractGift.GiftExpiryHours)
        );
    }

    /// <summary>
    ///     Was extract by car
    /// </summary>
    /// <param name="extractName"> Name of extract </param>
    /// <returns> True if extract was by car </returns>
    protected bool ExtractWasViaCar(string extractName)
    {
        // exit name is undefined on death
        if (string.IsNullOrEmpty(extractName))
        {
            return false;
        }

        if (extractName.ToLowerInvariant().Contains("v-ex"))
        {
            return true;
        }

        return _inRaidConfig.CarExtracts.Contains(extractName.Trim());
    }

    /// <summary>
    ///     Handle when a player extracts using a car - Add rep to fence
    /// </summary>
    /// <param name="extractName"> Name of the extract used </param>
    /// <param name="pmcData"> Player profile </param>
    /// <param name="sessionId"> Session ID </param>
    protected void HandleCarExtract(string extractName, PmcData pmcData, string sessionId)
    {
        pmcData.CarExtractCounts?.TryAdd(extractName, 0);

        // Increment extract count value
        pmcData.CarExtractCounts[extractName] += 1;

        var newFenceStanding = GetFenceStandingAfterExtract(
            pmcData,
            _inRaidConfig.CarExtractBaseStandingGain,
            pmcData.CarExtractCounts[extractName]
        );

        var fenceId = Traders.FENCE;
        pmcData.TradersInfo[fenceId].Standing = newFenceStanding;

        // Check if new standing has leveled up trader
        _traderHelper.LevelUp(fenceId, pmcData);
        pmcData.TradersInfo[fenceId].LoyaltyLevel = Math.Max(
            (int)pmcData.TradersInfo[fenceId].LoyaltyLevel,
            1
        );

        _logger.Debug(
            $"Car extract: {extractName} used, total times taken: {pmcData.CarExtractCounts[extractName]}"
        );

        // Copy updated fence rep values into scav profile to ensure consistency
        var scavData = _profileHelper.GetScavProfile(sessionId);
        scavData.TradersInfo[fenceId].Standing = pmcData.TradersInfo[fenceId].Standing;
        scavData.TradersInfo[fenceId].LoyaltyLevel = pmcData.TradersInfo[fenceId].LoyaltyLevel;
    }

    /// <summary>
    ///     Handle when a player extracts using a coop extract - add rep to fence
    /// </summary>
    /// <param name="sessionId"> Session/player id </param>
    /// <param name="pmcData"> Player profile </param>
    /// <param name="extractName"> Name of extract taken </param>
    protected void HandleCoopExtract(string sessionId, PmcData pmcData, string extractName)
    {
        pmcData.CoopExtractCounts?.TryAdd(extractName, 0);

        pmcData.CoopExtractCounts[extractName] += 1;

        var newFenceStanding = GetFenceStandingAfterExtract(
            pmcData,
            _inRaidConfig.CoopExtractBaseStandingGain,
            pmcData.CoopExtractCounts[extractName]
        );

        var fenceId = Traders.FENCE;
        pmcData.TradersInfo[fenceId].Standing = newFenceStanding;

        // Check if new standing has leveled up trader
        _traderHelper.LevelUp(fenceId, pmcData);
        pmcData.TradersInfo[fenceId].LoyaltyLevel = Math.Max(
            (int)pmcData.TradersInfo[fenceId].LoyaltyLevel,
            1
        );

        _logger.Debug($"COOP extract: {extractName} used");

        // Copy updated fence rep values into scav profile to ensure consistency
        var scavData = _profileHelper.GetScavProfile(sessionId);
        scavData.TradersInfo[fenceId].Standing = pmcData.TradersInfo[fenceId].Standing;
        scavData.TradersInfo[fenceId].LoyaltyLevel = pmcData.TradersInfo[fenceId].LoyaltyLevel;
    }

    /// <summary>
    ///     Get the fence rep gain from using a car or coop extract
    /// </summary>
    /// <param name="pmcData"> Profile </param>
    /// <param name="baseGain"> Amount gained for the first extract </param>
    /// <param name="extractCount"> Number of times extract was taken </param>
    /// <returns> Fence standing after taking extract </returns>
    protected double GetFenceStandingAfterExtract(
        PmcData pmcData,
        double baseGain,
        double extractCount
    )
    {
        var fenceId = Traders.FENCE;
        var fenceStanding = pmcData.TradersInfo[fenceId].Standing;

        // get standing after taking extract x times, x.xx format, gain from extract can be no smaller than 0.01
        fenceStanding += Math.Max(baseGain / extractCount, 0.01);

        // Ensure fence loyalty level is not above/below the range -7 to 15
        var newFenceStanding = Math.Min(Math.Max((double)fenceStanding, -7), 15);
        _logger.Debug(
            $"Old vs new fence standing: {pmcData.TradersInfo[fenceId].Standing}, {newFenceStanding}"
        );

        return Math.Round(newFenceStanding, 2);
    }

    /// <summary>
    ///     Did player take a COOP extract
    /// </summary>
    /// <param name="extractName"> Name of extract player took </param>
    /// <returns> True if coop extract </returns>
    protected bool ExtractTakenWasCoop(string extractName)
    {
        // No extract name, not a coop extract
        if (extractName is null)
        {
            return false;
        }

        return _inRaidConfig.CoopExtracts.Contains(extractName.Trim());
    }

    /// <summary>
    /// Perform post-raid profile changes
    /// </summary>
    /// <param name="sessionId">Player id</param>
    /// <param name="pmcProfile">Players PMC profile</param>
    /// <param name="scavProfile">Players scav profile</param>
    /// <param name="isDead">Did player die</param>
    /// <param name="isTransfer">Did player transfer to new map</param>
    /// <param name="isSurvived">DId player get 'survived' exit status</param>
    /// <param name="request">End raid request</param>
    protected void HandlePostRaidPlayerScav(
        string sessionId,
        PmcData pmcProfile,
        PmcData scavProfile,
        bool isDead,
        bool isTransfer,
        bool isSurvived,
        EndLocalRaidRequestData request
    )
    {
        var postRaidProfile = request.Results.Profile;

        if (isTransfer || request.Results.Result == ExitStatus.RUNNER)
        {
            // Transfer over hp and effects - not necessary for runthroughs, but it causes no issues
            scavProfile.Health = postRaidProfile.Health;

            // Adjust limb hp and effects while transiting
            UpdateLimbValuesAfterTransit(scavProfile.Health);

            // We want scav inventory to persist into next raid when pscav is moving between maps
            // Also adjust FiR status when exit was runthrough
            _inRaidHelper.SetInventory(
                sessionId,
                scavProfile,
                postRaidProfile,
                isSurvived,
                isTransfer
            );
        }

        scavProfile.Info.Level = postRaidProfile.Info.Level;
        scavProfile.Skills = postRaidProfile.Skills;
        scavProfile.Stats = postRaidProfile.Stats;
        scavProfile.Encyclopedia = postRaidProfile.Encyclopedia;
        scavProfile.TaskConditionCounters = postRaidProfile.TaskConditionCounters;
        scavProfile.SurvivorClass = postRaidProfile.SurvivorClass;

        // Scavs don't have achievements, but copy anyway
        scavProfile.Achievements = postRaidProfile.Achievements;

        scavProfile.Info.Experience = postRaidProfile.Info.Experience;

        // Must occur after experience is set and stats copied over
        scavProfile.Stats.Eft.TotalSessionExperience = 0;

        ApplyTraderStandingAdjustments(scavProfile.TradersInfo, postRaidProfile.TradersInfo);

        // Clamp fence standing within -7 to 15 range
        var fenceMax = _traderConfig.Fence.PlayerRepMax; // 15
        var fenceMin = _traderConfig.Fence.PlayerRepMin; //-7
        if (!postRaidProfile.TradersInfo.TryGetValue(Traders.FENCE, out var postRaidFenceData))
        {
            _logger.Error($"post raid fence data not found for: {sessionId}");
        }

        scavProfile.TradersInfo[Traders.FENCE].Standing = Math.Min(
            Math.Max(postRaidFenceData.Standing.Value, fenceMin),
            fenceMax
        );

        // Successful extract as scav, give some rep
        if (
            IsPlayerSurvived(request.Results)
            && scavProfile.TradersInfo[Traders.FENCE].Standing < fenceMax
        )
        {
            scavProfile.TradersInfo[Traders.FENCE].Standing +=
                _inRaidConfig.ScavExtractStandingGain;
        }

        // Copy scav fence values to PMC profile
        pmcProfile.TradersInfo[Traders.FENCE] = scavProfile.TradersInfo[Traders.FENCE];

        if (scavProfile.ProfileHasConditionCounters())
        // Scav quest progress needs to be moved to pmc so player can see it in menu / hand them in
        {
            MigrateScavQuestProgressToPmcProfile(scavProfile, pmcProfile);
        }

        // Must occur after encyclopedia updated
        MergePmcAndScavEncyclopedias(scavProfile, pmcProfile);

        // Scav died, regen scav loadout and reset timer
        if (isDead)
        {
            _playerScavGenerator.Generate(sessionId);
        }

        // Update last played property
        pmcProfile.Info.LastTimePlayedAsSavage = _timeUtil.GetTimeStamp();

        // Force a profile save
        _saveServer.SaveProfileAsync(sessionId);
    }

    /// <summary>
    ///     Scav quest progress isn't transferred automatically from scav to pmc, we do this manually
    /// </summary>
    /// <param name="scavProfile"> Scav profile with quest progress post-raid </param>
    /// <param name="pmcProfile"> Server pmc profile to copy scav quest progress into </param>
    protected void MigrateScavQuestProgressToPmcProfile(PmcData scavProfile, PmcData pmcProfile)
    {
        foreach (var scavQuest in scavProfile.Quests)
        {
            var pmcQuest = pmcProfile.Quests.FirstOrDefault(quest => quest.QId == scavQuest.QId);
            if (pmcQuest is null)
            {
                _logger.Warning(
                    _serverLocalisationService.GetText(
                        "inraid-unable_to_migrate_pmc_quest_not_found_in_profile",
                        scavQuest.QId
                    )
                );
                continue;
            }

            // Get counters related to scav quest
            var matchingCounters = scavProfile.TaskConditionCounters.Where(counter =>
                counter.Value.SourceId == scavQuest.QId
            );

            if (matchingCounters is null)
            {
                continue;
            }

            // insert scav quest counters into pmc profile
            foreach (var counter in matchingCounters)
            {
                pmcProfile.TaskConditionCounters[counter.Value.Id] = counter.Value;
            }

            // Find Matching PMC Quest
            // Update Status and StatusTimer properties
            pmcQuest.Status = scavQuest.Status;
            pmcQuest.StatusTimers = scavQuest.StatusTimers;
        }
    }

    /// <summary>
    /// Slightly fix broken limbs and remove effects
    /// </summary>
    /// <param name="profileHealth">Profile health data to adjust</param>
    protected void UpdateLimbValuesAfterTransit(BotBaseHealth? profileHealth)
    {
        var transitSettings = _locationConfig.TransitSettings;
        if (transitSettings == null)
        {
            _logger.Warning("Unable to find: _locationConfig.TransitSettings");

            return;
        }

        // Check each body part
        foreach (var (_, hpValues) in profileHealth.BodyParts)
        {
            if (
                transitSettings.AdjustLimbHealthPoints.GetValueOrDefault()
                && hpValues.Health.Minimum <= 0
            )
            {
                // Limb has been destroyed, reset
                hpValues.Health.Current = _randomUtil.GetPercentOfValue(
                    transitSettings.LimbHealPercent.GetValueOrDefault(30),
                    hpValues.Health.Maximum.Value
                );
            }

            if (!(hpValues.Effects?.Count > 0))
            {
                // No effects on limb, skip
                continue;
            }

            // Limb has effects, check for blacklisted values and remove
            var keysToRemove = hpValues
                .Effects.Keys.Where(key => transitSettings.EffectsToRemove.Contains(key))
                .ToHashSet();

            foreach (var key in keysToRemove)
            {
                hpValues.Effects.Remove(key);
            }
        }
    }

    /// <summary>
    ///     Handles PMC Profile after the raid
    /// </summary>
    /// <param name="sessionId"> Player id </param>
    /// <param name="fullServerProfile"> Pmc profile from server</param>
    /// <param name="scavProfile"> Scav profile </param>
    /// <param name="isDead"> Player died/got left behind in raid </param>
    /// <param name="isSurvived"> Not same as opposite of `isDead`, specific status </param>
    /// <param name="isTransfer"> Player transferred to another map </param>
    /// <param name="request"> Client request data </param>
    /// <param name="locationName"> Current finished Raid location </param>
    protected void HandlePostRaidPmc(
        string sessionId,
        SptProfile fullServerProfile,
        PmcData scavProfile,
        bool isDead,
        bool isSurvived,
        bool isTransfer,
        EndLocalRaidRequestData request,
        string locationName
    )
    {
        var serverPmcProfile = fullServerProfile.CharacterData.PmcData;
        var postRaidProfile = request.Results.Profile;
        var preRaidProfileQuestDataClone = _cloner.Clone(serverPmcProfile.Quests);

        // MUST occur BEFORE inventory actions (setInventory()) occur
        // Player died, get quest items they lost for use later
        var lostQuestItems = postRaidProfile.GetQuestItemsInProfile();

        // Update inventory
        _inRaidHelper.SetInventory(
            sessionId,
            serverPmcProfile,
            postRaidProfile,
            isSurvived,
            isTransfer
        );

        serverPmcProfile.Info.Level = postRaidProfile.Info.Level;
        serverPmcProfile.Skills = postRaidProfile.Skills;
        serverPmcProfile.Stats.Eft = postRaidProfile.Stats.Eft;
        serverPmcProfile.Encyclopedia = postRaidProfile.Encyclopedia;
        serverPmcProfile.TaskConditionCounters = postRaidProfile.TaskConditionCounters;
        serverPmcProfile.SurvivorClass = postRaidProfile.SurvivorClass;

        // MUST occur prior to profile achievements being overwritten by post-raid achievements
        ProcessAchievementRewards(fullServerProfile, postRaidProfile.Achievements);

        serverPmcProfile.Achievements = postRaidProfile.Achievements;
        serverPmcProfile.Quests = ProcessPostRaidQuests(postRaidProfile.Quests);

        // Handle edge case - must occur AFTER processPostRaidQuests()
        LightkeeperQuestWorkaround(
            sessionId,
            postRaidProfile.Quests,
            preRaidProfileQuestDataClone,
            serverPmcProfile
        );

        serverPmcProfile.WishList = postRaidProfile.WishList;

        serverPmcProfile.Info.Experience = postRaidProfile.Info.Experience;

        ApplyTraderStandingAdjustments(serverPmcProfile.TradersInfo, postRaidProfile.TradersInfo);

        // Must occur AFTER experience is set and stats copied over
        serverPmcProfile.Stats.Eft.TotalSessionExperience = 0;

        var fenceId = Traders.FENCE;

        // Clamp fence standing
        var currentFenceStanding = postRaidProfile.TradersInfo[fenceId].Standing;
        serverPmcProfile.TradersInfo[fenceId].Standing = Math.Min(
            Math.Max((double)currentFenceStanding, -7),
            15
        ); // Ensure it stays between -7 and 15

        // Copy fence values to Scav
        scavProfile.TradersInfo[fenceId] = serverPmcProfile.TradersInfo[fenceId];

        // MUST occur AFTER encyclopedia updated
        MergePmcAndScavEncyclopedias(serverPmcProfile, scavProfile);

        // Handle temp, hydration, limb hp/effects
        _healthHelper.ApplyHealthChangesToProfile(
            sessionId,
            serverPmcProfile,
            postRaidProfile.Health,
            isDead
        );

        if (isTransfer)
        {
            // Adjust limb hp and effects while transiting
            UpdateLimbValuesAfterTransit(serverPmcProfile.Health);
        }

        // This must occur _BEFORE_ `deleteInventory`, as that method clears insured items
        HandleInsuredItemLostEvent(sessionId, serverPmcProfile, request, locationName);

        if (isDead)
        {
            if (lostQuestItems.Count > 0)
            // MUST occur AFTER quests have post raid quest data has been merged "processPostRaidQuests()"
            // Player is dead + had quest items, check and fix any broken find item quests
            {
                CheckForAndFixPickupQuestsAfterDeath(
                    sessionId,
                    lostQuestItems,
                    serverPmcProfile.Quests
                );
            }

            if (postRaidProfile.Stats.Eft.Aggressor is not null)
            {
                // get the aggressor ID from the client request body
                postRaidProfile.Stats.Eft.Aggressor.ProfileId = request.Results.KillerId;
                _pmcChatResponseService.SendKillerResponse(
                    sessionId,
                    serverPmcProfile,
                    postRaidProfile.Stats.Eft.Aggressor
                );
            }

            _inRaidHelper.DeleteInventory(serverPmcProfile, sessionId);

            _inRaidHelper.RemoveFiRStatusFromItemsInContainer(
                sessionId,
                serverPmcProfile,
                "SecuredContainer"
            );
        }

        // Must occur AFTER killer messages have been sent
        _matchBotDetailsCacheService.ClearCache();

        var roles = new List<string> { "pmcbear", "pmcusec" };

        var victims = postRaidProfile
            .Stats.Eft.Victims.Where(victim => roles.Contains(victim.Role.ToLowerInvariant()))
            .ToList();
        if (victims?.Count > 0)
        // Player killed PMCs, send some mail responses to them
        {
            _pmcChatResponseService.SendVictimResponse(sessionId, victims, serverPmcProfile);
        }
    }

    /// <summary>
    ///     On death Quest items are lost, the client does not clean up completed conditions for picking up those quest items,
    ///     If the completed conditions remain in the profile the player is unable to pick the item up again
    /// </summary>
    /// <param name="sessionId"> Session ID </param>
    /// <param name="lostQuestItems"> Quest items lost on player death </param>
    /// <param name="profileQuests"> Quest status data from player profile </param>
    protected void CheckForAndFixPickupQuestsAfterDeath(
        string sessionId,
        List<Item> lostQuestItems,
        List<QuestStatus> profileQuests
    )
    {
        // Exclude completed quests
        var activeQuestIdsInProfile = profileQuests
            .Where(quest =>
                quest.Status
                    is not QuestStatusEnum.AvailableForStart
                        and not QuestStatusEnum.Success
            )
            .Select(status => status.QId);

        // Get db details of quests we found above
        var questDb = _databaseService
            .GetQuests()
            .Values.Where(quest => activeQuestIdsInProfile.Contains(quest.Id));

        foreach (var lostItem in lostQuestItems)
        {
            var matchingConditionId = string.Empty;
            // Find a quest that has a FindItem condition that has the list items tpl as a target
            var matchingQuests = questDb
                .Where(quest =>
                {
                    var matchingCondition = quest.Conditions.AvailableForFinish.FirstOrDefault(
                        questCondition =>
                            questCondition.ConditionType == "FindItem"
                            && (
                                questCondition.Target.IsList
                                    ? questCondition.Target.List
                                    : [questCondition.Target.Item]
                            ).Contains(lostItem.Template)
                    );
                    if (matchingCondition is null)
                    // Quest doesnt have a matching condition
                    {
                        return false;
                    }

                    // We found a condition, save id for later
                    matchingConditionId = matchingCondition.Id;
                    return true;
                })
                .ToList();

            // Fail if multiple were found
            if (matchingQuests.Count != 1)
            {
                _logger.Error(
                    $"Unable to fix quest item: {lostItem}, {matchingQuests.Count} matching quests found, expected 1"
                );

                continue;
            }

            var matchingQuest = matchingQuests[0];
            // We have a match, remove the condition id from profile to reset progress and let player pick item up again
            var profileQuestToUpdate = profileQuests.FirstOrDefault(questStatus =>
                questStatus.QId == matchingQuest.Id
            );
            if (profileQuestToUpdate is null)
            // Profile doesnt have a matching quest
            {
                continue;
            }

            // Filter out the matching condition we found
            profileQuestToUpdate.CompletedConditions = profileQuestToUpdate
                .CompletedConditions.Where(conditionId => conditionId != matchingConditionId)
                .ToList();
        }
    }

    /// <summary>
    ///     In 0.15 Lightkeeper quests do not give rewards in PvE, this issue also occurs in spt.
    ///     We check for newly completed Lk quests and run them through the servers `CompleteQuest` process.
    ///     This rewards players with items + craft unlocks + new trader assorts.
    /// </summary>
    /// <param name="sessionId"> Session ID </param>
    /// <param name="postRaidQuests"> Quest statuses post-raid </param>
    /// <param name="preRaidQuests"> Quest statuses pre-raid </param>
    /// <param name="pmcProfile"> Players profile </param>
    protected void LightkeeperQuestWorkaround(
        string sessionId,
        List<QuestStatus> postRaidQuests,
        List<QuestStatus> preRaidQuests,
        PmcData pmcProfile
    )
    {
        // LK quests that were not completed before raid but now are
        var newlyCompletedLightkeeperQuests = postRaidQuests
            .Where(postRaidQuest =>
                postRaidQuest.Status == QuestStatusEnum.Success
                && // Quest is complete
                preRaidQuests.Any(preRaidQuest =>
                    preRaidQuest.QId == postRaidQuest.QId
                    && // Get matching pre-raid quest
                    preRaidQuest.Status != QuestStatusEnum.Success
                )
                && // Completed quest was not completed before raid started
                _databaseService.GetQuests().TryGetValue(postRaidQuest.QId, out var quest)
                && quest?.TraderId == Traders.LIGHTHOUSEKEEPER
            ) // Quest is from LK
            .ToList();

        // Run server complete quest process to ensure player gets rewards
        foreach (var questToComplete in newlyCompletedLightkeeperQuests)
        {
            _questHelper.CompleteQuest(
                pmcProfile,
                new CompleteQuestRequestData
                {
                    Action = "CompleteQuest",
                    QuestId = questToComplete.QId,
                    RemoveExcessItems = false,
                },
                sessionId
            );
        }
    }

    /// <summary>
    ///     Convert post-raid quests into correct format.
    ///     Quest status comes back as a string version of the enum `Success`, not the expected value of 1.
    /// </summary>
    /// <param name="questsToProcess"> Quests data from client </param>
    /// <returns> List of adjusted QuestStatus post-raid </returns>
    protected List<QuestStatus> ProcessPostRaidQuests(List<QuestStatus> questsToProcess)
    {
        var failedQuests = questsToProcess.Where(quest =>
            quest.Status == QuestStatusEnum.MarkedAsFailed
        );
        foreach (var failedQuest in failedQuests)
        {
            var dbQuest = _databaseService.GetQuests()[failedQuest.QId];
            if (dbQuest is null)
            {
                continue;
            }

            if (dbQuest.Restartable)
            {
                failedQuest.Status = QuestStatusEnum.Fail;
            }
        }

        return questsToProcess;
    }

    /// <summary>
    ///     Adjust server trader settings if they differ from data sent by client
    /// </summary>
    /// <param name="tradersServerProfile"> Server </param>
    /// <param name="tradersClientProfile"> Client </param>
    protected void ApplyTraderStandingAdjustments(
        Dictionary<string, TraderInfo>? tradersServerProfile,
        Dictionary<string, TraderInfo>? tradersClientProfile
    )
    {
        foreach (var traderId in tradersClientProfile)
        {
            var serverProfileTrader = tradersServerProfile
                .FirstOrDefault(x => x.Key == traderId.Key)
                .Value;
            var clientProfileTrader = tradersClientProfile
                .FirstOrDefault(x => x.Key == traderId.Key)
                .Value;
            if (serverProfileTrader is null || clientProfileTrader is null)
            {
                continue;
            }

            if (clientProfileTrader.Standing != serverProfileTrader.Standing)
            // Difference found, update server profile with values from client profile
            {
                tradersServerProfile[traderId.Key].Standing = clientProfileTrader.Standing;
            }
        }
    }

    protected void HandleInsuredItemLostEvent(
        string sessionId,
        PmcData preRaidPmcProfile,
        EndLocalRaidRequestData request,
        string locationName
    )
    {
        if (request.LostInsuredItems?.Count > 0)
        {
            var mappedItems = _insuranceService.MapInsuredItemsToTrader(
                sessionId,
                request.LostInsuredItems,
                preRaidPmcProfile
            );

            // Is possible to have items in lostInsuredItems but removed before reaching mappedItems
            if (mappedItems.Count == 0)
            {
                return;
            }

            _insuranceService.StoreGearLostInRaidToSendLater(sessionId, mappedItems);

            _insuranceService.StartPostRaidInsuranceLostProcess(
                preRaidPmcProfile,
                sessionId,
                locationName
            );
        }
    }

    /// <summary>
    ///     Checks to see if player survives. run through will return false
    /// </summary>
    /// <param name="results"> Post raid request </param>
    /// <returns> True if survived </returns>
    protected bool IsPlayerSurvived(EndRaidResult results)
    {
        return results.Result == ExitStatus.SURVIVED;
    }

    /// <summary>
    ///     Is the player dead after a raid - dead = anything other than "survived" / "runner"
    /// </summary>
    /// <param name="results"> Post raid request </param>
    /// <returns> True if dead </returns>
    protected bool IsPlayerDead(EndRaidResult results)
    {
        var deathEnums = new List<ExitStatus>
        {
            ExitStatus.KILLED,
            ExitStatus.MISSINGINACTION,
            ExitStatus.LEFT,
        };
        return deathEnums.Contains(results.Result.Value);
    }

    /// <summary>
    ///     Has the player moved from one map to another
    /// </summary>
    /// <param name="results"> Post raid request </param>
    /// <returns> True if players transferred </returns>
    protected bool IsMapToMapTransfer(EndRaidResult results)
    {
        return results.Result == ExitStatus.TRANSIT;
    }

    /// <summary>
    ///     Reset the skill points earned in a raid to 0, ready for next raid
    /// </summary>
    /// <param name="commonSkills"> Profile common skills to update </param>
    protected void ResetSkillPointsEarnedDuringRaid(List<CommonSkill> commonSkills)
    {
        foreach (var skill in commonSkills)
        {
            skill.PointsEarnedDuringSession = 0;
        }
    }

    /// <summary>
    ///     Merge two dictionaries together.
    ///     Prioritise pair that has true as a value
    /// </summary>
    /// <param name="primary"> Main dictionary </param>
    /// <param name="secondary"> Secondary dictionary </param>
    protected void MergePmcAndScavEncyclopedias(PmcData primary, PmcData secondary)
    {
        var mergedDicts = primary
            .Encyclopedia?.Union(secondary.Encyclopedia)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(g => g.Key, g => g.Any(kvp => kvp.Value));

        primary.Encyclopedia = mergedDicts;
        secondary.Encyclopedia = mergedDicts;
    }

    /// <summary>
    ///     Check for and add any rewards found via the gained achievements this raid
    /// </summary>
    /// <param name="fullProfile"> Profile to add customisations to </param>
    /// <param name="postRaidAchievements"> All profile achievements at the end of a raid </param>
    protected void ProcessAchievementRewards(
        SptProfile fullProfile,
        Dictionary<MongoId, long>? postRaidAchievements
    )
    {
        var sessionId = fullProfile.ProfileInfo.ProfileId;
        var pmcProfile = fullProfile.CharacterData.PmcData;
        var preRaidAchievementIds = fullProfile.CharacterData.PmcData.Achievements;
        var postRaidAchievementIds = postRaidAchievements;
        var achievementIdsAcquiredThisRaid = postRaidAchievementIds.Where(id =>
            !preRaidAchievementIds.Contains(id)
        );

        // Get achievement data from db
        var achievementsDb = _databaseService.GetTemplates().Achievements;

        // Map the achievement ids player obtained in raid with matching achievement data from db
        var achievements = achievementIdsAcquiredThisRaid.Select(achievementId =>
            achievementsDb.FirstOrDefault(achievementDb => achievementDb.Id == achievementId.Key)
        );
        if (achievements is null)
        // No achievements found
        {
            return;
        }

        foreach (var achievement in achievements)
        {
            var rewardItems = _rewardHelper.ApplyRewards(
                achievement.Rewards,
                CustomisationSource.ACHIEVEMENT,
                fullProfile,
                pmcProfile,
                achievement.Id
            );

            if (rewardItems?.Count > 0)
            {
                _mailSendService.SendLocalisedSystemMessageToPlayer(
                    sessionId,
                    "670547bb5fa0b1a7c30d5836 0",
                    rewardItems,
                    [],
                    _timeUtil.GetHoursAsSeconds(24 * 7)
                );
            }
        }
    }
}
