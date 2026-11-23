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
