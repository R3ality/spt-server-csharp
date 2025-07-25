using SPTarkov.DI.Annotations;
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
public class LauncherV2Controller(
    ISptLogger<LauncherV2Controller> _logger,
    IReadOnlyList<SptMod> _loadedMods,
    HashUtil _hashUtil,
    TimeUtil _timeUtil,
    RandomUtil _randomUtil,
    SaveServer _saveServer,
    DatabaseService _databaseService,
    ServerLocalisationService _serverLocalisationService,
    ConfigServer _configServer,
    Watermark _watermark
)
{
    protected readonly CoreConfig _coreConfig = _configServer.GetConfig<CoreConfig>();

    /// <summary>
    ///     Returns a simple string of Pong!
    /// </summary>
    /// <returns></returns>
    public string Ping()
    {
        return "Pong!";
    }

    /// <summary>
    ///     Returns all available profile types and descriptions for creation.
    ///     - This is also localised.
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, string> Types()
    {
        var result = new Dictionary<string, string>();
        var dbProfiles = _databaseService.GetProfileTemplates();

        foreach (var profileKvP in dbProfiles)
        {
            result.TryAdd(
                profileKvP.Key,
                _serverLocalisationService.GetText(profileKvP.Value.DescriptionLocaleKey)
            );
        }

        return result;
    }

    /// <summary>
    ///     Checks if login details were correct.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public bool Login(LoginRequestData info)
    {
        var sessionId = GetSessionId(info);

        return sessionId is not null;
    }

    /// <summary>
    ///     Register a new profile.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public async Task<bool> Register(RegisterData info)
    {
        foreach (var (_, profile) in _saveServer.GetProfiles())
        {
            if (info.Username == profile.ProfileInfo!.Username)
            {
                return false;
            }
        }

        await CreateAccount(info);
        return true;
    }

    /// <summary>
    ///     Make a password change.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public async Task<bool> PasswordChange(ChangeRequestData info)
    {
        var sessionId = GetSessionId(info);

        if (sessionId is null)
        {
            return false;
        }

        if (!Login(info))
        {
            return false;
        }

        _saveServer.GetProfile(sessionId).ProfileInfo!.Password = info.Change;
        await _saveServer.SaveProfileAsync(sessionId);
        return true;
    }

    /// <summary>
    ///     Remove profile from server.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public bool Remove(LoginRequestData info)
    {
        var sessionId = GetSessionId(info);

        return sessionId is not null && _saveServer.RemoveProfile(sessionId);
    }

    /// <summary>
    ///     Gets the Servers SPT Version.
    ///     - "4.0.0"
    /// </summary>
    /// <returns></returns>
    public string SptVersion()
    {
        return _watermark.GetVersionTag();
    }

    /// <summary>
    ///     Gets the compatible EFT Version.
    ///     - "0.14.9.31124"
    /// </summary>
    /// <returns></returns>
    public string EftVersion()
    {
        return _coreConfig.CompatibleTarkovVersion;
    }

    /// <summary>
    ///     Gets the Servers loaded mods.
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, AbstractModMetadata> LoadedMods()
    {
        return _loadedMods.ToDictionary(
            sptMod => sptMod.ModMetadata.Name,
            sptMod => sptMod.ModMetadata
        );
    }

    /// <summary>
    ///     Creates the account from provided details.
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

    protected string GenerateProfileId()
    {
        var timestamp = _timeUtil.GetTimeStamp();

        return FormatID(timestamp, timestamp * _randomUtil.GetInt(1, 1000000));
    }

    protected string FormatID(long timeStamp, long counter)
    {
        var timeStampStr = Convert.ToString(timeStamp, 16).PadLeft(8, '0');
        var counterStr = Convert.ToString(counter, 16).PadLeft(16, '0');

        return timeStampStr.ToLowerInvariant() + counterStr.ToLowerInvariant();
    }

    protected string? GetSessionId(LoginRequestData info)
    {
        foreach (var (sessionId, profile) in _saveServer.GetProfiles())
        {
            if (
                info.Username == profile.ProfileInfo!.Username
                && info.Password == profile.ProfileInfo.Password
            )
            {
                return sessionId;
            }
        }

        return null;
    }

    public SptProfile GetProfile(string? sessionId)
    {
        return _saveServer.GetProfile(sessionId);
    }
}
