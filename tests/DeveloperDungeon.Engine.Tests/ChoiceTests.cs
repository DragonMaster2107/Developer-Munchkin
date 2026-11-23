using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class ChoiceTests
{
    private static int IdOf(IEnumerable<CardInstance> cards, string cardId) =>
        cards.First(c => c.CardId == cardId).InstanceId;

    private static GameBuilder Turn() =>
        GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket);

    private static CommandResult PlayGiveCurse(Game game, int target)
    {
        var id = IdOf(game.State.Players[0].Hand, "curse-give-item");
        return game.Execute(new PlayCard("p1", id, TargetPlayerId: $"p{target}"));
    }

    [Fact]
    public void GiveItemCurse_CreatesChoice_ForTarget()
    {
        var game = Turn().Hand(1, "curse-give-item").Equip(2, "equip-body").Build();

        var result = PlayGiveCurse(game, 2);

        Assert.True(result.Accepted);
        var choice = Assert.Single(game.State.PendingChoices);
        Assert.Equal("p2", choice.PlayerId);
        Assert.Equal(ChoiceKind.GiveEquippedItem, choice.Kind);
        Assert.Equal("p3", choice.RecipientPlayerId);
        Assert.Equal("curse-give-item", choice.SourceCardId);
        Assert.Contains(result.Events, e => e is ChoiceRequested { PlayerId: "p2", Kind: ChoiceKind.GiveEquippedItem });
    }

    [Fact]
    public void GiveItemCurse_WithNothingEquipped_Fizzles()
    {
        var game = Turn().Hand(1, "curse-give-item").Build();

        var result = PlayGiveCurse(game, 2);

        Assert.True(result.Accepted);
        Assert.Empty(game.State.PendingChoices);
    }

    [Fact]
    public void LoseSmallItem_WithoutSmallItemWorn_Fizzles_WithSmallItem_CreatesChoice()
    {
        var game = Turn().Equip(1, "equip-body").Equip(2, "equip-small").Build();

        ApplyLoseSmallItem(game, "p1");
        Assert.Empty(game.State.PendingChoices);
        ApplyLoseSmallItem(game, "p2");
        var choice = Assert.Single(game.State.PendingChoices);
        Assert.Equal(ChoiceKind.DiscardSmallItem, choice.Kind);
        Assert.Equal("p2", choice.PlayerId);
    }

    private static void ApplyLoseSmallItem(Game game, string player) =>
        EffectApplier.ApplyToPlayer(game.State, game.Catalog, game.Rng, player, [new LoseSmallItem()], "x", []);

    [Fact]
    public void PendingChoice_BlocksOtherCommands()
    {
        var game = Turn().Hand(1, "curse-give-item", "equip-head").Equip(2, "equip-body").Build();
        PlayGiveCurse(game, 2);

        var result = game.Execute(new EquipCard("p1", IdOf(game.State.Players[0].Hand, "equip-head")));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void ResolveChoice_ByWrongPlayer_IsRejected()
    {
        var game = Turn().Hand(1, "curse-give-item").Equip(2, "equip-body").Build();
        PlayGiveCurse(game, 2);
        var itemId = IdOf(game.State.Players[1].Equipped, "equip-body");

        var result = game.Execute(new ResolveChoice("p1", itemId));

        Assert.False(result.Accepted);
        Assert.Single(game.State.PendingChoices);
    }

    [Fact]
    public void ResolveChoice_GiveItem_MovesItemToLeftNeighborsBackpack()
    {
        var game = Turn().Hand(1, "curse-give-item").Equip(3, "equip-body").Build();
        PlayGiveCurse(game, 3);
        var itemId = IdOf(game.State.Players[2].Equipped, "equip-body");

        var result = game.Execute(new ResolveChoice("p3", itemId));

        Assert.True(result.Accepted);
        Assert.Empty(game.State.Players[2].Equipped);
        Assert.Contains(game.State.Players[0].Backpack, c => c.InstanceId == itemId);
        Assert.Empty(game.State.PendingChoices);
        Assert.Contains(result.Events, e => e is ChoiceResolved { PlayerId: "p3" } r && r.InstanceId == itemId);
    }

    [Fact]
    public void ResolveChoice_InvalidCard_IsRejected()
    {
        var game = Turn().Hand(1, "curse-give-item", "equip-head").Equip(2, "equip-body").Build();
        PlayGiveCurse(game, 2);
        var handCard = IdOf(game.State.Players[0].Hand, "equip-head");

        var result = game.Execute(new ResolveChoice("p2", handCard));

        Assert.False(result.Accepted);
        Assert.Single(game.State.PendingChoices);
        Assert.Single(game.State.Players[1].Equipped);
    }

    [Fact]
    public void ResolveChoice_DiscardSmallItem_RequiresSmallItem_AndDiscards()
    {
        var game = Turn().Equip(2, "equip-small", "equip-body").Build();
        ApplyLoseSmallItem(game, "p2");
        var body = IdOf(game.State.Players[1].Equipped, "equip-body");
        var small = IdOf(game.State.Players[1].Equipped, "equip-small");

        Assert.False(game.Execute(new ResolveChoice("p2", body)).Accepted);
        var result = game.Execute(new ResolveChoice("p2", small));

        Assert.True(result.Accepted);
        Assert.Contains(game.State.LootDiscard, c => c.InstanceId == small);
        Assert.Empty(game.State.PendingChoices);
    }

    [Fact]
    public void ResolveChoice_WithoutPendingChoice_IsRejected()
    {
        var game = Turn().Equip(2, "equip-body").Build();

        var result = game.Execute(new ResolveChoice("p2", IdOf(game.State.Players[1].Equipped, "equip-body")));

        Assert.False(result.Accepted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void QueuedChoice_WithoutRemainingOption_IsDroppedAfterFirstResolve(bool secondIsGive)
    {
        var game = Turn().Hand(1, "equip-head").Equip(2, "equip-small").Build();
        Effect second = secondIsGive ? new GiveEquippedItemToNeighbor(Direction.Left) : new LoseSmallItem();
        EffectApplier.ApplyToPlayer(game.State, game.Catalog, game.Rng, "p2", [new LoseSmallItem(), second], "x", []);
        Assert.Equal(2, game.State.PendingChoices.Count);

        var small = IdOf(game.State.Players[1].Equipped, "equip-small");
        Assert.True(game.Execute(new ResolveChoice("p2", small)).Accepted);

        Assert.Empty(game.State.PendingChoices);
        var head = IdOf(game.State.Players[0].Hand, "equip-head");
        Assert.True(game.Execute(new EquipCard("p1", head)).Accepted);
    }
}
