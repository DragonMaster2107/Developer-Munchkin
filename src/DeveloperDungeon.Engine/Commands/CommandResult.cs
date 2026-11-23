using DeveloperDungeon.Engine.Events;

namespace DeveloperDungeon.Engine.Commands;

public sealed record CommandResult(bool Accepted, string? Rejection, IReadOnlyList<GameEvent> Events)
{
    public static CommandResult Ok(IReadOnlyList<GameEvent> events) => new(true, null, events);

    public static CommandResult Rejected(string reason) => new(false, reason, []);
}
