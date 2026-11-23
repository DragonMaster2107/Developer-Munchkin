using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class EconomyTests
{
    private static GameBuilder Turn() =>
        GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket);

    private static int IdOf(IEnumerable<CardInstance> cards, string cardId) =>
        cards.First(c => c.CardId == cardId).InstanceId;

    [Fact]
    public void LevelUpCard_RaisesLevel()
    {
        var game = Turn().Hand(1, "levelup-raise").Build();

        var result = game.Execute(new PlayCard("p1", IdOf(game.State.Players[0].Hand, "levelup-raise")));

        Assert.True(result.Accepted);
        Assert.Equal(2, game.State.Players[0].Level);
        Assert.Contains(result.Events, e => e is LevelChanged { PlayerId: "p1", From: 1, To: 2 });
        Assert.Empty(game.State.Players[0].Hand);
        Assert.Contains(game.State.LootDiscard, c => c.CardId == "levelup-raise");
    }

    [Fact]
    public void LevelUpCard_AtNine_IsRejected()
    {
        var game = Turn().Hand(1, "levelup-raise").Level(1, 9).Build();

        var result = game.Execute(new PlayCard("p1", IdOf(game.State.Players[0].Hand, "levelup-raise")));

        Assert.False(result.Accepted);
        Assert.Equal("Stufe 10 gibt es nur im Kampf.", result.Rejection);
        Assert.Equal(9, game.State.Players[0].Level);
        Assert.Single(game.State.Players[0].Hand);
    }

    [Fact]
    public void LevelUpCard_InOtherPlayersTurn_IsRejected()
    {
        var game = Turn().Hand(2, "levelup-raise").Build();

        var result = game.Execute(new PlayCard("p2", IdOf(game.State.Players[1].Hand, "levelup-raise")));

        Assert.False(result.Accepted);
        Assert.Equal(1, game.State.Players[1].Level);
    }

    [Fact]
    public void LevelUpCard_DuringCombat_IsRejected()
    {
        var game = Turn().Hand(1, "levelup-raise").TicketTop("monster-l1").Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);

        var result = game.Execute(new PlayCard("p1", IdOf(game.State.Players[0].Hand, "levelup-raise")));

        Assert.False(result.Accepted);
        Assert.Equal(1, game.State.Players[0].Level);
    }

    [Fact]
    public void Sell_1100Credits_GivesOneLevel_RestLost()
    {
        var game = Turn().Hand(1, "equip-ide-2h").Equip(1, "equip-feet").Backpack(1, "equip-small").Build();
        var p = game.State.Players[0];
        var ids = new[] { IdOf(p.Hand, "equip-ide-2h"), IdOf(p.Equipped, "equip-feet"), IdOf(p.Backpack, "equip-small") };

        var result = game.Execute(new SellItems("p1", ids));

        Assert.True(result.Accepted);
        Assert.Equal(2, p.Level);
        Assert.Contains(result.Events, e => e is ItemsSold { PlayerId: "p1", Credits: 1100, LevelsGained: 1 });
        Assert.Empty(p.Hand);
        Assert.Empty(p.Equipped);
        Assert.Empty(p.Backpack);
        Assert.Equal(3, game.State.LootDiscard.Count(c => ids.Contains(c.InstanceId)));
    }

    [Fact]
    public void Sell_Under1000_IsRejected()
    {
        var game = Turn().Hand(1, "equip-ide-2h", "equip-small").Build();
        var p = game.State.Players[0];

        var result = game.Execute(new SellItems("p1", p.Hand.Select(c => c.InstanceId).ToList()));

        Assert.False(result.Accepted);
        Assert.Equal(2, p.Hand.Count);
        Assert.Equal(1, p.Level);
    }

    [Fact]
    public void Sell_ToTen_IsRejected()
    {
        var game = Turn().Hand(1, "equip-ide-2h", "equip-feet").Level(1, 9).Build();
        var p = game.State.Players[0];

        var result = game.Execute(new SellItems("p1", p.Hand.Select(c => c.InstanceId).ToList()));

        Assert.False(result.Accepted);
        Assert.Equal(9, p.Level);
        Assert.Equal(2, p.Hand.Count);
    }

    [Fact]
    public void Sell_CardWithoutValue_IsRejected()
    {
        var game = Turn().Hand(1, "equip-ide-2h", "equip-feet", "monster-l1").Build();
        var p = game.State.Players[0];

        var result = game.Execute(new SellItems("p1", p.Hand.Select(c => c.InstanceId).ToList()));

        Assert.False(result.Accepted);
        Assert.Equal(3, p.Hand.Count);
    }

    [Fact]
    public void Sell_Duplicate_ForeignOrEmpty_IsRejected()
    {
        var game = Turn().Hand(1, "equip-ide-2h", "equip-feet").Equip(2, "equip-head").Build();
        var p = game.State.Players[0];
        var a = p.Hand[0].InstanceId;
        var b = p.Hand[1].InstanceId;

        Assert.False(game.Execute(new SellItems("p1", [a, a, b])).Accepted);
        Assert.False(game.Execute(new SellItems("p1", [a, b, game.State.Players[1].Equipped[0].InstanceId])).Accepted);
        Assert.False(game.Execute(new SellItems("p1", [])).Accepted);
        Assert.Equal(2, p.Hand.Count);
    }

    [Fact]
    public void Sell_DuringCombat_IsRejected()
    {
        var game = Turn().Hand(1, "equip-ide-2h", "equip-feet").TicketTop("monster-l1").Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        var p = game.State.Players[0];

        var result = game.Execute(new SellItems("p1", p.Hand.Select(c => c.InstanceId).ToList()));

        Assert.False(result.Accepted);
        Assert.Equal(1, p.Level);
    }

    [Fact]
    public void Sell_InOtherPlayersTurn_IsRejected()
    {
        var game = Turn().Hand(2, "equip-ide-2h", "equip-feet").Build();
        var p = game.State.Players[1];

        var result = game.Execute(new SellItems("p2", p.Hand.Select(c => c.InstanceId).ToList()));

        Assert.False(result.Accepted);
    }
}
