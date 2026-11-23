using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class ChoiceRules
{
    public static CommandResult Handle(GameState s, CardCatalog c, ResolveChoice cmd)
    {
        if (s.PendingChoices.Count == 0)
        {
            return CommandResult.Rejected("Es ist keine Auswahl offen.");
        }
        var choice = s.PendingChoices[0];
        if (choice.PlayerId != cmd.ActorId)
        {
            return CommandResult.Rejected("Diese Auswahl trifft ein anderer Spieler.");
        }
        var player = s.Players.First(p => p.Id == cmd.ActorId);
        var card = player.Equipped.FirstOrDefault(x => x.InstanceId == cmd.InstanceId);
        if (card is null)
        {
            return CommandResult.Rejected("Wähle eine deiner angelegten Karten.");
        }

        switch (choice.Kind)
        {
            case ChoiceKind.DiscardSmallItem:
                if (!EffectApplier.IsSmallItem(c, card))
                {
                    return CommandResult.Rejected("Wähle ein kleines Item.");
                }
                player.Equipped.Remove(card);
                Decks.Discard(s, c, card);
                GameLog.Add(s, $"{player.Name} wirft {c.Get(card.CardId).Name} ab.");
                break;
            case ChoiceKind.GiveEquippedItem:
                var recipient = s.Players.First(p => p.Id == choice.RecipientPlayerId);
                player.Equipped.Remove(card);
                recipient.Backpack.Add(card);
                GameLog.Add(s, $"{player.Name} gibt {c.Get(card.CardId).Name} an {recipient.Name}.");
                break;
        }

        s.PendingChoices.RemoveAt(0);
        return CommandResult.Ok([new ChoiceResolved(player.Id, card.InstanceId)]);
    }

    /// <summary>Verwirft führende Auswahlen ohne gültige Option, damit das Spiel nicht festhängt. <c>Game.Execute</c> ruft das nach jedem angenommenen Befehl auf.</summary>
    internal static void DropUnresolvable(GameState s, CardCatalog c)
    {
        while (s.PendingChoices.Count > 0)
        {
            var next = s.PendingChoices[0];
            var owner = s.Players.First(p => p.Id == next.PlayerId);
            if (EffectApplier.HasValidOption(c, owner, next.Kind))
            {
                return;
            }
            s.PendingChoices.RemoveAt(0);
            GameLog.Add(s, $"{owner.Name} hat nichts mehr abzugeben.");
        }
    }
}
