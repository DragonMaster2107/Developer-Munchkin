using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class EquipRulesTests
{
    private static int IdOf(IEnumerable<CardInstance> cards, string cardId) =>
        cards.First(c => c.CardId == cardId).InstanceId;

    private static int InHand(Game g, string cardId, int p = 1) => IdOf(g.State.Players[p - 1].Hand, cardId);

    private static GameBuilder Turn(params string[] hand) =>
        GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).Hand(1, hand);

    private static List<int> AllInHand(Game g, string cardId) =>
        g.State.Players[0].Hand.Where(c => c.CardId == cardId).Select(c => c.InstanceId).ToList();

    [Fact]
    public void Equip_Head_Twice_IsRejected()
    {
        var game = Turn("equip-head", "equip-head").Build();
        var ids = AllInHand(game, "equip-head");

        Assert.True(game.Execute(new EquipCard("p1", ids[0])).Accepted);
        var result = game.Execute(new EquipCard("p1", ids[1]));

        Assert.False(result.Accepted);
        Assert.Single(game.State.Players[0].Equipped);
    }

    [Fact]
    public void Equip_TwoOneHandedIdes_Allowed_ThirdRejected()
    {
        var game = Turn("equip-ide-1h", "equip-ide-1h", "equip-ide-1h").Build();
        var ids = AllInHand(game, "equip-ide-1h");

        Assert.True(game.Execute(new EquipCard("p1", ids[0])).Accepted);
        Assert.True(game.Execute(new EquipCard("p1", ids[1])).Accepted);
        var third = game.Execute(new EquipCard("p1", ids[2]));

        Assert.False(third.Accepted);
        Assert.Equal(2, game.State.Players[0].Equipped.Count);
    }

    [Fact]
    public void Equip_TwoHandedPlusOneHanded_IsRejected()
    {
        var game = Turn("equip-ide-2h", "equip-ide-1h").Build();

        Assert.True(game.Execute(new EquipCard("p1", InHand(game, "equip-ide-2h"))).Accepted);
        var result = game.Execute(new EquipCard("p1", InHand(game, "equip-ide-1h")));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Equip_QaOnly_WithoutQa_IsRejected_WithQa_Allowed()
    {
        var without = Turn("equip-qa-only").Build();
        var rejected = without.Execute(new EquipCard("p1", InHand(without, "equip-qa-only")));
        Assert.False(rejected.Accepted);
        Assert.Equal("Nur für dept-qa. Du hast diese Abteilung nicht.", rejected.Rejection);

        var with = Turn("equip-qa-only").Department(1, "dept-qa").Build();
        Assert.True(with.Execute(new EquipCard("p1", InHand(with, "equip-qa-only"))).Accepted);
    }

    [Fact]
    public void Equip_SecondDepartment_RejectedUnlessFullStack()
    {
        var game = Turn("dept-qa", "dept-cloud", "special-full-stack").Department(1, "dept-team-assistant").Build();
        Assert.False(game.Execute(new EquipCard("p1", InHand(game, "dept-qa"))).Accepted);

        Assert.True(game.Execute(new EquipCard("p1", InHand(game, "special-full-stack"))).Accepted);
        Assert.True(game.Execute(new EquipCard("p1", InHand(game, "dept-qa"))).Accepted);
        Assert.False(game.Execute(new EquipCard("p1", InHand(game, "dept-cloud"))).Accepted);
        Assert.Equal(2, game.State.Players[0].Departments.Count);
    }

    [Fact]
    public void Equip_SecondLanguage_RejectedUnlessPolyglot()
    {
        var game = Turn("lang-csharp", "special-polyglot").Language(1, "lang-python").Build();
        Assert.False(game.Execute(new EquipCard("p1", InHand(game, "lang-csharp"))).Accepted);

        Assert.True(game.Execute(new EquipCard("p1", InHand(game, "special-polyglot"))).Accepted);
        Assert.True(game.Execute(new EquipCard("p1", InHand(game, "lang-csharp"))).Accepted);
        Assert.Equal(2, game.State.Players[0].Languages.Count);
    }

    [Fact]
    public void Equip_InOtherPlayersTurn_IsRejected()
    {
        var game = GameBuilder.Create().ClearHands().Active(2).Phase(GamePhase.OpenTicket).Hand(1, "equip-head").Build();

        var result = game.Execute(new EquipCard("p1", InHand(game, "equip-head")));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Equip_DuringCombat_IsRejected()
    {
        var game = Turn("equip-head").Phase(GamePhase.Combat).Build();

        var result = game.Execute(new EquipCard("p1", InHand(game, "equip-head")));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Equip_InSetup_AllowedForAnyPlayer()
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Hand(2, "equip-head").Build();

        var result = game.Execute(new EquipCard("p2", InHand(game, "equip-head", 2)));

        Assert.True(result.Accepted);
        Assert.IsType<CardEquipped>(Assert.Single(result.Events));
        Assert.Single(game.State.Players[1].Equipped);
    }

    [Fact]
    public void Equip_FromBackpack_Works()
    {
        var game = Turn().Backpack(1, "equip-head").Build();

        var result = game.Execute(new EquipCard("p1", IdOf(game.State.Players[0].Backpack, "equip-head")));

        Assert.True(result.Accepted);
        Assert.Empty(game.State.Players[0].Backpack);
    }

    [Fact]
    public void Equip_OneShot_IsRejected()
    {
        var game = Turn("oneshot-plus2").Build();

        var result = game.Execute(new EquipCard("p1", InHand(game, "oneshot-plus2")));

        Assert.False(result.Accepted);
        Assert.Equal("Diese Karte kann man nicht anlegen.", result.Rejection);
    }

    [Fact]
    public void Equip_CardNotOwned_IsRejected()
    {
        var game = Turn().Build();

        var result = game.Execute(new EquipCard("p1", 99999));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Unequip_MovesToBackpack_AndBackpackDoesNotCount()
    {
        var game = Turn().Equip(1, "special-full-stack").Build();
        var p = game.State.Players[0];
        Assert.Equal(2, PassiveEffects.MaxDepartments(game.State, game.Catalog, "p1"));

        var result = game.Execute(new UnequipCard("p1", p.Equipped[0].InstanceId));

        Assert.True(result.Accepted);
        Assert.IsType<CardUnequipped>(Assert.Single(result.Events));
        Assert.Empty(p.Equipped);
        Assert.Single(p.Backpack);
        Assert.Empty(PassiveEffects.Of(game.State, game.Catalog, "p1"));
        Assert.Equal(1, PassiveEffects.MaxDepartments(game.State, game.Catalog, "p1"));
    }

    [Fact]
    public void Unequip_FullStack_WithTwoDepartments_IsRejected()
    {
        var game = Turn().Equip(1, "special-full-stack").Department(1, "dept-qa").Department(1, "dept-cloud").Build();
        var p = game.State.Players[0];

        var result = game.Execute(new UnequipCard("p1", p.Equipped[0].InstanceId));

        Assert.False(result.Accepted);
        Assert.Single(p.Equipped);
    }

    [Fact]
    public void DiscardOwnCard_PutsCardOnMatchingDiscardPile()
    {
        var game = Turn("equip-head", "dept-qa").Build();
        var loot = InHand(game, "equip-head");
        var ticket = InHand(game, "dept-qa");

        var r1 = game.Execute(new DiscardOwnCard("p1", loot));
        var r2 = game.Execute(new DiscardOwnCard("p1", ticket));

        Assert.True(r1.Accepted && r2.Accepted);
        Assert.IsType<CardDiscarded>(r1.Events[0]);
        Assert.Contains(game.State.LootDiscard, c => c.InstanceId == loot);
        Assert.Contains(game.State.TicketDiscard, c => c.InstanceId == ticket);
        Assert.DoesNotContain(game.State.Players[0].Hand, c => c.InstanceId == loot || c.InstanceId == ticket);
    }

    [Fact]
    public void DiscardOwnCard_FullStackWithTwoDepartments_IsRejected()
    {
        var game = Turn().Equip(1, "special-full-stack").Department(1, "dept-qa").Department(1, "dept-cloud").Build();

        var result = game.Execute(new DiscardOwnCard("p1", game.State.Players[0].Equipped[0].InstanceId));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void DiscardOwnCard_DuringCombat_IsRejected()
    {
        var game = Turn("equip-head").Phase(GamePhase.Combat).Build();

        Assert.False(game.Execute(new DiscardOwnCard("p1", InHand(game, "equip-head"))).Accepted);
    }

    [Fact]
    public void RejectedEquip_DoesNotChangeStateOrRng()
    {
        var game = Turn("oneshot-plus2").Build();
        var before = game.Save();

        game.Execute(new EquipCard("p1", InHand(game, "oneshot-plus2")));

        Assert.Equal(before, game.Save());
    }
}
