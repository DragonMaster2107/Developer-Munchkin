namespace DeveloperDungeon.Engine.Commands;

public abstract record GameCommand(string ActorId);

public sealed record StartGame(string ActorId) : GameCommand(ActorId);

public sealed record EquipCard(string ActorId, int InstanceId) : GameCommand(ActorId);

public sealed record UnequipCard(string ActorId, int InstanceId) : GameCommand(ActorId);

public sealed record DiscardOwnCard(string ActorId, int InstanceId) : GameCommand(ActorId);
