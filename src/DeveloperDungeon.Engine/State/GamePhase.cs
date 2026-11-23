using System.Text.Json.Serialization;
using DeveloperDungeon.Engine.Serialization;

namespace DeveloperDungeon.Engine.State;

[JsonConverter(typeof(CamelCaseEnumConverter<GamePhase>))]
public enum GamePhase { Setup, OpenTicket, AfterTicket, Combat, EndTurn, GameOver }
