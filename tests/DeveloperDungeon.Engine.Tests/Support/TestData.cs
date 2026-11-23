using DeveloperDungeon.Engine.Cards;

namespace DeveloperDungeon.Engine.Tests.Support;

internal static class TestData
{
    private static string FixtureJson => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestCards.json"));

    private static readonly Lazy<CardCatalog> LazyCatalog = new(() => CardCatalog.FromJson(FixtureJson));

    /// <summary>Zusätzliche Karten; getrennt vom Fixture, damit gesäte Tests unverändert bleiben.</summary>
    private const string ExtraCardsJson = """
        [
          { "id": "special-manual", "name": "special-manual", "deck": "ticket", "kind": "special", "flavor": "Test", "rulesText": "Test", "effects": [ { "type": "Manual" } ] },
          { "id": "enhancer-manual", "name": "enhancer-manual", "deck": "ticket", "kind": "monsterEnhancer", "flavor": "Test", "rulesText": "Test", "effects": [ { "type": "Manual" } ] },
          { "id": "levelup-manual", "name": "levelup-manual", "deck": "loot", "kind": "levelUp", "flavor": "Test", "rulesText": "Test", "effects": [ { "type": "Manual" } ] },
          { "id": "curse-give-then-lose-hands", "name": "curse-give-then-lose-hands", "deck": "ticket", "kind": "curse", "flavor": "Test", "rulesText": "Test",
            "effects": [ { "type": "GiveEquippedItemToNeighbor", "direction": "left" }, { "type": "LoseSlot", "slot": "hands" } ] },
          { "id": "curse-draw-two", "name": "curse-draw-two", "deck": "ticket", "kind": "curse", "flavor": "Test", "rulesText": "Test",
            "effects": [ { "type": "DrawCards", "deck": "loot", "count": 2 } ] }
        ]
        """;

    private static readonly Lazy<CardCatalog> LazyExtendedCatalog = new(() => CardCatalog.FromJson(FixtureJson, ExtraCardsJson));

    public static CardCatalog Catalog => LazyCatalog.Value;

    /// <summary>Fixture plus die Karten aus <see cref="ExtraCardsJson"/> (manuelle Karten, Fluch mit Geben und Slot-Verlust, Fluch mit Kartenziehen).</summary>
    public static CardCatalog ExtendedCatalog => LazyExtendedCatalog.Value;
}
