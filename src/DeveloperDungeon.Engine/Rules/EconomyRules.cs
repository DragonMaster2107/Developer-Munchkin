using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

/// <summary>Stufe-hoch-Karten und Verkaufen außerhalb des Kampfs.</summary>
internal static class EconomyRules
{
    private const int CreditsPerLevel = 1000;

    public static CommandResult PlayLevelUp(GameState s, CardCatalog c, PlayerState player, CardInstance card)
    {
        if (!CommandGuard.IsOwnTurnOutsideCombat(s, player.Id))
        {
            return CommandResult.Rejected("Stufe-hoch-Karten spielst du nur in deinem Zug außerhalb eines Kampfs.");
        }

        var def = c.Get(card.CardId);
        var levels = def.Effects.OfType<GainLevels>().Sum(e => e.Amount);
        if (player.Level + levels >= LevelRules.WinLevel)
        {
            return CommandResult.Rejected("Stufe 10 gibt es nur im Kampf.");
        }

        var events = new List<GameEvent>();
        player.Hand.Remove(card);
        Decks.Discard(s, c, card);
        GameLog.Add(s, $"{player.Name} spielt {def.Name}.");
        LevelRules.Set(s, player, player.Level + levels, events);
        return CommandResult.Ok(events);
    }

    public static CommandResult Sell(GameState s, CardCatalog c, SellItems cmd)
    {
        if (!CommandGuard.IsOwnTurnOutsideCombat(s, cmd.ActorId))
        {
            return CommandResult.Rejected("Verkaufen kannst du nur in deinem Zug außerhalb eines Kampfs.");
        }
        if (cmd.InstanceIds is null || cmd.InstanceIds.Count == 0 || cmd.InstanceIds.Distinct().Count() != cmd.InstanceIds.Count)
        {
            return CommandResult.Rejected("Wähle mindestens eine Karte, jede nur einmal.");
        }

        var player = s.Players.First(p => p.Id == cmd.ActorId);
        var sold = new List<(CardInstance Card, List<CardInstance> Source)>();
        foreach (var id in cmd.InstanceIds)
        {
            var source = new[] { player.Hand, player.Equipped, player.Backpack }.FirstOrDefault(l => l.Any(x => x.InstanceId == id));
            if (source is null)
            {
                return CommandResult.Rejected("Du kannst nur Karten aus Hand, Angelegtem und Rucksack verkaufen.");
            }
            var card = source.First(x => x.InstanceId == id);
            if (c.Get(card.CardId).Value <= 0)
            {
                return CommandResult.Rejected($"{c.Get(card.CardId).Name} hat keinen Wert und lässt sich nicht verkaufen.");
            }
            sold.Add((card, source));
        }

        var credits = sold.Sum(x => c.Get(x.Card.CardId).Value);
        if (credits < CreditsPerLevel)
        {
            return CommandResult.Rejected($"Du brauchst mindestens {CreditsPerLevel} Wert zum Verkaufen (aktuell {credits}).");
        }
        var levels = credits / CreditsPerLevel;
        if (player.Level + levels >= LevelRules.WinLevel)
        {
            return CommandResult.Rejected("Stufe 10 gibt es nur im Kampf.");
        }

        foreach (var (card, source) in sold)
        {
            source.Remove(card);
            Decks.Discard(s, c, card);
        }
        var events = new List<GameEvent> { new ItemsSold(player.Id, credits, levels) };
        GameLog.Add(s, $"{player.Name} verkauft {sold.Count} Karten für {credits} und steigt um {levels} Stufen.");
        LevelRules.Set(s, player, player.Level + levels, events);
        return CommandResult.Ok(events);
    }
}
