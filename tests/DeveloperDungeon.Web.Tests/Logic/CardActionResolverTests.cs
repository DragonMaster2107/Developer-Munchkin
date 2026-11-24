using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Views;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Tests.Support;

namespace DeveloperDungeon.Web.Tests.Logic;

public class CardActionResolverTests
{
    private static readonly GameView BaseView = TestGames.NewController().View;
    private static string Actor => BaseView.ActivePlayerId;

    private static IReadOnlyList<CardAction> Resolve(
        CardDefinition def, CardLocation location, GamePhase phase = GamePhase.OpenTicket, string? actor = null)
    {
        var card = new CardInstance { InstanceId = 7, CardId = def.Id };
        return CardActionResolver.For(def, card, location, BaseView with { Phase = phase }, actor ?? Actor);
    }

    private static CardDefinition Def(string id) => TestGames.Catalog.Get(id);

    private static string[] Labels(IReadOnlyList<CardAction> actions) => [.. actions.Select(a => a.Label)];

    [Fact]
    public void HandEquipment_OffersEquipAndDiscard()
    {
        var actions = Resolve(Def("equip-rider"), CardLocation.Hand);

        Assert.Equal(["Anlegen", "Abwerfen"], Labels(actions));
        Assert.Equal(new EquipCard(Actor, 7), actions[0].Build(new ActionTarget()));
        Assert.Equal(new DiscardOwnCard(Actor, 7), actions[1].Build(new ActionTarget()));
    }

    [Fact]
    public void HandDepartmentAndLanguage_OfferEquip()
    {
        Assert.Equal(["Anlegen", "Abwerfen"], Labels(Resolve(Def("dept-qa"), CardLocation.Hand)));
        Assert.Equal(["Anlegen", "Abwerfen"], Labels(Resolve(Def("lang-csharp"), CardLocation.Hand)));
    }

    [Fact]
    public void HandSpecialWithMaxEffects_OfferEquip()
    {
        Assert.Equal(["Anlegen", "Abwerfen"], Labels(Resolve(Def("special-full-stack"), CardLocation.Hand)));
        Assert.Equal(["Anlegen", "Abwerfen"], Labels(Resolve(Def("special-polyglot"), CardLocation.Hand)));
    }

    [Fact]
    public void HandSpecialManualOnly_OffersPlayWithoutTarget()
    {
        var def = new CardDefinition { Id = "x", Kind = CardKind.Special, Effects = [new Manual()] };

        var actions = Resolve(def, CardLocation.Hand);

        Assert.Equal(["Ausspielen", "Abwerfen"], Labels(actions));
        Assert.Equal(TargetKind.None, actions[0].Target);
        Assert.Equal(new PlayCard(Actor, 7), actions[0].Build(new ActionTarget()));
    }

    [Fact]
    public void HandMonster_InAfterTicket_OffersLookForTrouble()
    {
        var actions = Resolve(Def("monster-impatient-po"), CardLocation.Hand, GamePhase.AfterTicket);

        Assert.Equal(["Ärger suchen", "Abwerfen"], Labels(actions));
        Assert.Equal(new LookForTrouble(Actor, 7), actions[0].Build(new ActionTarget()));
    }

    [Fact]
    public void HandMonster_InOpenTicket_DoesNotOfferLookForTrouble()
    {
        Assert.Equal(["Abwerfen"], Labels(Resolve(Def("monster-impatient-po"), CardLocation.Hand, GamePhase.OpenTicket)));
    }

    [Fact]
    public void HandMonster_NotActivePlayer_DoesNotOfferLookForTrouble()
    {
        var other = BaseView.Players.First(p => p.Id != Actor).Id;

        Assert.Equal(["Abwerfen"], Labels(Resolve(Def("monster-impatient-po"), CardLocation.Hand, GamePhase.AfterTicket, other)));
    }

    [Fact]
    public void HandCurse_NeedsPlayerTarget_BuildsPlayCardWithTarget()
    {
        var actions = Resolve(Def("curse-pipeline-red"), CardLocation.Hand);

        Assert.Equal(["Verfluchen …", "Abwerfen"], Labels(actions));
        Assert.Equal(TargetKind.Player, actions[0].Target);
        var command = Assert.IsType<PlayCard>(actions[0].Build(new ActionTarget(PlayerId: "p2")));
        Assert.Equal("p2", command.TargetPlayerId);
        Assert.Equal(7, command.InstanceId);
    }

    [Fact]
    public void ColdCoffee_NeedsSide()
    {
        var actions = Resolve(Def("oneshot-cold-coffee"), CardLocation.Hand);

        Assert.Equal(["Ausspielen …", "Abwerfen"], Labels(actions));
        Assert.Equal(TargetKind.Side, actions[0].Target);
        var command = Assert.IsType<PlayCard>(actions[0].Build(new ActionTarget(Side: Side.Monster)));
        Assert.Equal(Side.Monster, command.Side);
    }

    [Fact]
    public void RemoteDay_NeedsNoTarget()
    {
        var actions = Resolve(Def("oneshot-remote-day"), CardLocation.Hand);

        Assert.Equal(["Ausspielen", "Abwerfen"], Labels(actions));
        Assert.Equal(TargetKind.None, actions[0].Target);
    }

    [Fact]
    public void LevelUp_OffersPlayWithoutTarget()
    {
        var actions = Resolve(Def("levelup-raise"), CardLocation.Hand);

        Assert.Equal(["Ausspielen", "Abwerfen"], Labels(actions));
        Assert.Equal(TargetKind.None, actions[0].Target);
    }

    [Fact]
    public void Enhancer_NeedsMonster()
    {
        var def = new CardDefinition { Id = "enh", Kind = CardKind.MonsterEnhancer, Effects = [new MonsterModifier(2, 0)] };

        var actions = Resolve(def, CardLocation.Hand);

        Assert.Equal(["Auf Monster spielen …", "Abwerfen"], Labels(actions));
        Assert.Equal(TargetKind.Monster, actions[0].Target);
        var command = Assert.IsType<PlayCard>(actions[0].Build(new ActionTarget(MonsterInstanceId: 42)));
        Assert.Equal(42, command.TargetMonsterInstanceId);
    }

    [Fact]
    public void WanderingBug_NeedsMonsterFromHand()
    {
        var actions = Resolve(Def("special-wandering-bug"), CardLocation.Hand);

        Assert.Equal(["Monster dazuholen …", "Abwerfen"], Labels(actions));
        Assert.Equal(TargetKind.MonsterFromHand, actions[0].Target);
        var command = Assert.IsType<PlayCard>(actions[0].Build(new ActionTarget(MonsterInstanceId: 9)));
        Assert.Equal(9, command.SecondaryInstanceId);
    }

    [Fact]
    public void Equipped_OffersBackpackAndDiscard()
    {
        var actions = Resolve(Def("equip-rider"), CardLocation.Equipped);

        Assert.Equal(["In den Rucksack", "Abwerfen"], Labels(actions));
        Assert.Equal(new UnequipCard(Actor, 7), actions[0].Build(new ActionTarget()));
    }

    [Fact]
    public void Backpack_OneShot_OffersPlay()
    {
        Assert.Equal(["Anlegen", "Ausspielen …", "Abwerfen"], Labels(Resolve(Def("oneshot-cold-coffee"), CardLocation.Backpack)));
        Assert.Equal(["Anlegen", "Abwerfen"], Labels(Resolve(Def("equip-rider"), CardLocation.Backpack)));
    }

    [Theory]
    [InlineData(CardLocation.Department)]
    [InlineData(CardLocation.Language)]
    public void DepartmentAndLanguageZones_OfferOnlyDiscard(CardLocation location)
    {
        Assert.Equal(["Abwerfen"], Labels(Resolve(Def("dept-qa"), location)));
    }

    private static CardAction Action(TargetKind kind) => new("x", kind, _ => new EndTurn(Actor, []));

    private static CardInstance Inst(int id, string cardId) => new() { InstanceId = id, CardId = cardId };

    [Fact]
    public void RepresentativeTarget_None_IsEmpty()
    {
        Assert.Equal(new ActionTarget(), CardActionResolver.RepresentativeTarget(Action(TargetKind.None), BaseView, TestGames.Catalog, Actor, 7));
    }

    [Fact]
    public void RepresentativeTarget_Player_IsFirstOtherPlayer()
    {
        var other = BaseView.Players.First(p => p.Id != Actor).Id;

        var target = CardActionResolver.RepresentativeTarget(Action(TargetKind.Player), BaseView, TestGames.Catalog, Actor, 7);

        Assert.Equal(new ActionTarget(PlayerId: other), target);
    }

    [Fact]
    public void RepresentativeTarget_Side_IsPlayerSide()
    {
        Assert.Equal(new ActionTarget(Side: Side.Player),
            CardActionResolver.RepresentativeTarget(Action(TargetKind.Side), BaseView, TestGames.Catalog, Actor, 7));
    }

    [Fact]
    public void RepresentativeTarget_Monster_IsFirstMonsterInCombat()
    {
        var combat = new CombatState { Monsters = [Inst(31, "monster-impatient-po"), Inst(32, "monster-legacy-2012")] };
        var view = BaseView with { Combat = new CombatView(combat, new CombatSummary(1, 2, false, false, 1, 1)) };

        var target = CardActionResolver.RepresentativeTarget(Action(TargetKind.Monster), view, TestGames.Catalog, Actor, 7);

        Assert.Equal(new ActionTarget(MonsterInstanceId: 31), target);
    }

    [Fact]
    public void RepresentativeTarget_Monster_WithoutCombat_UsesDummyId()
    {
        Assert.Equal(new ActionTarget(MonsterInstanceId: 0),
            CardActionResolver.RepresentativeTarget(Action(TargetKind.Monster), BaseView with { Combat = null }, TestGames.Catalog, Actor, 7));
    }

    [Fact]
    public void RepresentativeTarget_MonsterFromHand_IsFirstOtherMonsterInHand()
    {
        var view = BaseView with { MyHand = [Inst(7, "monster-impatient-po"), Inst(8, "equip-rider"), Inst(9, "monster-legacy-2012")] };

        var target = CardActionResolver.RepresentativeTarget(Action(TargetKind.MonsterFromHand), view, TestGames.Catalog, Actor, 7);

        Assert.Equal(new ActionTarget(MonsterInstanceId: 9), target);
    }

    [Fact]
    public void RepresentativeTarget_MonsterFromHand_WithoutMonster_UsesDummyId()
    {
        var view = BaseView with { MyHand = [Inst(7, "special-wandering-bug"), Inst(8, "equip-rider")] };

        Assert.Equal(new ActionTarget(MonsterInstanceId: 0),
            CardActionResolver.RepresentativeTarget(Action(TargetKind.MonsterFromHand), view, TestGames.Catalog, Actor, 7));
    }
}
