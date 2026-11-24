using DeveloperDungeon.Web.Logic;
using Microsoft.Extensions.Time.Testing;

namespace DeveloperDungeon.Web.Tests.Logic;

public class CombatCountdownTests
{
    [Fact]
    public void Start_TicksDownAndElapses()
    {
        var time = new FakeTimeProvider();
        using var countdown = new CombatCountdown(time);
        var ticks = 0;
        var elapsed = 0;
        countdown.Tick += () => ticks++;
        countdown.Elapsed += () => elapsed++;

        countdown.Start(5);
        Assert.True(countdown.IsRunning);
        Assert.Equal(5, countdown.RemainingSeconds);

        for (var i = 1; i <= 5; i++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(5 - i, countdown.RemainingSeconds);
        }

        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(1, elapsed);
        Assert.Equal(5, ticks);
        Assert.False(countdown.IsRunning);
    }

    [Fact]
    public void StartZero_ElapsesImmediately()
    {
        using var countdown = new CombatCountdown(new FakeTimeProvider());
        var elapsed = 0;
        countdown.Elapsed += () => elapsed++;

        countdown.Start(0);

        Assert.Equal(1, elapsed);
        Assert.False(countdown.IsRunning);
    }

    [Fact]
    public void Cancel_PreventsElapsed()
    {
        var time = new FakeTimeProvider();
        using var countdown = new CombatCountdown(time);
        var elapsed = 0;
        countdown.Elapsed += () => elapsed++;

        countdown.Start(3);
        time.Advance(TimeSpan.FromSeconds(1));
        countdown.Cancel();
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(0, elapsed);
        Assert.False(countdown.IsRunning);
    }
}
