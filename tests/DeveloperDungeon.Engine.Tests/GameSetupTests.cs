using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class GameSetupTests
{
    private static List<PlayerSetup> Setups(int n) =>
        Enumerable.Range(1, n).Select(i => new PlayerSetup($"Spieler {i}", $"farbe{i}")).ToList();

    private static IEnumerable<CardInstance> AllInstances(GameState s) =>
        s.TicketDeck.Concat(s.TicketDiscard).Concat(s.LootDeck).Concat(s.LootDiscard)
            .Concat(s.Players.SelectMany(p => p.Hand.Concat(p.Equipped).Concat(p.Backpack).Concat(p.Departments).Concat(p.Languages)));

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    public void New_RejectsTooFewOrTooManyPlayers(int count)
    {
        Assert.Throws<ArgumentException>(() => Game.New(Setups(count), TestData.Catalog, 1));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void New_DealsFourTicketAndFourLootToEachPlayer_AtLevelOne(int count)
    {
        var game = Game.New(Setups(count), TestData.Catalog, 1);
        var s = game.State;

        Assert.Equal(Enumerable.Range(1, count).Select(i => $"p{i}"), s.Players.Select(p => p.Id));
        Assert.Equal(GamePhase.Setup, s.Phase);
        Assert.Equal(0, s.TurnNumber);
        Assert.InRange(s.ActivePlayerIndex, 0, count - 1);
        foreach (var p in s.Players)
        {
            Assert.Equal(1, p.Level);
            Assert.Equal(4, p.Hand.Count(c => game.Catalog.Get(c.CardId).Deck == Deck.Ticket));
            Assert.Equal(4, p.Hand.Count(c => game.Catalog.Get(c.CardId).Deck == Deck.Loot));
        }
        Assert.Contains("Neues Spiel", s.Log[0].Text);
    }

    [Fact]
    public void New_IsDeterministicForSeed()
    {
        var a = Game.New(Setups(4), TestData.Catalog, 9).Save();
        var b = Game.New(Setups(4), TestData.Catalog, 9).Save();
        var c = Game.New(Setups(4), TestData.Catalog, 10).Save();
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void New_ExpandsCardCountsIntoUniqueInstances()
    {
        var game = Game.New(Setups(3), TestData.Catalog, 3);
        var all = AllInstances(game.State).ToList();

        Assert.Equal(TestData.Catalog.All.Sum(c => c.Count), all.Count);
        Assert.Equal(all.Count, all.Select(c => c.InstanceId).Distinct().Count());
    }
}
