using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class EffectApplier
{
    private const int MaxLevelByEffect = 9;

    /// <summary>Wendet die Sofort-Wirkungen auf einen Spieler an; passive und kampfbezogene Wirkungen werden ignoriert.</summary>
    public static void ApplyToPlayer(GameState s, CardCatalog c, GameRandom rng, string targetPlayerId, IEnumerable<Effect> effects, string sourceCardId, List<GameEvent> events)
    {
        var player = s.Players.First(p => p.Id == targetPlayerId);
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case LoseLevels e:
                    SetLevel(s, player, Math.Max(1, player.Level - e.Amount), events);
                    break;
                case GainLevels e:
                    SetLevel(s, player, Math.Max(player.Level, Math.Min(MaxLevelByEffect, player.Level + e.Amount)), events);
                    break;
                case LoseSlot e:
                    var worn = player.Equipped.Where(x => c.Get(x.CardId) is { Kind: CardKind.Equipment } d && d.Slot == e.Slot).ToList();
                    DiscardAll(s, c, player, worn, player.Equipped, events);
                    break;
                case LoseSmallItem:
                    if (HasValidOption(c, player, ChoiceKind.DiscardSmallItem))
                    {
                        Request(s, player.Id, ChoiceKind.DiscardSmallItem, null, sourceCardId, events);
                    }
                    break;
                case DiscardHand e:
                    DiscardFromHand(s, c, rng, player, e.Count, events);
                    break;
                case GiveEquippedItemToNeighbor e:
                    if (HasValidOption(c, player, ChoiceKind.GiveEquippedItem))
                    {
                        Request(s, player.Id, ChoiceKind.GiveEquippedItem, Neighbor(s, player, e.Direction).Id, sourceCardId, events);
                    }
                    break;
                case DrawCards e:
                    for (var i = 0; i < e.Count; i++)
                    {
                        var card = Decks.Draw(s, rng, e.Deck);
                        if (card is not null)
                        {
                            player.Hand.Add(card);
                        }
                    }
                    break;
                case NextTurnPenalty e:
                    s.TemporaryEffects.Add(new TemporaryEffect
                    {
                        PlayerId = player.Id,
                        CombatModifier = -e.Amount,
                        StartsAfterTurnNumber = s.TurnNumber,
                        SourceCardId = sourceCardId,
                    });
                    break;
                case Manual:
                    events.Add(new ManualEffectShown(sourceCardId));
                    break;
            }
        }
    }

    /// <summary>Gibt es für die Auswahl noch mindestens eine wählbare Karte? Gemeinsame Prüfung für Anlegen und Verwerfen.</summary>
    public static bool HasValidOption(CardCatalog c, PlayerState player, ChoiceKind kind) => kind switch
    {
        ChoiceKind.DiscardSmallItem => player.Equipped.Any(x => IsSmallItem(c, x)),
        ChoiceKind.GiveEquippedItem => player.Equipped.Count > 0,
        _ => false,
    };

    public static bool IsSmallItem(CardCatalog c, CardInstance card) =>
        c.Get(card.CardId) is { Kind: CardKind.Equipment, Slot: Slot.None };

    /// <summary>Linker Nachbar = nächster Index, rechter = vorheriger.</summary>
    private static PlayerState Neighbor(GameState s, PlayerState player, Direction direction)
    {
        var index = s.Players.IndexOf(player);
        var step = direction == Direction.Left ? 1 : -1;
        return s.Players[(index + step + s.Players.Count) % s.Players.Count];
    }

    private static void SetLevel(GameState s, PlayerState player, int to, List<GameEvent> events)
    {
        if (to == player.Level)
        {
            return;
        }
        events.Add(new LevelChanged(player.Id, player.Level, to));
        GameLog.Add(s, $"{player.Name}: Stufe {player.Level} → {to}.");
        player.Level = to;
    }

    private static void DiscardFromHand(GameState s, CardCatalog c, GameRandom rng, PlayerState player, int? count, List<GameEvent> events)
    {
        var pool = player.Hand.ToList();
        var lost = new List<CardInstance>();
        var n = count is null ? pool.Count : Math.Min(count.Value, pool.Count);
        for (var i = 0; i < n; i++)
        {
            var pick = pool[rng.Next(pool.Count)];
            pool.Remove(pick);
            lost.Add(pick);
        }
        DiscardAll(s, c, player, lost, player.Hand, events);
    }

    private static void DiscardAll(GameState s, CardCatalog c, PlayerState player, List<CardInstance> cards, List<CardInstance> zone, List<GameEvent> events)
    {
        if (cards.Count == 0)
        {
            return;
        }
        foreach (var card in cards)
        {
            zone.Remove(card);
            Decks.Discard(s, c, card);
        }
        events.Add(new CardsLost(player.Id, cards.Select(x => x.InstanceId).ToList()));
        GameLog.Add(s, $"{player.Name} verliert {string.Join(", ", cards.Select(x => c.Get(x.CardId).Name))}.");
    }

    private static void Request(GameState s, string playerId, ChoiceKind kind, string? recipientId, string sourceCardId, List<GameEvent> events)
    {
        s.PendingChoices.Add(new PendingChoice { PlayerId = playerId, Kind = kind, RecipientPlayerId = recipientId, SourceCardId = sourceCardId });
        events.Add(new ChoiceRequested(playerId, kind));
    }
}
