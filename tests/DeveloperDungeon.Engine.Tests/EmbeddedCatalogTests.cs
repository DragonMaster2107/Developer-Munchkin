using DeveloperDungeon.Engine.Cards;

namespace DeveloperDungeon.Engine.Tests;

public class EmbeddedCatalogTests
{
    private static readonly string[] RequiredIds =
    [
        "monster-works-on-my-machine", "monster-impatient-po", "monster-legacy-2012", "monster-endless-meeting",
        "curse-unlocked-pc", "curse-master-conflict", "curse-pipeline-red", "curse-monday-meeting",
        "dept-cross-platform", "dept-native", "dept-cloud-platform", "dept-team-assistant", "dept-qa", "dept-ai-team",
        "lang-csharp", "lang-kotlin", "lang-python",
        "special-full-stack", "special-polyglot", "special-wandering-bug",
        "equip-rider", "equip-vscode", "equip-visual-studio", "equip-nc-headphones", "equip-gaming-socks", "equip-ultrawide",
        "oneshot-cold-coffee", "levelup-raise", "oneshot-remote-day",
    ];

    [Fact]
    public void EmbeddedCatalog_IsValid() =>
        Assert.Empty(CardCatalog.LoadEmbedded().Validate());

    [Fact]
    public void EmbeddedCatalog_ContainsAllUserRequiredCards()
    {
        var catalog = CardCatalog.LoadEmbedded();
        foreach (var id in RequiredIds)
        {
            Assert.Equal(id, catalog.Get(id).Id);
        }
    }

    [Fact]
    public void EmbeddedCatalog_HasExpectedDeckSizes()
    {
        var catalog = CardCatalog.LoadEmbedded();
        Assert.Equal(33, catalog.All.Where(c => c.Deck == Deck.Ticket).Sum(c => c.Count));
        Assert.Equal(30, catalog.All.Where(c => c.Deck == Deck.Loot).Sum(c => c.Count));
    }

    [Fact]
    public void EmbeddedCatalog_SupportsSixPlayers()
    {
        var players = Enumerable.Range(1, 6).Select(i => new PlayerSetup($"P{i}", "red")).ToList();
        var game = Game.New(players, CardCatalog.LoadEmbedded(), 1);
        Assert.Equal(6, game.State.Players.Count);
    }
}
