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
