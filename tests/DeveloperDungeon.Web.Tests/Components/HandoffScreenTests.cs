using Bunit;
using DeveloperDungeon.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DeveloperDungeon.Web.Tests.Components;

public class HandoffScreenTests : BunitContext
{
    private readonly FakeTimeProvider _time = new();
    private int _reveals;

    private IRenderedComponent<HandoffScreen> RenderScreen(string name = "Ada")
    {
        Services.AddSingleton<TimeProvider>(_time);
        return Render<HandoffScreen>(p => p
            .Add(x => x.PlayerName, name)
            .Add(x => x.Color, "#123456")
            .Add(x => x.OnReveal, () => _reveals++));
    }

    [Fact]
    public void ImmediateClick_IsIgnored()
    {
        var cut = RenderScreen();

        _time.Advance(TimeSpan.FromMilliseconds(399));
        cut.Find("button").Click();

        Assert.Equal(0, _reveals);
    }

    [Fact]
    public void ClickAfter400Ms_Reveals()
    {
        var cut = RenderScreen();

        cut.Find("button").Click();
        _time.Advance(TimeSpan.FromMilliseconds(400));
        cut.Find("button").Click();

        Assert.Equal(1, _reveals);
    }

    [Fact]
    public void NewPlayer_RestartsTheGuard()
    {
        var cut = RenderScreen("Ada");
        _time.Advance(TimeSpan.FromSeconds(5));

        cut.Render(p => p.Add(x => x.PlayerName, "Bob"));
        cut.Find("button").Click();

        Assert.Equal(0, _reveals);
    }
}
