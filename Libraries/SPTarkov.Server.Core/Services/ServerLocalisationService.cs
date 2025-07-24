using System.Collections.Frozen;
using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace SPTarkov.Server.Core.Services;

/// <summary>
///     Handles translating server text into different languages
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ServerLocalisationService(
    ISptLogger<ServerLocalisationService> logger,
    RandomUtil randomUtil,
    LocaleService localeService,
    JsonUtil jsonUtil,
    FileUtil fileUtil
)
{
    private readonly Dictionary<string, LazyLoad<Dictionary<string, string>>> _loadedLocales = [];
    private string _serverLocale = localeService.GetDesiredServerLocale();
    private readonly FrozenDictionary<string, string> _localeFallbacks = localeService
        .GetLocaleFallbacks()
        .ToFrozenDictionary();

    private const string DefaultLocale = "en";
    private const string LocaleDirectory = "./SPT_Data/database/locales/server";
    private bool _serverLocalesHydrated = false;

    protected void HydrateServerLocales()
    {
        if (_serverLocalesHydrated)
        {
            return;
        }

        var files = fileUtil
            .GetFiles(LocaleDirectory, true)
            .Where(f => fileUtil.GetFileExtension(f) == "json")
            .ToList();

        if (files.Count == 0)
        {
            throw new Exception($"Localisation files in directory {LocaleDirectory} not found.");
        }

        foreach (var file in files)
        {
            _loadedLocales.Add(
                fileUtil.StripExtension(file),
                new LazyLoad<Dictionary<string, string>>(() =>
                    jsonUtil.DeserializeFromFile<Dictionary<string, string>>(file) ?? []
                )
            );
        }

        if (!_loadedLocales.ContainsKey(DefaultLocale))
        {
            throw new Exception(
                $"The default locale '{DefaultLocale}' does not exist on the loaded locales."
            );
        }

        _serverLocalesHydrated = true;
    }

    public void SetServerLocaleByKey(string locale)
    {
        if (_loadedLocales.ContainsKey(locale))
        {
            _serverLocale = locale;
        }
        else
        {
            var fallback = _localeFallbacks.Where(kv => locale.StartsWith(kv.Key.Replace("*", "")));
            if (fallback.Any())
            {
                var foundFallbackLocale = fallback.First().Value;
                if (!_loadedLocales.ContainsKey(foundFallbackLocale))
                {
                    throw new Exception(
                        $"Locale '{locale}' was not defined, and the found fallback locale did not match any of the loaded locales."
                    );
                }

                _serverLocale = foundFallbackLocale;
            }

            _serverLocale = DefaultLocale;
        }
    }

    /// <summary>
    ///     Get a localised value using the passed in key
    /// </summary>
    /// <param name="key"> Key to look up locale for </param>
    /// <param name="args"> optional arguments </param>
    /// <returns> Localised string </returns>
    public string GetText(string key, object? args = null)
    {
        return args is null ? GetLocalisedValue(key) : GetLocalised(key, args);
    }

    /// <summary>
    ///     Get a localised value using the passed in key
    /// </summary>
    /// <param name="key"> Key to look up locale for </param>
    /// <param name="value"> Value to localize </param>
    /// <returns> Localised string </returns>
    public string GetText<T>(string key, T value)
        where T : IConvertible?
    {
        return GetLocalised(key, value);
    }

    /// <summary>
    ///     Get all locale keys
    /// </summary>
    /// <returns> Generic collection of keys </returns>
    public IEnumerable<string> GetLocaleKeys()
    {
        return _loadedLocales["en"].Value?.Keys ?? Enumerable.Empty<string>();
    }

    /// <summary>
    ///     From the provided partial key, find all keys that start with text and choose a random match
    /// </summary>
    /// <param name="partialKey"> Key to match locale keys on </param>
    /// <returns> Locale text </returns>
    public string GetRandomTextThatMatchesPartialKey(string partialKey)
    {
        var matchingKeys = GetLocaleKeys().Where(x => x.Contains(partialKey));

        if (!matchingKeys.Any())
        {
            logger.Warning($"No locale keys found for: {partialKey}");

            return string.Empty;
        }

        return GetText(randomUtil.GetArrayValue(matchingKeys));
    }

    public string GetLocalisedValue(string key)
    {
        // On the initial localised request, hydrate server locales
        if (!_serverLocalesHydrated)
        {
            HydrateServerLocales();
        }

        // get loaded locales for set key
        if (!_loadedLocales.TryGetValue(_serverLocale, out var locales))
        {
            // if we are unable to get the "loadedLocales" for the set locale, return the key
            return key;
        }

        // searching through loaded locales for given key
        if (!locales.Value.TryGetValue(key, out var value))
        {
            // if the key is not found in loaded locales
            // check if the key is found in the default locale
            _loadedLocales.TryGetValue(DefaultLocale, out var defaults);
            if (!defaults.Value.TryGetValue(key, out value))
            {
                value = localeService
                    .GetLocaleDb(DefaultLocale)
                    .FirstOrDefault(x => x.Key == key)
                    .Value;
            }

            return value ?? key;
        }

        // if the key is found in the server locale, return the value
        return value;
    }

    protected string GetLocalised(string key, object? args)
    {
        var rawLocalizedString = GetLocalisedValue(key);
        if (args == null)
        {
            return rawLocalizedString;
        }

        var typeProps = args.GetType().GetProperties();

        foreach (var propertyInfo in typeProps)
        {
            var localizedName = $"{{{{{propertyInfo.GetJsonName()}}}}}";
            if (rawLocalizedString.Contains(localizedName))
            {
                rawLocalizedString = rawLocalizedString.Replace(
                    localizedName,
                    propertyInfo.GetValue(args)?.ToString() ?? string.Empty
                );
            }
        }

        return rawLocalizedString;
    }

    protected string GetLocalised<T>(string key, T? value)
        where T : IConvertible?
    {
        var rawLocalizedString = GetLocalisedValue(key);
        return rawLocalizedString.Replace("%s", value?.ToString() ?? string.Empty);
    }

    // gets the localized string directly
    protected string GetLocalised<T>(string key)
    {
        return GetLocalisedValue(key);
    }
}
