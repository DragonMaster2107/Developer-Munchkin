using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class CommandGuard
{
    /// <summary>Gemeinsame Vorprüfungen; liefert den Ablehnungsgrund oder null.</summary>
    public static string? Check(GameState s, GameCommand command)
    {
        if (s.Players.All(p => p.Id != command.ActorId))
        {
            return "Unbekannter Spieler";
        }
        if (s.Phase == GamePhase.GameOver)
        {
            return "Das Spiel ist vorbei.";
        }
        if (s.PendingChoices.Count > 0 && !AllowedWhilePending(command))
        {
            var pendingId = s.PendingChoices[0].PlayerId;
            var name = s.Players.FirstOrDefault(p => p.Id == pendingId)?.Name ?? pendingId;
            return $"Erst muss {name} eine Auswahl treffen.";
        }
        return null;
    }

    /// <summary>Befehle, die trotz offener Auswahl erlaubt sind. Task 5 ergänzt ResolveChoice, Task 10 die Werkzeugkasten-Befehle (je eine Zeile).</summary>
    private static bool AllowedWhilePending(GameCommand command) => command switch
    {
        ResolveChoice => true,
        _ => false,
    };

    public static bool IsOwnTurnOutsideCombat(GameState s, string actorId) =>
        s.Players[s.ActivePlayerIndex].Id == actorId
        && s.Phase is GamePhase.OpenTicket or GamePhase.AfterTicket or GamePhase.EndTurn;
}
