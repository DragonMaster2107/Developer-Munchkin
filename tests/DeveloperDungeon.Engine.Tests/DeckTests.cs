using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class DeckTests
{
    private static CardInstance Card(int id, string cardId = "monster-l1") => new() { InstanceId = id, CardId = cardId };

    [Fact]
    public void Draw_TakesTopCard()
    {
        var s = new GameState { TicketDeck = [Card(1), Card(2), Card(3)] };

        var drawn = Decks.Draw(s, new GameRandom(1), Deck.Ticket);

        Assert.Equal(1, drawn!.InstanceId);
        Assert.Equal([2, 3], s.TicketDeck.Select(c => c.InstanceId));
    }

    [Fact]
    public void Draw_EmptyDeck_ReshufflesDiscard()
    {
        var s = new GameState { LootDiscard = Enumerable.Range(1, 10).Select(i => Card(i, "equip-head")).ToList() };

        var drawn = Decks.Draw(s, new GameRandom(5), Deck.Loot);

        Assert.NotNull(drawn);
        Assert.Empty(s.LootDiscard);
        Assert.Equal(9, s.LootDeck.Count);
        Assert.Equal(Enumerable.Range(1, 10), s.LootDeck.Append(drawn).Select(c => c.InstanceId).Order());
    }

    [Fact]
    public void Draw_BothEmpty_ReturnsNull()
    {
        Assert.Null(Decks.Draw(new GameState(), new GameRandom(1), Deck.Ticket));
    }

    [Fact]
    public void Discard_PutsCardOnDiscardOfItsOwnDeck()
    {
        var s = new GameState();
        Decks.Discard(s, TestData.Catalog, Card(1, "monster-l1"));
        Decks.Discard(s, TestData.Catalog, Card(2, "equip-head"));

        Assert.Equal([1], s.TicketDiscard.Select(c => c.InstanceId));
        Assert.Equal([2], s.LootDiscard.Select(c => c.InstanceId));
    }
}
