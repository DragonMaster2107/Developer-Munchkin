using System.Text.Json.Serialization;

namespace DeveloperDungeon.Web.Logic;

public sealed record GameSettings(bool AnimationsEnabled = true, int CountdownSeconds = 5);

[JsonSerializable(typeof(GameSettings))]
public partial class WebJsonContext : JsonSerializerContext;
