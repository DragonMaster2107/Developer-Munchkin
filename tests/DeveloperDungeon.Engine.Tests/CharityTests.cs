using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class CharityTests
{
    private static readonly string[] Seven =
        ["equip-head", "equip-body", "equip-feet", "equip-ide-1h", "equip-ide-2h", "equip-big", "equip-small"];

    private static GameBuilder SevenCards(int players = 3) =>
        GameBuilder.Create(players).ClearHands().Active(1).Hand(1, Seven).Phase(GamePhase.EndTurn);

    private static int[] Ids(Game g, int count) => g.State.Players[0].Hand.Take(count).Select(c => c.InstanceId).ToArray();

    [Fact]
    public void EndTurn_WithSevenCards_RequiresExactlyTwo()
    {
        var game = SevenCards().Level(1, 5).Build();

        var one = game.Execute(new EndTurn("p1", Ids(game, 1)));
        var three = game.Execute(new EndTurn("p1", Ids(game, 3)));

        Assert.False(one.Accepted);
        Assert.Equal("Du musst genau 2 Karten abgeben.", one.Rejection);
        Assert.False(three.Accepted);
        Assert.Equal(7, game.State.Players[0].Hand.Count);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);

        var two = game.Execute(new EndTurn("p1", Ids(game, 2)));

        Assert.True(two.Accepted);
        Assert.Equal(5, game.State.Players[0].Hand.Count);
    }

    [Fact]
    public void EndTurn_WithDuplicateGiveAway_IsRejected()
    {
        var game = SevenCards().Level(1, 5).Build();
        var id = game.State.Players[0].Hand[0].InstanceId;

        var result = game.Execute(new EndTurn("p1", [id, id]));

        Assert.False(result.Accepted);
        Assert.Equal(7, game.State.Players[0].Hand.Count);
    }

    [Fact]
    public void Excess_GoesToLowestLevelPlayer()
    {
        var game = SevenCards().Level(1, 5).Level(2, 3).Level(3, 4).Build();
        var ids = Ids(game, 2);

        var result = game.Execute(new EndTurn("p1", ids));

        Assert.True(result.Accepted);
        Assert.Equal(ids.Order(), game.State.Players[1].Hand.Select(c => c.InstanceId).Order());
        Assert.Empty(game.State.Players[2].Hand);
        Assert.Contains(result.Events, e => e is CardsGiven { FromId: "p1", ToId: "p2", Count: 2 });
    }

    [Fact]
    public void Excess_SplitAmongTiedLowest_RoundRobin()
    {
        var game = SevenCards(4).Level(1, 5).Level(2, 2).Level(3, 4).Level(4, 2).Build();
        var ids = Ids(game, 2);

        game.Execute(new EndTurn("p1", ids));

        Assert.Equal([ids[0]], game.State.Players[1].Hand.Select(c => c.InstanceId));
        Assert.Equal([ids[1]], game.State.Players[3].Hand.Select(c => c.InstanceId));
        Assert.Empty(game.State.Players[2].Hand);
    }

    [Fact]
    public void Excess_DiscardedWhenActiveIsLowest()
    {
        var game = SevenCards().Level(1, 1).Level(2, 3).Level(3, 4).Build();
        var ids = Ids(game, 2);

        var result = game.Execute(new EndTurn("p1", ids));

        Assert.True(result.Accepted);
        Assert.Empty(game.State.Players[1].Hand);
        Assert.Empty(game.State.Players[2].Hand);
        Assert.Equal(2, game.State.LootDiscard.Count(c => ids.Contains(c.InstanceId)));
    }

    [Fact]
    public void Excess_DiscardedWhenActiveTiesLowest()
    {
        var game = SevenCards().Level(1, 2).Level(2, 2).Level(3, 4).Build();
        var ids = Ids(game, 2);

        game.Execute(new EndTurn("p1", ids));

        Assert.Empty(game.State.Players[1].Hand);
        Assert.Equal(2, game.State.LootDiscard.Count(c => ids.Contains(c.InstanceId)));
    }

    [Fact]
    public void GiveAway_WithFiveCards_MustBeEmpty()
    {
        var game = GameBuilder.Create().ClearHands().Active(1)
            .Hand(1, Seven[..5]).Phase(GamePhase.EndTurn).Build();

        var result = game.Execute(new EndTurn("p1", Ids(game, 1)));

        Assert.False(result.Accepted);
        Assert.Equal(5, game.State.Players[0].Hand.Count);
        Assert.True(game.Execute(new EndTurn("p1", [])).Accepted);
    }
}
