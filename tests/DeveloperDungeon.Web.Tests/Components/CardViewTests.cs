using Microsoft.Extensions.DependencyInjection;
using Bunit;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Web.Components;
using DeveloperDungeon.Web.Logic;

namespace DeveloperDungeon.Web.Tests.Components;

public class CardViewTests : BunitContext
{
    private static readonly CardCatalog Cards = CardCatalog.LoadEmbedded();

    public CardViewTests()
    {
        Services.AddSingleton(CardArtCatalog.Parse("""{ "monster-works-on-my-machine": "svg" }"""));
    }

    private IRenderedComponent<CardView> RenderCard(string id, Action<ComponentParameterCollectionBuilder<CardView>>? more = null) =>
        Render<CardView>(p =>
        {
            p.Add(c => c.Card, Cards.Get(id));
            more?.Invoke(p);
        });

    [Fact]
    public void Monster_ShowsLevelBadgeAndRewards()
    {
        var cut = RenderCard("monster-works-on-my-machine");

        Assert.Contains("Stufe 3", cut.Markup);
        Assert.Contains("1 Stufe", cut.Markup);
        Assert.Contains("1 Loot", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".card--monster"));
        Assert.Equal("cards/monster-works-on-my-machine.svg", cut.Find("img").GetAttribute("src"));
        Assert.Equal(Cards.Get("monster-works-on-my-machine").Name, cut.Find("img").GetAttribute("alt"));
    }

    [Fact]
    public void Equipment_ShowsSlotBonusAndValue()
    {
        var cut = RenderCard("equip-visual-studio");

        Assert.Contains("2 Hände", cut.Markup);
        Assert.Contains("+4", cut.Markup);
        Assert.Contains("800 Credits", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".card--loot"));
    }

    [Fact]
    public void Curse_HasCurseFrame()
    {
        var cut = RenderCard("curse-unlocked-pc");

        Assert.NotEmpty(cut.FindAll(".card--curse"));
        Assert.Contains("Sofort", cut.Markup);
    }

    [Fact]
    public void Disabled_ShowsReason()
    {
        var clicked = false;
        var cut = RenderCard("oneshot-cold-coffee", p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.DisabledReason, "Nicht im Kampf")
            .Add(c => c.OnClick, () => clicked = true));

        Assert.Contains("Nicht im Kampf", cut.Markup);
        Assert.Equal("Nicht im Kampf", cut.Find(".card").GetAttribute("title"));
        cut.Find(".card").Click();
        Assert.False(clicked);
    }

    [Fact]
    public void Click_InvokesCallback()
    {
        var clicked = false;
        var cut = RenderCard("oneshot-cold-coffee", p => p.Add(c => c.OnClick, () => clicked = true));

        cut.Find(".card").Click();

        Assert.True(clicked);
    }

    [Theory]
    [InlineData(1, "1 Stufe")]
    [InlineData(2, "2 Stufen")]
    public void CardTexts_PluralizesLevels(int levels, string expected)
    {
        var custom = new CardDefinition { Id = "x", Name = "X", Deck = Deck.Ticket, Kind = CardKind.Monster, Level = 4, RewardLevels = levels, RewardLoot = 2 };

        Assert.Equal(expected, CardTexts.FooterLeft(custom));
        Assert.Equal("2 Loot", CardTexts.FooterRight(custom));
    }
}
