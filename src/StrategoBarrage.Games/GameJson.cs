using System.Text.Json;

namespace StrategoBarrage.Games;

public static class GameJson
{
    // camelCase names, as in the data model. Cosmos DB may return an event's "kind" after its other
    // properties, so it isn't required to come first.
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
