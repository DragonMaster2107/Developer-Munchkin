using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Views;

namespace DeveloperDungeon.Web.Logic;

public enum CardLocation { Hand, Equipped, Backpack, Department, Language }

public enum TargetKind { None, Player, Side, Monster, MonsterFromHand }

public sealed record ActionTarget(string? PlayerId = null, Side? Side = null, int? MonsterInstanceId = null);

public sealed record CardAction(string Label, TargetKind Target, Func<ActionTarget, GameCommand> Build);

/// <summary>Bestimmt, welche Aktionen eine Karte je nach Ablageort anbietet.</summary>
public static class CardActionResolver
{
    public static IReadOnlyList<CardAction> For(CardDefinition def, CardInstance card, CardLocation location, GameView view, string actorId)
    {
        var id = card.InstanceId;
        var actions = new List<CardAction>();

        switch (location)
        {
            case CardLocation.Hand:
                AddHandActions(actions, def, id, view, actorId);
                break;
            case CardLocation.Equipped:
                actions.Add(new("In den Rucksack", TargetKind.None, _ => new UnequipCard(actorId, id)));
                break;
            case CardLocation.Backpack:
                actions.Add(Equip(id, actorId));
                if (def.Kind == CardKind.OneShot)
                {
                    actions.Add(OneShotPlay(def, id, actorId));
                }
                break;
        }

        actions.Add(new("Abwerfen", TargetKind.None, _ => new DiscardOwnCard(actorId, id)));
        return actions;
    }

    private static void AddHandActions(List<CardAction> actions, CardDefinition def, int id, GameView view, string actorId)
    {
        switch (def.Kind)
        {
            case CardKind.Equipment or CardKind.Department or CardKind.Language:
                actions.Add(Equip(id, actorId));
                break;
            case CardKind.Special:
                AddSpecialActions(actions, def, id, actorId);
                break;
            case CardKind.Monster:
                if (view.Phase == GamePhase.AfterTicket && actorId == view.ActivePlayerId)
                {
                    actions.Add(new("Ärger suchen", TargetKind.None, _ => new LookForTrouble(actorId, id)));
                }
                break;
            case CardKind.Curse:
                actions.Add(new("Verfluchen …", TargetKind.Player, t => new PlayCard(actorId, id, TargetPlayerId: t.PlayerId)));
                break;
            case CardKind.OneShot:
                actions.Add(OneShotPlay(def, id, actorId));
                break;
            case CardKind.MonsterEnhancer:
                actions.Add(new("Auf Monster spielen …", TargetKind.Monster,
                    t => new PlayCard(actorId, id, TargetMonsterInstanceId: t.MonsterInstanceId)));
                break;
            case CardKind.LevelUp:
                actions.Add(new("Ausspielen", TargetKind.None, _ => new PlayCard(actorId, id)));
                break;
        }
    }

    private static void AddSpecialActions(List<CardAction> actions, CardDefinition def, int id, string actorId)
    {
        if (def.Effects.Any(e => e is MaxDepartments or MaxLanguages))
        {
            actions.Add(Equip(id, actorId));
        }
        else if (def.Effects.Any(e => e is AddMonsterToCombat))
        {
            actions.Add(new("Monster dazuholen …", TargetKind.MonsterFromHand,
                t => new PlayCard(actorId, id, SecondaryInstanceId: t.MonsterInstanceId)));
        }
        else if (def.Effects.Any(e => e is Manual))
        {
            actions.Add(new("Ausspielen", TargetKind.None, _ => new PlayCard(actorId, id)));
        }
    }

    /// <summary>Ein stellvertretendes Ziel, mit dem eine Aktion beim Öffnen des Menüs vorab geprüft wird.
    /// Fehlt ein echtes Ziel, steht die Monster-ID 0; die Engine lehnt dann mit dem passenden Grund ab.</summary>
    public static ActionTarget RepresentativeTarget(CardAction action, GameView view, CardCatalog catalog, string actorId, int playedInstanceId) =>
        action.Target switch
        {
            TargetKind.Player => new ActionTarget(PlayerId: view.Players.FirstOrDefault(p => p.Id != actorId)?.Id),
            TargetKind.Side => new ActionTarget(Side: Side.Player),
            TargetKind.Monster => new ActionTarget(MonsterInstanceId: view.Combat?.State.Monsters.FirstOrDefault()?.InstanceId ?? 0),
            TargetKind.MonsterFromHand => new ActionTarget(MonsterInstanceId: (view.MyHand ?? [])
                .FirstOrDefault(c => c.InstanceId != playedInstanceId && catalog.Get(c.CardId).Kind == CardKind.Monster)?.InstanceId ?? 0),
            _ => new ActionTarget(),
        };

    private static CardAction Equip(int id, string actorId) =>
        new("Anlegen", TargetKind.None, _ => new EquipCard(actorId, id));

    private static CardAction OneShotPlay(CardDefinition def, int id, string actorId) =>
        def.Effects.Any(e => e is CombatBonus)
            ? new("Ausspielen …", TargetKind.Side, t => new PlayCard(actorId, id, Side: t.Side))
            : new("Ausspielen", TargetKind.None, _ => new PlayCard(actorId, id));
}
