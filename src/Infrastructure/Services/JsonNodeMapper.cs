using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Infrastructure.Services;

internal static class JsonNodeMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string? Serialize(JsonNode? node) => node?.ToJsonString(SerializerOptions);

    public static JsonNode? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonNode.Parse(json);
    }
}

