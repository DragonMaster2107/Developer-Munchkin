using System.Text.Json.Serialization;
using DeveloperDungeon.Engine.Cards;

namespace DeveloperDungeon.Engine.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(List<CardDefinition>))]
[JsonSerializable(typeof(DeveloperDungeon.Engine.State.GameState))]
internal partial class EngineJsonContext : JsonSerializerContext
{
}
