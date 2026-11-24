using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Tests.Support;

namespace DeveloperDungeon.Web.Tests.Logic;

public class TurnActionsTests
{
    [Fact]
    public async Task OpenTicketPhase_OffersOpenTicket()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var active = c.View.ActivePlayerId;

        var actions = TurnActions.For(c.View, active);

        var action = Assert.Single(actions);
        Assert.Equal("Ticket öffnen", action.Label);
        Assert.Equal(new OpenTicket(active), action.Command);
    }

    [Fact]
    public async Task AfterTicket_OffersWorkBacklog()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var active = c.View.ActivePlayerId;
        for (ulong seed = 1; c.View.Phase != GamePhase.AfterTicket; seed++)
        {
            c = await TestGames.StartedAsync(3, seed);
            c.ConfirmHandoff();
            active = c.View.ActivePlayerId;
            await c.SendAsync(new OpenTicket(active));
        }

        var action = Assert.Single(TurnActions.For(c.View, active));

        Assert.Equal("Backlog abarbeiten", action.Label);
        Assert.Equal(new WorkBacklog(active), action.Command);
    }

    [Fact]
    public async Task NonActiveActor_GetsNoTurnActions()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var other = c.View.Players.First(p => p.Id != c.View.ActivePlayerId).Id;

        Assert.Empty(TurnActions.For(c.View, other));
    }

    [Fact]
    public async Task EndTurnPhase_WithSmallHand_OffersEndTurn()
    {
        var c = await TestGames.AtEndTurnAsync(new InMemoryGameStorage());
        var active = c.View.ActivePlayerId;
        await HandSizing.SetHandSizeAsync(c, 5);

        var action = Assert.Single(TurnActions.For(c.View, active));

        Assert.Equal("Feierabend", action.Label);
        var endTurn = Assert.IsType<EndTurn>(action.Command);
        Assert.Equal(active, endTurn.ActorId);
        Assert.Empty(endTurn.GiveAway);
    }
}
