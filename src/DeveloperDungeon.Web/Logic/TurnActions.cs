using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Views;

namespace DeveloperDungeon.Web.Logic;

public sealed record TurnAction(string Label, GameCommand Command);

/// <summary>Die Standard-Zugaktionen des aktiven Spielers je Phase.</summary>
public static class TurnActions
{
    public const int HandLimit = 5;

    public static IReadOnlyList<TurnAction> For(GameView view, string actorId)
    {
        if (view.WinnerId is not null || actorId != view.ActivePlayerId)
        {
            return [];
        }

        return view.Phase switch
        {
            GamePhase.OpenTicket => [new TurnAction("Ticket öffnen", new OpenTicket(actorId))],
            GamePhase.AfterTicket => [new TurnAction("Backlog abarbeiten", new WorkBacklog(actorId))],
            GamePhase.EndTurn when (view.MyHand?.Count ?? 0) <= HandLimit =>
                [new TurnAction("Feierabend", new EndTurn(actorId, []))],
            _ => [],
        };
    }
}
