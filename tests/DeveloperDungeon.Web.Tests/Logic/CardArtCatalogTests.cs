using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Web.Logic;

namespace DeveloperDungeon.Web.Tests.Logic;

public class CardArtCatalogTests
{
    private static readonly CardCatalog Cards = CardCatalog.LoadEmbedded();

    private static CardDefinition Card(string id) => Cards.Get(id);

    [Fact]
    public void KnownCard_UsesOwnArt()
    {
        var art = CardArtCatalog.Parse("""{ "monster-works-on-my-machine": "svg", "equip-visual-studio": "webp" }""");

        Assert.Equal("cards/monster-works-on-my-machine.svg", art.UrlFor(Card("monster-works-on-my-machine")));
        Assert.Equal("cards/equip-visual-studio.webp", art.UrlFor(Card("equip-visual-studio")));
    }

    [Fact]
    public void UnknownCard_UsesPlaceholderByKind()
    {
        var art = CardArtCatalog.Parse("{}");

        Assert.Equal("cards/_placeholder-monster.svg", art.UrlFor(Cards.All.First(c => c.Kind == CardKind.Monster)));
        Assert.Equal("cards/_placeholder-curse.svg", art.UrlFor(Cards.All.First(c => c.Kind == CardKind.Curse)));
        Assert.Equal("cards/_placeholder-ticket.svg", art.UrlFor(Cards.All.First(c => c.Kind == CardKind.Department)));
        Assert.Equal("cards/_placeholder-loot.svg", art.UrlFor(Cards.All.First(c => c.Deck == Deck.Loot)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("[1,2]")]
    public void InvalidJson_YieldsPlaceholdersOnly(string json)
    {
        var art = CardArtCatalog.Parse(json);

        Assert.Equal("cards/_placeholder-monster.svg", art.UrlFor(Card("monster-works-on-my-machine")));
    }

    [Fact]
    public void EveryArtEntry_HasFileOnDisk()
    {
        var wwwroot = FindWwwroot();
        var json = File.ReadAllText(Path.Combine(wwwroot, "cards", "art.json"));
        var art = CardArtCatalog.Parse(json);

        foreach (var card in Cards.All)
        {
            var url = art.UrlFor(card);
            Assert.True(File.Exists(Path.Combine(wwwroot, url)), $"Datei fehlt: {url}");
        }

        foreach (var name in new[] { "monster", "curse", "loot", "ticket" })
        {
            Assert.True(File.Exists(Path.Combine(wwwroot, "cards", $"_placeholder-{name}.svg")), name);
        }

        foreach (var id in System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name))
        {
            Assert.Contains(Cards.All, c => c.Id == id);
        }
    }

    private static string FindWwwroot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "DeveloperDungeon.Web", "wwwroot");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("wwwroot des Web-Projekts nicht gefunden.");
    }
}
