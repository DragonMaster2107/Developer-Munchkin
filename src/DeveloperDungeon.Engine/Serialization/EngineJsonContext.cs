using System.Text.Json.Serialization;
using DeveloperDungeon.Engine.Cards;

namespace DeveloperDungeon.Engine.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<CardDefinition>))]
internal partial class EngineJsonContext : JsonSerializerContext
{
}
