using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class EquipRules
{
    private const string TimeRejection = "Das geht gerade nicht – nur im Aufbau oder in der eigenen Runde außerhalb des Kampfes.";

    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, EquipCard cmd)
    {
        if (!TimeAllowed(s, cmd.ActorId))
        {
            return CommandResult.Rejected(TimeRejection);
        }
        var player = s.Players.First(p => p.Id == cmd.ActorId);
        var source = player.Hand.Any(x => x.InstanceId == cmd.InstanceId) ? player.Hand : player.Backpack;
        var card = source.FirstOrDefault(x => x.InstanceId == cmd.InstanceId);
        if (card is null)
        {
            return CommandResult.Rejected("Diese Karte hast du nicht auf der Hand oder im Rucksack.");
        }

        var def = c.Get(card.CardId);
        List<CardInstance> target;
        switch (def.Kind)
        {
            case CardKind.Equipment:
                var problem = CheckEquipment(player, c, def);
                if (problem is not null)
                {
                    return CommandResult.Rejected(problem);
                }
                target = player.Equipped;
                break;
            case CardKind.Department:
                if (player.Departments.Count >= PassiveEffects.MaxDepartments(s, c, player.Id))
                {
                    return CommandResult.Rejected("Du hast bereits die maximale Anzahl Abteilungen.");
                }
                target = player.Departments;
                break;
            case CardKind.Language:
                if (player.Languages.Count >= PassiveEffects.MaxLanguages(s, c, player.Id))
                {
                    return CommandResult.Rejected("Du kennst bereits die maximale Anzahl Sprachen.");
                }
                target = player.Languages;
                break;
            case CardKind.Special when def.Effects.Any(e => e is MaxDepartments or MaxLanguages):
                target = player.Equipped;
                break;
            default:
                return CommandResult.Rejected("Diese Karte kann man nicht anlegen.");
        }

        source.Remove(card);
        target.Add(card);
        GameLog.Add(s, $"{player.Name} legt {def.Name} an.");
        return CommandResult.Ok([new CardEquipped(player.Id, card.InstanceId)]);
    }

    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, UnequipCard cmd)
    {
        if (!TimeAllowed(s, cmd.ActorId))
        {
            return CommandResult.Rejected(TimeRejection);
        }
        var player = s.Players.First(p => p.Id == cmd.ActorId);
        var card = player.Equipped.FirstOrDefault(x => x.InstanceId == cmd.InstanceId);
        if (card is null)
        {
            return CommandResult.Rejected("Diese Karte ist nicht angelegt.");
        }
        var limit = LimitProblem(s, c, player, card);
        if (limit is not null)
        {
            return CommandResult.Rejected(limit);
        }

        player.Equipped.Remove(card);
        player.Backpack.Add(card);
        GameLog.Add(s, $"{player.Name} legt {c.Get(card.CardId).Name} in den Rucksack.");
        return CommandResult.Ok([new CardUnequipped(player.Id, card.InstanceId)]);
    }

    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, DiscardOwnCard cmd)
    {
        if (!TimeAllowed(s, cmd.ActorId))
        {
            return CommandResult.Rejected(TimeRejection);
        }
        var player = s.Players.First(p => p.Id == cmd.ActorId);
        var zone = new[] { player.Hand, player.Equipped, player.Backpack, player.Departments, player.Languages }
            .FirstOrDefault(z => z.Any(x => x.InstanceId == cmd.InstanceId));
        if (zone is null)
        {
            return CommandResult.Rejected("Diese Karte gehört dir nicht.");
        }
        var card = zone.First(x => x.InstanceId == cmd.InstanceId);
        var limit = LimitProblem(s, c, player, card);
        if (limit is not null)
        {
            return CommandResult.Rejected(limit);
        }

        zone.Remove(card);
        Decks.Discard(s, c, card);
        GameLog.Add(s, $"{player.Name} wirft {c.Get(card.CardId).Name} ab.");
        return CommandResult.Ok([new CardDiscarded(player.Id, card.InstanceId)]);
    }

    private static bool TimeAllowed(GameState s, string actorId) =>
        s.Phase == GamePhase.Setup || CommandGuard.IsOwnTurnOutsideCombat(s, actorId);

    /// <summary>Ablehnungsgrund, wenn nach dem Entfernen der Karte mehr Abteilungen/Sprachen liegen als erlaubt.</summary>
    private static string? LimitProblem(GameState s, CardCatalog c, PlayerState player, CardInstance removed)
    {
        if (player.Departments.Count(x => x.InstanceId != removed.InstanceId) > PassiveEffects.MaxDepartments(s, c, player.Id, removed.InstanceId))
        {
            return "Dann hättest du zu viele Abteilungen. Wirf erst eine Abteilung ab.";
        }
        if (player.Languages.Count(x => x.InstanceId != removed.InstanceId) > PassiveEffects.MaxLanguages(s, c, player.Id, removed.InstanceId))
        {
            return "Dann hättest du zu viele Sprachen. Wirf erst eine Sprache ab.";
        }
        return null;
    }

    private static string? CheckEquipment(PlayerState player, CardCatalog c, CardDefinition def)
    {
        var worn = player.Equipped.Select(x => c.Get(x.CardId)).Where(d => d.Kind == CardKind.Equipment).ToList();

        if (def.Slot == Slot.Hands)
        {
            if (worn.Where(d => d.Slot == Slot.Hands).Sum(d => d.Hands) + def.Hands > 2)
            {
                return "Du hast nur zwei Hände frei zu vergeben.";
            }
        }
        else if (def.Slot != Slot.None && worn.Any(d => d.Slot == def.Slot))
        {
            return $"Im Slot {SlotName(def.Slot)} trägst du bereits etwas.";
        }

        if (def.RequiresDepartment is { } dept && player.Departments.All(x => c.Get(x.CardId).Key != dept))
        {
            return $"Nur für {DisplayName(c, CardKind.Department, dept)}. Du hast diese Abteilung nicht.";
        }
        if (def.RequiresLanguage is { } lang && player.Languages.All(x => c.Get(x.CardId).Key != lang))
        {
            return $"Nur für {DisplayName(c, CardKind.Language, lang)}. Du kennst diese Sprache nicht.";
        }
        return null;
    }

    private static string DisplayName(CardCatalog c, CardKind kind, string key) =>
        c.All.FirstOrDefault(d => d.Kind == kind && d.Key == key)?.Name ?? key;

    private static string SlotName(Slot slot) => slot switch
    {
        Slot.Head => "Kopf",
        Slot.Body => "Körper",
        Slot.Feet => "Füße",
        Slot.Big => "Groß",
        _ => slot.ToString(),
    };
}
