using Bunit;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Web.Components;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DeveloperDungeon.Web.Tests.Components;

public class AnimationLayerTests : BunitContext
{
    private IRenderedComponent<AnimationLayer> RenderLayer(TableController c, FakeTimeProvider time, bool enabled)
    {
        Services.AddSingleton(c);
        Services.AddSingleton<TimeProvider>(time);
        return Render<AnimationLayer>(p => p.Add(x => x.Enabled, enabled));
    }

    /// <summary>Sucht einen Seed, bei dem das erste Ticket eine Animation auslöst (Monster = Kampfbeginn).</summary>
    private static async Task<(TableController Controller, FakeTimeProvider Time, IReadOnlyList<AnimationCue> Cues)> BeforeOpeningTicketAsync()
    {
        for (ulong seed = 1; seed < 500; seed++)
        {
            var time = new FakeTimeProvider();
            var c = await TestGames.StartedAsync(3, seed, null, time);
            c.ConfirmHandoff();
            var preview = await c.CheckAsync(new OpenTicket(c.View.ActivePlayerId));
            var cues = AnimationCues.SelectForPlayback(AnimationCues.From(preview.Events, c.View, c.Catalog));
            if (preview.Accepted && cues.Count > 0)
            {
                return (c, time, cues);
            }
        }
        throw new InvalidOperationException("Kein Seed mit Animation gefunden.");
    }

    [Fact]
    public async Task Disabled_RendersNothing()
    {
        var (c, time, _) = await BeforeOpeningTicketAsync();
        var cut = RenderLayer(c, time, enabled: false);

        await c.SendAsync(new OpenTicket(c.View.ActivePlayerId));

        Assert.Empty(cut.FindAll(".anim-banner"));
    }

    [Fact]
    public async Task Enabled_RendersCueText_ThenAdvancesAndClears()
    {
        var (c, time, expected) = await BeforeOpeningTicketAsync();
        var cut = RenderLayer(c, time, enabled: true);
        Assert.Empty(cut.FindAll(".anim-banner"));

        await c.SendAsync(new OpenTicket(c.View.ActivePlayerId));

        cut.WaitForAssertion(() => Assert.Equal(expected[0].Text, cut.Find(".anim-banner").TextContent.Trim()));
        foreach (var cue in expected.Skip(1))
        {
            time.Advance(TimeSpan.FromMilliseconds(1500));
            cut.WaitForAssertion(() => Assert.Equal(cue.Text, cut.Find(".anim-banner").TextContent.Trim()));
        }
        time.Advance(TimeSpan.FromMilliseconds(1500));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".anim-banner")));
    }

    [Fact]
    public async Task Disposed_MidCue_AdvancingTimeDoesNothing()
    {
        var (c, time, _) = await BeforeOpeningTicketAsync();
        var cut = RenderLayer(c, time, enabled: true);
        await c.SendAsync(new OpenTicket(c.View.ActivePlayerId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".anim-banner")));

        cut.Instance.Dispose();
        var markup = cut.Markup;
        time.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(markup, cut.Markup);
    }

    [Fact]
    public async Task DisablingMidCue_StopsPlaybackAndTimer()
    {
        var (c, time, _) = await BeforeOpeningTicketAsync();
        var cut = RenderLayer(c, time, enabled: true);
        await c.SendAsync(new OpenTicket(c.View.ActivePlayerId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".anim-banner")));

        cut.Render(p => p.Add(x => x.Enabled, false));
        time.Advance(TimeSpan.FromSeconds(30));

        Assert.Empty(cut.FindAll(".anim-banner"));
    }

    [Fact]
    public async Task UnrelatedChanges_DoNotReplayCues()
    {
        var (c, time, _) = await BeforeOpeningTicketAsync();
        var cut = RenderLayer(c, time, enabled: true);
        await c.SendAsync(new OpenTicket(c.View.ActivePlayerId));
        time.Advance(TimeSpan.FromSeconds(30));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".anim-banner")));

        c.RequestDevice(c.View.ActivePlayerId);
        c.ConfirmHandoff();

        Assert.Empty(cut.FindAll(".anim-banner"));
    }
}
