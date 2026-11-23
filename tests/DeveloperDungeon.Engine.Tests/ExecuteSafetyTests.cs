using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class ExecuteSafetyTests
{
    private static GameBuilder Turn() =>
        GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket);

    [Fact]
    public void InternalException_IsRejected_AndStateIsRestored()
    {
        var game = Turn().Build();
        game.State.TicketDeck[0].CardId = "card-that-does-not-exist";
        var before = game.Save();

        var result = game.Execute(new OpenTicket("p1"));

        Assert.False(result.Accepted);
        Assert.StartsWith("Interner Fehler", result.Rejection);
        Assert.Contains("card-that-does-not-exist", result.Rejection);
        Assert.Empty(result.Events);
        Assert.Equal(before, game.Save());
    }

    [Fact]
    public void AfterInternalException_GameKeepsWorking()
    {
        var game = Turn().TicketTop("monster-l1").Hand(2, "equip-body").Build();
        var card = game.State.Players[1].Hand[0];
        card.CardId = "card-that-does-not-exist";

        // Move entfernt die Karte, bevor es den (unbekannten) Kartennamen nachschlägt.
        Assert.False(game.Execute(new ManualMoveCard("p1", card.InstanceId, "p1", Zone.Hand)).Accepted);

        Assert.Empty(game.State.Players[0].Hand);
        var restored = Assert.Single(game.State.Players[1].Hand);
        restored.CardId = "equip-body";
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        Assert.Equal(GamePhase.Combat, game.State.Phase);
    }

    [Fact]
    public void EndTurn_WithNullGiveAway_IsRejectedWithReason()
    {
        var game = Turn().Phase(GamePhase.EndTurn).Build();
        var before = game.Save();

        var result = game.Execute(new EndTurn("p1", null!));

        Assert.False(result.Accepted);
        Assert.DoesNotContain("Interner Fehler", result.Rejection);
        Assert.Equal(before, game.Save());
    }

    [Fact]
    public void SellItems_WithNullInstanceIds_IsRejectedWithReason()
    {
        var game = Turn().Phase(GamePhase.AfterTicket).Build();
        var before = game.Save();

        var result = game.Execute(new SellItems("p1", null!));

        Assert.False(result.Accepted);
        Assert.DoesNotContain("Interner Fehler", result.Rejection);
        Assert.Equal(before, game.Save());
    }
}
