using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Sessions;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class PreviewTests
{
    private static Game Turn() =>
        GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l1").Build();

    [Fact]
    public void Preview_LegalCommand_IsAccepted_AndLeavesGameUntouched()
    {
        var game = Turn();
        var before = game.Save();

        var result = game.Preview(new OpenTicket("p1"));

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, e => e is Events.CombatStarted);
        Assert.Equal(before, game.Save());
        Assert.Equal(GamePhase.OpenTicket, game.State.Phase);
    }

    [Fact]
    public void Preview_IllegalCommand_ReturnsSameRejectionAsExecute()
    {
        var game = Turn();

        var preview = game.Preview(new OpenTicket("p2"));
        var executed = game.Execute(new OpenTicket("p2"));

        Assert.False(preview.Accepted);
        Assert.Equal(executed.Rejection, preview.Rejection);
    }

    [Fact]
    public void Preview_ThenExecute_GivesSameResult()
    {
        var game = Turn();

        var preview = game.Preview(new OpenTicket("p1"));
        var executed = game.Execute(new OpenTicket("p1"));

        Assert.Equal(preview.Events, executed.Events);
    }

    [Fact]
    public async Task CheckAsync_DoesNotChangeStateSaveOrNotify()
    {
        var game = Turn();
        var before = game.Save();
        var raised = 0;
        var saves = 0;
        var session = new LocalGameSession(game, _ => saves++);
        session.StateChanged += _ => raised++;

        var legal = await session.CheckAsync(new OpenTicket("p1"));
        var illegal = await session.CheckAsync(new OpenTicket("p3"));

        Assert.True(legal.Accepted);
        Assert.False(illegal.Accepted);
        Assert.Equal("Du bist nicht am Zug.", illegal.Rejection);
        Assert.Equal(0, raised);
        Assert.Equal(0, saves);
        Assert.Equal(before, game.Save());
    }
}
