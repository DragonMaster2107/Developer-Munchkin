using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.Time.Testing;

namespace DeveloperDungeon.Web.Tests.Logic;

public class TableControllerCombatTests
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task RequestResolution_AfterCountdown_ResolvesCombat()
    {
        var time = new FakeTimeProvider();
        var (c, _) = await TestGames.InCombatAsync(time);

        await c.RequestCombatResolutionAsync(3);

        Assert.True(c.CountdownRunning);
        Assert.Equal(3, c.CountdownRemaining);
        time.Advance(Second);
        time.Advance(Second);
        Assert.Equal(CombatStage.Fighting, c.View.Combat!.State.Stage);
        time.Advance(Second);

        Assert.False(c.CountdownRunning);
        Assert.True(c.View.Combat is null || c.View.Combat.State.Stage == CombatStage.Fleeing || c.View.Phase != GamePhase.Combat);
        Assert.NotEmpty(c.LastEvents);
    }

    [Fact]
    public async Task Interjection_CancelsCountdown()
    {
        var time = new FakeTimeProvider();
        var (c, _) = await TestGames.InCombatAsync(time);
        var other = c.View.Players.First(p => p.Id != c.ActorId).Id;
        await c.RequestCombatResolutionAsync(3);

        c.BeginInterjection(other);
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.False(c.CountdownRunning);
        Assert.Equal(GamePhase.Combat, c.View.Phase);
        Assert.Equal(CombatStage.Fighting, c.View.Combat!.State.Stage);
    }

    [Fact]
    public async Task ResolvingCombat_WhileInterjecting_ReturnsDeviceToActivePlayer()
    {
        var time = new FakeTimeProvider();
        var (c, _) = await TestGames.InCombatAsync(time);
        var active = c.View.ActivePlayerId;
        c.BeginInterjection(c.View.Players.First(p => p.Id != active).Id);
        c.ConfirmHandoff();

        await c.RequestCombatResolutionAsync(2);
        time.Advance(Second);
        time.Advance(Second);

        Assert.Null(c.InterjectorId);
        Assert.Equal(active, c.HandoffTargetId);
        Assert.False(c.CountdownRunning);
        Assert.NotEqual(CombatStage.Fighting, c.View.Combat?.State.Stage ?? CombatStage.Fleeing);
    }

    [Fact]
    public async Task RequestResolution_ByNonActiveDevice_IsIgnored()
    {
        var time = new FakeTimeProvider();
        var (c, _) = await TestGames.InCombatAsync(time);
        c.RequestDevice(c.View.Players.First(p => p.Id != c.View.ActivePlayerId).Id);
        c.ConfirmHandoff();

        await c.RequestCombatResolutionAsync(2);
        await c.RequestCombatResolutionAsync(0);
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.False(c.CountdownRunning);
        Assert.Equal(CombatStage.Fighting, c.View.Combat!.State.Stage);
    }
}
