using System.Text.Json;
using DeveloperDungeon.Engine.Serialization;

namespace DeveloperDungeon.Engine.Cards;

public sealed class CardCatalog
{
    private readonly List<CardDefinition> _cards;
    private readonly Dictionary<string, CardDefinition> _byId = new();

    private CardCatalog(List<CardDefinition> cards)
    {
        _cards = cards;
        foreach (var card in cards)
        {
            _byId.TryAdd(card.Id, card);
        }
    }

    public IReadOnlyList<CardDefinition> All => _cards;

    public static CardCatalog FromJson(params string[] jsonArrays)
    {
        var cards = new List<CardDefinition>();
        foreach (var json in jsonArrays)
        {
            var parsed = JsonSerializer.Deserialize(json, EngineJsonContext.Default.ListCardDefinition);
            if (parsed is not null)
            {
                cards.AddRange(parsed);
            }
        }
        return new CardCatalog(cards);
    }

    public static CardCatalog LoadEmbedded()
    {
        var assembly = typeof(CardCatalog).Assembly;
        var jsons = new List<string>();
        foreach (var name in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || !name.Contains(".Cards.Data.", StringComparison.Ordinal))
            {
                continue;
            }
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            jsons.Add(reader.ReadToEnd());
        }
        return FromJson(jsons.ToArray());
    }

    public CardDefinition Get(string id) =>
        _byId.TryGetValue(id, out var card) ? card : throw new KeyNotFoundException($"Unbekannte Karte: {id}");

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        foreach (var group in _cards.GroupBy(c => c.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"Karte '{group.Key}': Id kommt {group.Count()}-mal vor.");
        }

        foreach (var card in _cards)
        {
            var id = card.Id;

            if (card.Count < 1)
            {
                errors.Add($"Karte '{id}': Anzahl muss mindestens 1 sein.");
            }

            switch (card.Kind)
            {
                case CardKind.Monster:
                    if (card.Level is not >= 1)
                    {
                        errors.Add($"Karte '{id}': Monster braucht Stufe >= 1.");
                    }
                    if (card.RewardLoot < 0)
                    {
                        errors.Add($"Karte '{id}': Belohnung (Loot) darf nicht negativ sein.");
                    }
                    break;
                case CardKind.Department or CardKind.Language:
                    if (string.IsNullOrWhiteSpace(card.Key))
                    {
                        errors.Add($"Karte '{id}': Abteilung/Sprache braucht einen Key.");
                    }
                    break;
                case CardKind.Equipment:
                    if (card.Slot == Slot.Hands ? card.Hands is not (1 or 2) : card.Hands != 0)
                    {
                        errors.Add($"Karte '{id}': Hände passen nicht zum Slot (Slot Hands braucht 1 oder 2 Hände, alle anderen 0).");
                    }
                    break;
            }

            var deckOk = card.Kind switch
            {
                CardKind.Monster or CardKind.MonsterEnhancer or CardKind.Curse
                    or CardKind.Department or CardKind.Language => card.Deck == Deck.Ticket,
                CardKind.Equipment or CardKind.OneShot or CardKind.LevelUp => card.Deck == Deck.Loot,
                _ => true,
            };
            if (!deckOk)
            {
                errors.Add($"Karte '{id}': Stapel {card.Deck} passt nicht zur Art {card.Kind}.");
            }
        }

        return errors;
    }
}
