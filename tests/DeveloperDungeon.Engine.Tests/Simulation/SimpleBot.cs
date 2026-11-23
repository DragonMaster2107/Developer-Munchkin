using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Tests.Simulation;

/// <summary>Spielt ein Spiel mit simplen Heuristiken; liefert pro Schritt Befehle in Prioritätsreihenfolge.</summary>
internal static class SimpleBot
{
    private const int HandLimit = 5;

    public static IEnumerable<GameCommand> NextCommands(Game game)
    {
        var s = game.State;
        var active = s.Players[s.ActivePlayerIndex];

        if (s.PendingChoices.Count > 0)
        {
            var choice = s.PendingChoices[0];
            var chooser = s.Players.First(p => p.Id == choice.PlayerId);
            foreach (var card in chooser.Equipped)
            {
                yield return new ResolveChoice(chooser.Id, card.InstanceId);
            }
            yield break;
        }

        switch (s.Phase)
        {
            case GamePhase.Setup:
                foreach (var player in s.Players)
                {
                    foreach (var card in player.Hand.ToList())
                    {
                        yield return new EquipCard(player.Id, card.InstanceId);
                    }
                }
                yield return new StartGame(active.Id);
                break;

            case GamePhase.OpenTicket:
                foreach (var card in active.Hand.ToList())
                {
                    yield return new EquipCard(active.Id, card.InstanceId);
                }
                yield return new OpenTicket(active.Id);
                break;

            case GamePhase.AfterTicket:
                var power = StrengthCalculator.BasePower(s, game.Catalog, active.Id);
                var monster = active.Hand.FirstOrDefault(x =>
                    game.Catalog.Get(x.CardId) is { Kind: CardKind.Monster, Level: { } level } && level < power);
                if (monster is not null)
                {
                    yield return new LookForTrouble(active.Id, monster.InstanceId);
                }
                yield return new WorkBacklog(active.Id);
                break;

            case GamePhase.Combat when s.Combat is { Stage: CombatStage.Fighting }:
                if (!StrengthCalculator.Summarize(s, game.Catalog).PlayerWouldWin)
                {
                    foreach (var card in active.Hand.ToList())
                    {
                        if (game.Catalog.Get(card.CardId).Kind == CardKind.OneShot)
                        {
                            yield return new PlayCard(active.Id, card.InstanceId, Side: Side.Player);
                        }
                    }
                }
                yield return new ResolveCombat(active.Id);
                break;

            case GamePhase.Combat when s.Combat is { Stage: CombatStage.Fleeing } fleeing:
                foreach (var flee in fleeing.Fleeing)
                {
                    yield return new AttemptEscape(flee.PlayerId);
                }
                break;

            case GamePhase.EndTurn:
                var excess = Math.Max(0, active.Hand.Count - HandLimit);
                yield return new EndTurn(active.Id, active.Hand.Take(excess).Select(x => x.InstanceId).ToList());
                break;
        }
    }
}
