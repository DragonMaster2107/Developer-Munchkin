using DeveloperDungeon.Engine;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Web.Logic;

namespace DeveloperDungeon.Web.Tests.Support;

public static class TestGames
{
    public static readonly CardCatalog Catalog = CardCatalog.LoadEmbedded();

    public static TableController NewController(int players = 3, ulong seed = 42, IGameStorage? storage = null)
    {
        var controller = new TableController(Catalog, storage ?? new InMemoryGameStorage());
        controller.StartNewGameAsync(Setups(players), seed).GetAwaiter().GetResult();
        return controller;
    }

    public static IReadOnlyList<PlayerSetup> Setups(int players) =>
        [.. Enumerable.Range(1, players).Select(i => new PlayerSetup($"Spieler {i}", PlayerColors.All[i - 1].Hex))];

    /// <summary>Klickt die Aufbau-Runde komplett durch (Gerät bestätigen, Fertig).</summary>
    public static async Task CompleteSetupAsync(TableController c)
    {
        while (c.SetupPlayerId is not null)
        {
            c.ConfirmHandoff();
            await c.FinishSetupForCurrentAsync();
        }
    }

    public static async Task<TableController> StartedAsync(int players = 3, ulong seed = 42, IGameStorage? storage = null)
    {
        var c = NewController(players, seed, storage);
        await CompleteSetupAsync(c);
        return c;
    }

    /// <summary>Spielt den aktiven Spieler bis zum Zugende (Ticket öffnen, Backlog, ggf. Karten abgeben).</summary>
    public static async Task PlayTurnToEndAsync(TableController c)
    {
        var start = c.View.ActivePlayerId;
        for (var i = 0; i < 20 && c.View.ActivePlayerId == start; i++)
        {
            if (c.HandoffTargetId is not null)
            {
                c.ConfirmHandoff();
            }
            var actor = c.View.ActivePlayerId;
            GameCommand cmd = c.View.Phase switch
            {
                GamePhase.OpenTicket => new OpenTicket(actor),
                GamePhase.AfterTicket => new WorkBacklog(actor),
                _ when c.View.Phase == GamePhase.Combat || c.View.CurrentChoice is not null => throw new InvalidOperationException("Zug nicht geradlinig spielbar"),
                GamePhase.EndTurn => new EndTurn(actor, [.. (c.View.MyHand ?? []).Skip(5).Select(x => x.InstanceId)]),
                _ => throw new InvalidOperationException($"Unerwartete Phase {c.View.Phase}"),
            };
            var result = await c.SendAsync(cmd);
            if (!result.Accepted)
            {
                throw new InvalidOperationException($"{cmd} abgelehnt: {result.Rejection}");
            }
        }
    }

    /// <summary>Sucht einen Seed, bei dem der erste Zug ohne Kampf durchspielbar ist, und liefert den Controller nach Zugende.</summary>
    public static async Task<(TableController Controller, string FirstPlayerId)> AfterFirstTurnAsync()
    {
        for (ulong seed = 1; seed < 500; seed++)
        {
            var c = await StartedAsync(3, seed);
            c.ConfirmHandoff();
            var first = c.View.ActivePlayerId;
            try
            {
                await PlayTurnToEndAsync(c);
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            if (c.View.ActivePlayerId != first)
            {
                return (c, first);
            }
        }
        throw new InvalidOperationException("Kein Seed mit geradlinigem Zug gefunden.");
    }

    /// <summary>Sucht einen Seed, bei dem das erste geöffnete Ticket ein Monster ist, und führt den Controller in den Kampf.</summary>
    public static async Task<(TableController Controller, InMemoryGameStorage Storage)> InCombatAsync()
    {
        for (ulong seed = 1; seed < 500; seed++)
        {
            var storage = new InMemoryGameStorage();
            var c = await StartedAsync(3, seed, storage);
            c.ConfirmHandoff();
            await c.SendAsync(new OpenTicket(c.View.ActivePlayerId));
            if (c.View.Phase == GamePhase.Combat)
            {
                return (c, storage);
            }
        }
        throw new InvalidOperationException("Kein Seed mit Kampf gefunden.");
    }
}
