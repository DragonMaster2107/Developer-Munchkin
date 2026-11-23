using DeveloperDungeon.Engine.Cards;

namespace DeveloperDungeon.Engine.Tests.Support;

internal static class TestData
{
    private static readonly Lazy<CardCatalog> LazyCatalog = new(() =>
        CardCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "TestCards.json"))));

    public static CardCatalog Catalog => LazyCatalog.Value;
}
