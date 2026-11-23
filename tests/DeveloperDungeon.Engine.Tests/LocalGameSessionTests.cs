using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Sessions;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class LocalGameSessionTests
{
    [Fact]
    public async Task AcceptedCommand_RaisesStateChanged_AndSaves()
    {
        var game = GameBuilder.Create().Active(1).Build();
        var order = new List<string>();
        string? saved = null;
        var session = new LocalGameSession(game, json => { saved = json; order.Add("saved"); });
        IReadOnlyList<Events.GameEvent>? received = null;
        session.StateChanged += events => { received = events; order.Add("changed"); };

        var result = await session.SendAsync(new StartGame("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(["saved", "changed"], order);
        Assert.Same(result.Events, received);
        Assert.Contains(received!, e => e is Events.GameStarted);
        var loaded = Game.Load(saved!, TestData.Catalog);
        Assert.Equal(game.State.Phase, loaded.State.Phase);
        Assert.Same(game.Catalog, session.Catalog);
    }

    [Fact]
    public async Task RejectedCommand_RaisesNothing()
    {
        var game = GameBuilder.Create().Active(1).Build();
        var raised = 0;
        var saves = 0;
        var session = new LocalGameSession(game, _ => saves++);
        session.StateChanged += _ => raised++;

        var result = await session.SendAsync(new OpenTicket("p2"));

        Assert.False(result.Accepted);
        Assert.Equal(0, raised);
        Assert.Equal(0, saves);
    }
}
