using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class CommandGuardTests
{
    [Fact]
    public void UnknownActor_IsRejected()
    {
        var game = GameBuilder.Create().Build();

        var result = game.Execute(new StartGame("niemand"));

        Assert.False(result.Accepted);
        Assert.Equal("Unbekannter Spieler", result.Rejection);
    }

    [Fact]
    public void AnyCommand_AfterGameOver_IsRejected()
    {
        var game = GameBuilder.Create().Phase(GamePhase.GameOver).Build();

        var result = game.Execute(new StartGame("p1"));

        Assert.False(result.Accepted);
        Assert.Equal("Das Spiel ist vorbei.", result.Rejection);
    }

    [Fact]
    public void PendingChoice_BlocksEveryCommand_NamingThePlayer()
    {
        var game = GameBuilder.Create().Build();
        game.State.PendingChoices.Add(new PendingChoice { PlayerId = "p2", Kind = ChoiceKind.DiscardSmallItem });

        var result = game.Execute(new StartGame("p1"));

        Assert.False(result.Accepted);
        Assert.Equal("Erst muss Spieler 2 eine Auswahl treffen.", result.Rejection);
    }

    [Fact]
    public void Rejected_Command_DoesNotChangeState()
    {
        var game = GameBuilder.Create().Phase(GamePhase.OpenTicket).Build();
        var before = game.Save();

        var result = game.Execute(new StartGame("p1"));

        Assert.False(result.Accepted);
        Assert.Equal(before, game.Save());
    }

    [Fact]
    public void StartGame_MovesToOpenTicket_TurnOne()
    {
        var game = GameBuilder.Create().Build();
        var logBefore = game.State.Log.Count;

        var result = game.Execute(new StartGame("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(GamePhase.OpenTicket, game.State.Phase);
        Assert.Equal(1, game.State.TurnNumber);
        var started = Assert.IsType<GameStarted>(Assert.Single(result.Events));
        Assert.Equal(game.State.Players[game.State.ActivePlayerIndex].Id, started.StartPlayerId);
        Assert.True(game.State.Log.Count > logBefore);
    }

    [Fact]
    public void StartGame_OutsideSetup_IsRejected()
    {
        var game = GameBuilder.Create().Phase(GamePhase.EndTurn).Build();

        var result = game.Execute(new StartGame("p1"));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void AcceptedCommand_KeepsRngStateInSync()
    {
        var game = GameBuilder.Create().Build();

        game.Execute(new StartGame("p1"));

        Assert.Equal(game.Rng.State, game.State.RngState);
    }
}
