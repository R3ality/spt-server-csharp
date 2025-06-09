using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace SPTarkov.Server.Core.Services.Cache;

[Injectable]
public class ModHashCacheService(
    ISptLogger<ModHashCacheService> _logger,
    JsonUtil _jsonUtil,
    HashUtil _hashUtil,
    FileUtil _fileUtil
)
{
    protected readonly string _modCachePath = "./user/cache/modCache.json";
    protected readonly Dictionary<string, string> _modHashes = new();

    public string? GetStoredValue(string key)
    {
        _modHashes.TryGetValue(key, out var value);

        return value;
    }

    public void StoreValue(string key, string value)
    {
        _modHashes.TryAdd(key, value);

        _fileUtil.WriteFile(_modCachePath, _jsonUtil.Serialize(_modHashes));

        if (_logger.IsLogEnabled(LogLevel.Debug))
        {
            _logger.Debug($"Mod {key} hash stored in: {_modCachePath}");
        }
    }

    public bool MatchWithStoredHash(string modName, string hash)
    {
        return GetStoredValue(modName) == hash;
    }

    public bool CalculateAndCompareHash(string modName, string modContent)
    {
        var generatedHash = _hashUtil.GenerateHashForData(HashingAlgorithm.SHA1, modContent);

        return MatchWithStoredHash(modName, generatedHash);
    }

    public void CalculateAndStoreHash(string modName, string modContent)
    {
        var generatedHash = _hashUtil.GenerateHashForData(HashingAlgorithm.SHA1, modContent);

        StoreValue(modName, generatedHash);
    }
}
