using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class Decks
{
    /// <summary>Zieht die oberste Karte (Index 0). Ist der Stapel leer, wird die Ablage gemischt zum Stapel; sind beide leer, ist das Ergebnis null.</summary>
    public static CardInstance? Draw(GameState s, GameRandom rng, Deck deck)
    {
        var (pile, discard) = deck == Deck.Ticket
            ? (s.TicketDeck, s.TicketDiscard)
            : (s.LootDeck, s.LootDiscard);

        if (pile.Count == 0)
        {
            if (discard.Count == 0)
            {
                return null;
            }
            pile.AddRange(discard);
            discard.Clear();
            rng.Shuffle(pile);
        }

        var card = pile[0];
        pile.RemoveAt(0);
        return card;
    }

    public static void Discard(GameState s, CardCatalog c, CardInstance card)
    {
        var target = c.Get(card.CardId).Deck == Deck.Ticket ? s.TicketDiscard : s.LootDiscard;
        target.Add(card);
    }
}
