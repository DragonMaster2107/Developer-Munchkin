using System.Text.Json.Serialization;
using DeveloperDungeon.Engine.Serialization;

namespace DeveloperDungeon.Engine.Cards;

[JsonConverter(typeof(CamelCaseEnumConverter<Deck>))]
public enum Deck { Ticket, Loot }

[JsonConverter(typeof(CamelCaseEnumConverter<CardKind>))]
public enum CardKind { Monster, MonsterEnhancer, Curse, Department, Language, Special, Equipment, OneShot, LevelUp }

[JsonConverter(typeof(CamelCaseEnumConverter<Slot>))]
public enum Slot { None, Head, Body, Feet, Hands, Big }

[JsonConverter(typeof(CamelCaseEnumConverter<Side>))]
public enum Side { Player, Monster }

[JsonConverter(typeof(CamelCaseEnumConverter<Direction>))]
public enum Direction { Left, Right }
