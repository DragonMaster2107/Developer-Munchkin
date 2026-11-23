using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class StartRules
{
    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, StartGame cmd)
    {
        if (s.Phase != GamePhase.Setup)
        {
            return CommandResult.Rejected("Das Spiel läuft bereits.");
        }

        s.Phase = GamePhase.OpenTicket;
        s.TurnNumber = 1;
        var start = s.Players[s.ActivePlayerIndex];
        GameLog.Add(s, $"Das Spiel beginnt. {start.Name} fängt an.");
        return CommandResult.Ok([new GameStarted(start.Id)]);
    }
}
