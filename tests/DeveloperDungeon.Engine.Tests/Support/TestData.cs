using DeveloperDungeon.Engine.Cards;

namespace DeveloperDungeon.Engine.Tests.Support;

internal static class TestData
{
    private static string FixtureJson => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestCards.json"));

    private static readonly Lazy<CardCatalog> LazyCatalog = new(() => CardCatalog.FromJson(FixtureJson));

    /// <summary>Zusätzliche Karten mit rein manueller Wirkung; getrennt vom Fixture, damit gesäte Tests unverändert bleiben.</summary>
    private const string ManualCardsJson = """
        [
          { "id": "special-manual", "name": "special-manual", "deck": "ticket", "kind": "special", "flavor": "Test", "rulesText": "Test", "effects": [ { "type": "Manual" } ] },
          { "id": "enhancer-manual", "name": "enhancer-manual", "deck": "ticket", "kind": "monsterEnhancer", "flavor": "Test", "rulesText": "Test", "effects": [ { "type": "Manual" } ] },
          { "id": "levelup-manual", "name": "levelup-manual", "deck": "loot", "kind": "levelUp", "flavor": "Test", "rulesText": "Test", "effects": [ { "type": "Manual" } ] }
        ]
        """;

    private static readonly Lazy<CardCatalog> LazyManualCatalog = new(() => CardCatalog.FromJson(FixtureJson, ManualCardsJson));

    public static CardCatalog Catalog => LazyCatalog.Value;

    /// <summary>Fixture plus <c>special-manual</c>, <c>enhancer-manual</c> und <c>levelup-manual</c>.</summary>
    public static CardCatalog CatalogWithManualCards => LazyManualCatalog.Value;
}
