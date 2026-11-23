using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.State;
using Xunit.Abstractions;

namespace DeveloperDungeon.Engine.Tests.Simulation;

public class SimulationTests(ITestOutputHelper output)
{
    private const int Games = 50;
    private const int MaxTurns = 400;
    private const int MaxCommands = 20_000;

    [Fact]
    public void RandomGames_NeverDeadlock()
    {
        var catalog = CardCatalog.LoadEmbedded();
        var finished = 0;
        var turnTotal = 0;

        for (var seed = 1; seed <= Games; seed++)
        {
            var playerCount = 3 + seed % 4;
            var players = Enumerable.Range(1, playerCount).Select(i => new PlayerSetup($"Bot{i}", "grey")).ToList();
            var game = Game.New(players, catalog, (ulong)seed);
            var commands = 0;

            while (game.State.Phase != GamePhase.GameOver && game.State.TurnNumber < MaxTurns)
            {
                if (++commands > MaxCommands)
                {
                    Assert.Fail($"Seed {seed}: mehr als {MaxCommands} Befehle ohne Fortschritt.\n{game.Save()}");
                }

                // Abgelehnte Versuche (z. B. Equip) sind erlaubt; mindestens ein Befehl pro Schritt muss angenommen werden.
                var accepted = SimpleBot.NextCommands(game).Any(command => game.Execute(command).Accepted);
                if (!accepted)
                {
                    Assert.Fail($"Seed {seed}: Deadlock in Phase {game.State.Phase}, Zug {game.State.TurnNumber}.\n{game.Save()}");
                }
            }

            if (game.State.Phase == GamePhase.GameOver)
            {
                finished++;
            }
            turnTotal += game.State.TurnNumber;
        }

        output.WriteLine($"Beendete Partien: {finished}/{Games} ({100.0 * finished / Games:F0} %)");
        output.WriteLine($"Durchschnittliche Zuganzahl: {(double)turnTotal / Games:F1}");
    }
}
