using System.Text.Json.Serialization;
using DeveloperDungeon.Engine.Serialization;

namespace DeveloperDungeon.Engine.Commands;

public abstract record GameCommand(string ActorId);

public sealed record StartGame(string ActorId) : GameCommand(ActorId);

public sealed record EquipCard(string ActorId, int InstanceId) : GameCommand(ActorId);

public sealed record UnequipCard(string ActorId, int InstanceId) : GameCommand(ActorId);

public sealed record DiscardOwnCard(string ActorId, int InstanceId) : GameCommand(ActorId);

/// <summary>Spielt eine Handkarte aus; je nach Kartenart sind unterschiedliche Ziel-Felder relevant.</summary>
public sealed record PlayCard(
    string ActorId,
    int InstanceId,
    string? TargetPlayerId = null,
    int? TargetMonsterInstanceId = null,
    Cards.Side? Side = null,
    int? SecondaryInstanceId = null) : GameCommand(ActorId);

public sealed record ResolveChoice(string ActorId, int InstanceId) : GameCommand(ActorId);

public sealed record OpenTicket(string ActorId) : GameCommand(ActorId);

public sealed record LookForTrouble(string ActorId, int MonsterInstanceId) : GameCommand(ActorId);

public sealed record WorkBacklog(string ActorId) : GameCommand(ActorId);

/// <summary>Beendet den Zug; <c>GiveAway</c> sind die InstanceIds der überzähligen Handkarten.</summary>
public sealed record EndTurn(string ActorId, IReadOnlyList<int> GiveAway) : GameCommand(ActorId);

/// <summary>Wertet den Kampf aus; nur der aktive Spieler, solange gekämpft wird.</summary>
public sealed record ResolveCombat(string ActorId) : GameCommand(ActorId);

/// <summary>Bietet dem aktiven Spieler Hilfe im Kampf an; <c>LootShare</c> = geforderte Loot-Karten.</summary>
public sealed record OfferHelp(string ActorId, int LootShare) : GameCommand(ActorId);

/// <summary>Der aktive Spieler nimmt das Hilfsangebot von <c>HelperId</c> an.</summary>
public sealed record AcceptHelp(string ActorId, string HelperId) : GameCommand(ActorId);

/// <summary>Ein fliehender Spieler würfelt für alle seine offenen Monster.</summary>
public sealed record AttemptEscape(string ActorId) : GameCommand(ActorId);

/// <summary>Verkauft Karten aus Hand, Angelegtem und Rucksack; je volle 1000 Wert gibt es eine Stufe.</summary>
public sealed record SellItems(string ActorId, IReadOnlyList<int> InstanceIds) : GameCommand(ActorId);

[JsonConverter(typeof(CamelCaseEnumConverter<Zone>))]
public enum Zone { Hand, Equipped, Backpack, Department, Language }

/// <summary>Werkzeugkasten: Stufe eines Spielers ändern (ohne Regelprüfung, 1 bis 10).</summary>
public sealed record ManualAdjustLevel(string ActorId, string TargetPlayerId, int Delta) : GameCommand(ActorId);

/// <summary>Werkzeugkasten: Karte aus Spielerzone, Kampf oder Ablage in eine Zone eines Spielers legen.</summary>
public sealed record ManualMoveCard(string ActorId, int InstanceId, string ToPlayerId, Zone ToZone) : GameCommand(ActorId);

/// <summary>Werkzeugkasten: Karte aus Spielerzone oder Kampf auf die Ablage legen.</summary>
public sealed record ManualDiscard(string ActorId, int InstanceId) : GameCommand(ActorId);

/// <summary>Werkzeugkasten: Karte aus einer Ablage auf die Hand eines Spielers nehmen.</summary>
public sealed record ManualTakeFromDiscard(string ActorId, int InstanceId, string ToPlayerId) : GameCommand(ActorId);

/// <summary>Werkzeugkasten: Kampfstärke einer Seite anpassen; nur im Kampf.</summary>
public sealed record ManualCombatAdjust(string ActorId, Cards.Side Side, int Delta) : GameCommand(ActorId);

/// <summary>Werkzeugkasten: W6 werfen.</summary>
public sealed record RollDie(string ActorId) : GameCommand(ActorId);
