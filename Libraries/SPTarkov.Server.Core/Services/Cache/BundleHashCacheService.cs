using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace SPTarkov.Server.Core.Services.Cache;

[Injectable]
public class BundleHashCacheService(
    ISptLogger<BundleHashCacheService> _logger,
    HashUtil _hashUtil,
    JsonUtil _jsonUtil,
    FileUtil _fileUtil
)
{
    protected static readonly string _bundleHashCachePath = "./user/cache/bundleHashCache.json";
    protected Dictionary<string, string> _bundleHashes = new();

    public string GetStoredValue(string key)
    {
        _bundleHashes.TryGetValue(key, out var value);

        return value;
    }

    public void StoreValue(string key, string value)
    {
        _bundleHashes.Add(key, value);

        _fileUtil.WriteFile(_bundleHashCachePath, _jsonUtil.Serialize(_bundleHashes));

        if (_logger.IsLogEnabled(LogLevel.Debug))
        {
            _logger.Debug($"Bundle {key} hash stored in {_bundleHashCachePath}");
        }
    }

    public bool MatchWithStoredHash(string bundlePath, string hash)
    {
        return GetStoredValue(bundlePath) == hash;
    }

    public bool CalculateAndMatchHash(string bundlePath)
    {
        var fileContents = _fileUtil.ReadFile(bundlePath);
        var generatedHash = _hashUtil.GenerateHashForData(HashingAlgorithm.MD5, fileContents);

        return MatchWithStoredHash(bundlePath, generatedHash);
    }

    public void CalculateAndStoreHash(string bundlePath)
    {
        var fileContents = _fileUtil.ReadFile(bundlePath);
        var generatedHash = _hashUtil.GenerateHashForData(HashingAlgorithm.MD5, fileContents);

        StoreValue(bundlePath, generatedHash);
    }
}
