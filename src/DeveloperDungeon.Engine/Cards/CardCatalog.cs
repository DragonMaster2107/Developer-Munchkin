using System.Text.Json;
using DeveloperDungeon.Engine.Serialization;

namespace DeveloperDungeon.Engine.Cards;

public sealed class CardCatalog
{
    private readonly List<CardDefinition> _cards;
    private readonly Dictionary<string, CardDefinition> _byId = new();
    private readonly int _nullEntries;

    private CardCatalog(List<CardDefinition?> cards)
    {
        _cards = cards.OfType<CardDefinition>().ToList();
        _nullEntries = cards.Count - _cards.Count;
        foreach (var card in _cards)
        {
            if (card.Id is not null)
            {
                _byId.TryAdd(card.Id, card);
            }
        }
    }

    public IReadOnlyList<CardDefinition> All => _cards;

    public static CardCatalog FromJson(params string[] jsonArrays)
    {
        var cards = new List<CardDefinition?>();
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

        if (_nullEntries > 0)
        {
            errors.Add($"Der Katalog enthält {_nullEntries} leere Einträge (null).");
        }

        foreach (var group in _cards.Where(c => c.Id is not null).GroupBy(c => c.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"Karte '{group.Key}': Id kommt {group.Count()}-mal vor.");
        }

        var departmentKeys = KeysOf(CardKind.Department);
        var languageKeys = KeysOf(CardKind.Language);

        foreach (var card in _cards)
        {
            var id = card.Id;

            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"Karte '{card.Name}': Id fehlt.");
            }
            if (string.IsNullOrWhiteSpace(card.Name))
            {
                errors.Add($"Karte '{id}': Name fehlt.");
            }
            if (string.IsNullOrWhiteSpace(card.RulesText))
            {
                errors.Add($"Karte '{id}': Regeltext fehlt.");
            }
            if (card.Effects is null || card.BadStuff is null || card.Tags is null)
            {
                errors.Add($"Karte '{id}': Wirkungen, Schlimme Dinge und Tags dürfen nicht null sein.");
            }
            var effects = card.Effects ?? [];
            var allEffects = effects.Concat(card.BadStuff ?? []).ToList();
            if (allEffects.Any(e => e is null))
            {
                errors.Add($"Karte '{id}': enthält eine leere Wirkung (null).");
            }

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

            var usable = card.Kind switch
            {
                CardKind.Special => effects.Any(e => e is MaxDepartments or MaxLanguages or AddMonsterToCombat or Manual),
                CardKind.OneShot => effects.Any(e => e is CombatBonus or AutoEscape or Manual),
                CardKind.MonsterEnhancer => effects.Any(e => e is MonsterModifier or Manual),
                CardKind.LevelUp => effects.Any(e => e is GainLevels or Manual),
                _ => true,
            };
            if (!usable)
            {
                errors.Add($"Karte '{id}': {card.Kind} hat keine Wirkung, mit der man sie benutzen kann.");
            }

            var conditions = allEffects.OfType<ConditionalCombatBonus>().ToList();
            foreach (var key in conditions.Select(e => e.PlayerDepartment).Append(card.RequiresDepartment).OfType<string>())
            {
                if (!departmentKeys.Contains(key))
                {
                    errors.Add($"Karte '{id}': Abteilung '{key}' gibt es im Katalog nicht.");
                }
            }
            foreach (var key in conditions.Select(e => e.PlayerLanguage).Append(card.RequiresLanguage).OfType<string>())
            {
                if (!languageKeys.Contains(key))
                {
                    errors.Add($"Karte '{id}': Sprache '{key}' gibt es im Katalog nicht.");
                }
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

    private HashSet<string> KeysOf(CardKind kind) =>
        _cards.Where(c => c.Kind == kind).Select(c => c.Key).OfType<string>().ToHashSet();
}
