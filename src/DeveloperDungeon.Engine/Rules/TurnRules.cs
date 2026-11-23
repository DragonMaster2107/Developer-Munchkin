using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class TurnRules
{
    private const int HandLimit = 5;

    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, GameCommand cmd)
    {
        if (s.Players[s.ActivePlayerIndex].Id != cmd.ActorId)
        {
            return CommandResult.Rejected("Du bist nicht am Zug.");
        }

        return cmd switch
        {
            OpenTicket => OpenTicketHandler(s, c, rng),
            LookForTrouble look => LookForTroubleHandler(s, c, look),
            WorkBacklog => WorkBacklogHandler(s, rng),
            EndTurn end => EndTurnHandler(s, c, end),
            _ => CommandResult.Rejected("Unbekannter Befehl."),
        };
    }

    private static CommandResult OpenTicketHandler(GameState s, CardCatalog c, GameRandom rng)
    {
        if (s.Phase != GamePhase.OpenTicket)
        {
            return CommandResult.Rejected("Du hast dein Ticket schon geöffnet.");
        }

        var player = s.Players[s.ActivePlayerIndex];
        var card = Decks.Draw(s, rng, Deck.Ticket);
        if (card is null)
        {
            s.Phase = GamePhase.AfterTicket;
            GameLog.Add(s, "Es gibt keine Tickets mehr.");
            return CommandResult.Ok([]);
        }

        var def = c.Get(card.CardId);
        var events = new List<GameEvent> { new TicketOpened(player.Id, def.Id) };
        GameLog.Add(s, $"{player.Name} öffnet ein Ticket: {def.Name}.");

        switch (def.Kind)
        {
            case CardKind.Monster:
                CombatStart.Begin(s, card);
                events.Add(new CombatStarted(player.Id, def.Id));
                break;
            case CardKind.Curse:
                s.Phase = GamePhase.AfterTicket;
                Curses.Apply(s, c, rng, player.Id, player.Id, card, events);
                break;
            default:
                player.Hand.Add(card);
                s.Phase = GamePhase.AfterTicket;
                break;
        }
        return CommandResult.Ok(events);
    }

    private static CommandResult LookForTroubleHandler(GameState s, CardCatalog c, LookForTrouble cmd)
    {
        if (s.Phase != GamePhase.AfterTicket)
        {
            return CommandResult.Rejected("Ärger suchen geht nur nach dem Ticket.");
        }

        var player = s.Players[s.ActivePlayerIndex];
        var card = player.Hand.FirstOrDefault(x => x.InstanceId == cmd.MonsterInstanceId);
        if (card is null)
        {
            return CommandResult.Rejected("Diese Karte hast du nicht auf der Hand.");
        }
        var def = c.Get(card.CardId);
        if (def.Kind != CardKind.Monster)
        {
            return CommandResult.Rejected("Das ist kein Monster.");
        }

        player.Hand.Remove(card);
        CombatStart.Begin(s, card);
        GameLog.Add(s, $"{player.Name} sucht Ärger: {def.Name}.");
        return CommandResult.Ok([new CombatStarted(player.Id, def.Id)]);
    }

    private static CommandResult WorkBacklogHandler(GameState s, GameRandom rng)
    {
        if (s.Phase != GamePhase.AfterTicket)
        {
            return CommandResult.Rejected("Den Backlog bearbeiten geht nur nach dem Ticket.");
        }

        var player = s.Players[s.ActivePlayerIndex];
        var events = new List<GameEvent>();
        var card = Decks.Draw(s, rng, Deck.Ticket);
        if (card is not null)
        {
            player.Hand.Add(card);
            events.Add(new CardDrawn(player.Id, Deck.Ticket));
        }
        s.Phase = GamePhase.EndTurn;
        GameLog.Add(s, $"{player.Name} bearbeitet den Backlog.");
        return CommandResult.Ok(events);
    }

    private static CommandResult EndTurnHandler(GameState s, CardCatalog c, EndTurn cmd)
    {
        if (s.Phase != GamePhase.EndTurn)
        {
            return CommandResult.Rejected("Der Zug kann jetzt noch nicht beendet werden.");
        }

        if (cmd.GiveAway is null)
        {
            return CommandResult.Rejected("Gib an, welche Karten du abgibst (eine leere Liste, wenn keine).");
        }

        var player = s.Players[s.ActivePlayerIndex];
        var excess = Math.Max(0, player.Hand.Count - HandLimit);
        var ids = cmd.GiveAway.Distinct().ToList();
        var cards = ids.Select(id => player.Hand.FirstOrDefault(x => x.InstanceId == id)).ToList();
        if (ids.Count != cmd.GiveAway.Count || ids.Count != excess || cards.Any(x => x is null))
        {
            return CommandResult.Rejected(excess == 0
                ? "Du hast keine überzähligen Karten abzugeben."
                : $"Du musst genau {excess} Karten abgeben.");
        }

        var events = new List<GameEvent>();
        if (excess > 0)
        {
            Distribute(s, c, player, cards!, events);
        }

        TemporaryEffects.ExpireAtEndOfTurn(s, player.Id);
        s.ActivePlayerIndex = (s.ActivePlayerIndex + 1) % s.Players.Count;
        s.TurnNumber++;
        s.Phase = GamePhase.OpenTicket;
        var next = s.Players[s.ActivePlayerIndex];
        events.Add(new TurnEnded(player.Id, next.Id));
        GameLog.Add(s, $"{player.Name} beendet den Zug. {next.Name} ist dran.");
        return CommandResult.Ok(events);
    }

    private static void Distribute(GameState s, CardCatalog c, PlayerState giver, List<CardInstance?> cards, List<GameEvent> events)
    {
        var others = Enumerable.Range(1, s.Players.Count - 1)
            .Select(offset => s.Players[(s.ActivePlayerIndex + offset) % s.Players.Count])
            .ToList();
        var lowest = others.Min(p => p.Level);

        foreach (var card in cards)
        {
            giver.Hand.Remove(card!);
        }

        if (giver.Level <= lowest)
        {
            foreach (var card in cards)
            {
                Decks.Discard(s, c, card!);
            }
            GameLog.Add(s, $"{giver.Name} legt {cards.Count} überzählige Karten ab.");
            return;
        }

        var receivers = others.Where(p => p.Level == lowest).ToList();
        var given = receivers.ToDictionary(p => p.Id, _ => 0);
        for (var i = 0; i < cards.Count; i++)
        {
            var receiver = receivers[i % receivers.Count];
            receiver.Hand.Add(cards[i]!);
            given[receiver.Id]++;
        }
        foreach (var receiver in receivers.Where(p => given[p.Id] > 0))
        {
            events.Add(new CardsGiven(giver.Id, receiver.Id, given[receiver.Id]));
            GameLog.Add(s, $"{giver.Name} gibt {given[receiver.Id]} Karten an {receiver.Name}.");
        }
    }
}
