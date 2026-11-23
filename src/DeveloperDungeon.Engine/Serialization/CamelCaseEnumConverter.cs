using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeveloperDungeon.Engine.Serialization;

/// <summary>Schreibt Enums als camelCase-Strings (trim-sicher, ohne Reflection).</summary>
public sealed class CamelCaseEnumConverter<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.CamelCase)
    where T : struct, Enum;
