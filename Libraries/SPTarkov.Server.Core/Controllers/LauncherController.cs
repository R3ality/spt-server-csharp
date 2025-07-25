using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Launcher;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using Info = SPTarkov.Server.Core.Models.Eft.Profile.Info;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class LauncherController(
    ISptLogger<LauncherController> _logger,
    IReadOnlyList<SptMod> _loadedMods,
    HashUtil _hashUtil,
    TimeUtil _timeUtil,
    RandomUtil _randomUtil,
    SaveServer _saveServer,
    HttpServerHelper _httpServerHelper,
    ProfileHelper _profileHelper,
    DatabaseService _databaseService,
    ServerLocalisationService _serverLocalisationService,
    ConfigServer _configServer
)
{
    protected readonly CoreConfig _coreConfig = _configServer.GetConfig<CoreConfig>();

    /// <summary>
    ///     Handle launcher connecting to server
    /// </summary>
    /// <returns>ConnectResponse</returns>
    public ConnectResponse Connect()
    {
        // Get all possible profile types + filter out any that are blacklisted
        var profileTemplates = _databaseService
            .GetProfileTemplates()
            .Where(profile =>
                !_coreConfig.Features.CreateNewProfileTypesBlacklist.Contains(profile.Key)
            )
            .ToDictionary();

        return new ConnectResponse
        {
            BackendUrl = _httpServerHelper.GetBackendUrl(),
            Name = _coreConfig.ServerName,
            Editions = profileTemplates.Select(x => x.Key).ToList(),
            ProfileDescriptions = GetProfileDescriptions(profileTemplates),
        };
    }

    /// <summary>
    ///     Get descriptive text for each of the profile editions a player can choose, keyed by profile.json profile type e.g. "Edge Of Darkness"
    /// </summary>
    /// <param name="profileTemplates">Profiles to get descriptions of</param>
    /// <returns>Dictionary of profile types with related descriptive text</returns>
    protected Dictionary<string, string> GetProfileDescriptions(
        Dictionary<string, ProfileSides> profileTemplates
    )
    {
        var result = new Dictionary<string, string>();
        foreach (var (profileKey, profile) in profileTemplates)
        {
            result.TryAdd(
                profileKey,
                _serverLocalisationService.GetText(profile.DescriptionLocaleKey)
            );
        }

        return result;
    }

    /// <summary>
    /// </summary>
    /// <param name="sessionId">Session/Player id</param>
    /// <returns></returns>
    public Info? Find(string? sessionId)
    {
        return
            sessionId is not null
            && _saveServer.GetProfiles().TryGetValue(sessionId, out var profile)
            ? profile.ProfileInfo
            : null;
    }

    /// <summary>
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public string? Login(LoginRequestData? info)
    {
        foreach (var (sessionId, profile) in _saveServer.GetProfiles())
        {
            var account = profile.ProfileInfo;
            if (info?.Username == account?.Username)
            {
                return sessionId;
            }
        }

        return null;
    }

    /// <summary>
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public async Task<string> Register(RegisterData info)
    {
        foreach (var (_, profile) in _saveServer.GetProfiles())
        {
            if (info.Username == profile.ProfileInfo?.Username)
            {
                return "";
            }
        }

        return await CreateAccount(info);
    }

    /// <summary>
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    protected async Task<string> CreateAccount(RegisterData info)
    {
        var profileId = GenerateProfileId();
        var scavId = GenerateProfileId();
        var newProfileDetails = new Info
        {
            ProfileId = profileId,
            ScavengerId = scavId,
            Aid = _hashUtil.GenerateAccountId(),
            Username = info.Username,
            Password = info.Password,
            IsWiped = true,
            Edition = info.Edition,
        };
        _saveServer.CreateProfile(newProfileDetails);

        await _saveServer.LoadProfileAsync(profileId);
        await _saveServer.SaveProfileAsync(profileId);

        return profileId;
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    protected string GenerateProfileId()
    {
        var timestamp = _timeUtil.GetTimeStamp();

        return FormatID(timestamp, timestamp * _randomUtil.GetInt(1, 1000000));
    }

    /// <summary>
    /// </summary>
    /// <param name="timeStamp"></param>
    /// <param name="counter"></param>
    /// <returns></returns>
    protected string FormatID(long timeStamp, long counter)
    {
        var timeStampStr = Convert.ToString(timeStamp, 16).PadLeft(8, '0');
        var counterStr = Convert.ToString(counter, 16).PadLeft(16, '0');

        return timeStampStr.ToLowerInvariant() + counterStr.ToLowerInvariant();
    }

    /// <summary>
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public string? ChangeUsername(ChangeRequestData info)
    {
        var sessionID = Login(info);

        if (!string.IsNullOrEmpty(sessionID))
        {
            _saveServer.GetProfile(sessionID).ProfileInfo!.Username = info.Change;
        }

        return sessionID;
    }

    /// <summary>
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public string? ChangePassword(ChangeRequestData info)
    {
        var sessionID = Login(info);

        if (!string.IsNullOrEmpty(sessionID))
        {
            _saveServer.GetProfile(sessionID).ProfileInfo!.Password = info.Change;
        }

        return sessionID;
    }

    /// <summary>
    ///     Handle launcher requesting profile be wiped
    /// </summary>
    /// <param name="info">Registration data</param>
    /// <returns>Session id</returns>
    public string? Wipe(RegisterData info)
    {
        if (!_coreConfig.AllowProfileWipe)
        {
            return null;
        }

        var sessionID = Login(info);

        if (!string.IsNullOrEmpty(sessionID))
        {
            var profileInfo = _saveServer.GetProfile(sessionID).ProfileInfo;
            profileInfo!.Edition = info.Edition;
            profileInfo.IsWiped = true;
        }

        return sessionID;
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public string GetCompatibleTarkovVersion()
    {
        return _coreConfig.CompatibleTarkovVersion;
    }

    /// <summary>
    ///     Get the mods the server has currently loaded
    /// </summary>
    /// <returns>Dictionary of mod name and mod details</returns>
    public Dictionary<string, AbstractModMetadata> GetLoadedServerMods()
    {
        return _loadedMods.ToDictionary(
            sptMod => sptMod.ModMetadata?.Name ?? "UNKNOWN MOD",
            sptMod => sptMod.ModMetadata
        );
    }

    /// <summary>
    ///     Get the mods a profile has ever loaded into game with
    /// </summary>
    /// <param name="sessionID">Session/Player id</param>
    /// <returns>Array of mod details</returns>
    public List<ModDetails> GetServerModsProfileUsed(string sessionId)
    {
        var profile = _profileHelper.GetFullProfile(sessionId);

        if (profile?.SptData?.Mods is not null)
        {
            return GetProfileModsGroupedByModName(profile?.SptData?.Mods);
        }

        return [];
    }

    /// <summary>
    /// </summary>
    /// <param name="profileMods"></param>
    /// <returns></returns>
    public List<ModDetails> GetProfileModsGroupedByModName(List<ModDetails> profileMods)
    {
        // Group all mods used by profile by name
        var modsGroupedByName = new Dictionary<string, List<ModDetails>>();
        foreach (var mod in profileMods)
        {
            if (!modsGroupedByName.ContainsKey(mod.Name))
            {
                modsGroupedByName[mod.Name] = [];
            }

            modsGroupedByName[mod.Name].Add(mod);
        }

        // Find the highest versioned mod and add to results array
        var result = new List<ModDetails>();
        foreach (var (modName, modDatas) in modsGroupedByName)
        {
            var modVersions = modDatas.Select(x => x.Version);
            // var highestVersion = MaxSatisfying(modVersions, "*"); ?? TODO: Node used SemVer here

            var chosenVersion = modDatas.FirstOrDefault(x => x.Name == modName); // && x.Version == highestVersion
            if (chosenVersion is null)
            {
                continue;
            }

            result.Add(chosenVersion);
        }

        return result;
    }
}
