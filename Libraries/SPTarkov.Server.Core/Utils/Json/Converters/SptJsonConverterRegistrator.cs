using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace SPTarkov.Server.Core.Utils.Json.Converters;

[Injectable]
public class SptJsonConverterRegistrator : IJsonConverterRegistrator
{
    public IEnumerable<JsonConverter> GetJsonConverters()
    {
        return
        [
            new BaseSptLoggerReferenceConverter(),
            new ListOrTConverterFactory(),
            new DictionaryOrListConverter(),
            new BaseInteractionRequestDataConverter(),
            new StringToMongoIdConverter(),
            new EftEnumConverterFactory(),
            new EftListEnumConverterFactory(),
            new EnumerableConverterFactory(),
        ];
    }
}
