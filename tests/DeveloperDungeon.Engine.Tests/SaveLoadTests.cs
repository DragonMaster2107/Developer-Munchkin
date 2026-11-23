using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class SaveLoadTests
{
    [Fact]
    public void Roundtrip_ProducesIdenticalJson()
    {
        var game = GameBuilder.Create(4, 11).Phase(GamePhase.Combat).Build();
        game.State.TemporaryEffects.Add(new TemporaryEffect { PlayerId = "p1", CombatModifier = 2, SourceCardId = "x" });
        game.State.Combat = new CombatState();
        game.State.Combat.Enhancers[5] = [new CardInstance { InstanceId = 9, CardId = "enhancer-plus5" }];
        var json = game.Save();

        var loaded = Game.Load(json, TestData.Catalog);

        Assert.Equal(json, loaded.Save());
        Assert.Equal(GamePhase.Combat, loaded.State.Phase);
        Assert.Contains("\"phase\":\"combat\"", json.Replace(" ", ""));
    }

    [Fact]
    public void Load_ContinuesRandomSequence()
    {
        var original = GameBuilder.Create(3, 77).Build();
        var restored = Game.Load(original.Save(), TestData.Catalog);

        foreach (var g in new[] { original, restored })
        {
            g.State.LootDiscard.AddRange(g.State.LootDeck);
            g.State.LootDeck.Clear();
            Decks.Draw(g.State, g.Rng, Deck.Loot);
        }

        Assert.Equal(
            original.State.LootDeck.Select(c => c.InstanceId),
            restored.State.LootDeck.Select(c => c.InstanceId));
        Assert.Equal(original.Save(), restored.Save());
    }
}
