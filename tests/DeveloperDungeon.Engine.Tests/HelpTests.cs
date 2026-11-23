using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class HelpTests
{
    private static Game Fight()
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l3-legacy").Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        return game;
    }

    [Fact]
    public void OfferAndAccept_SetsHelper()
    {
        var game = Fight();

        var offer = game.Execute(new OfferHelp("p2", 1));
        var accept = game.Execute(new AcceptHelp("p1", "p2"));

        Assert.True(offer.Accepted);
        Assert.Contains(offer.Events, e => e is HelpOffered { PlayerId: "p2", LootShare: 1 });
        Assert.True(accept.Accepted);
        Assert.Contains(accept.Events, e => e is HelpAccepted { HelperId: "p2" });
        Assert.Equal("p2", game.State.Combat!.HelperId);
        Assert.Equal(1, game.State.Combat.HelperLootShare);
        Assert.Empty(game.State.Combat.Offers);
    }

    [Fact]
    public void ActivePlayer_CannotOfferHelp()
    {
        var game = Fight();

        Assert.False(game.Execute(new OfferHelp("p1", 0)).Accepted);
        Assert.Empty(game.State.Combat!.Offers);
    }

    [Fact]
    public void NegativeLootShare_IsRejected()
    {
        var game = Fight();

        Assert.False(game.Execute(new OfferHelp("p2", -1)).Accepted);
    }

    [Fact]
    public void Accept_WithoutOffer_IsRejected()
    {
        var game = Fight();

        Assert.False(game.Execute(new AcceptHelp("p1", "p2")).Accepted);
        Assert.Null(game.State.Combat!.HelperId);
    }

    [Fact]
    public void Accept_ByNonActive_IsRejected()
    {
        var game = Fight();
        game.Execute(new OfferHelp("p2", 0));

        Assert.False(game.Execute(new AcceptHelp("p3", "p2")).Accepted);
    }

    [Fact]
    public void SecondHelper_IsRejected()
    {
        var game = Fight();
        game.Execute(new OfferHelp("p2", 0));
        game.Execute(new OfferHelp("p3", 0));
        game.Execute(new AcceptHelp("p1", "p2"));

        Assert.False(game.Execute(new AcceptHelp("p1", "p3")).Accepted);
        Assert.False(game.Execute(new OfferHelp("p3", 0)).Accepted);
        Assert.Equal("p2", game.State.Combat!.HelperId);
    }

    [Fact]
    public void NewOffer_ReplacesOldOfferOfSamePlayer()
    {
        var game = Fight();
        game.Execute(new OfferHelp("p2", 1));
        game.Execute(new OfferHelp("p2", 2));

        var offer = Assert.Single(game.State.Combat!.Offers);
        Assert.Equal(2, offer.LootShare);
    }

    [Fact]
    public void Offer_OutsideCombat_IsRejected()
    {
        var game = GameBuilder.Create().Active(1).Phase(GamePhase.OpenTicket).Build();

        Assert.False(game.Execute(new OfferHelp("p2", 0)).Accepted);
    }
}
