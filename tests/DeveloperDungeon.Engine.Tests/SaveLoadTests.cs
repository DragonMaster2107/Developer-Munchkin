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
        var enhancer = game.State.TicketDeck.First(c => c.CardId == "enhancer-plus5");
        game.State.TicketDeck.Remove(enhancer);
        game.State.Combat.Enhancers[5] = [enhancer];
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

    [Fact]
    public void Save_ContainsCurrentSchemaVersion()
    {
        var json = GameBuilder.Create().Build().Save();

        Assert.Contains($"\"schemaVersion\":{GameState.CurrentSchemaVersion}", json);
        Assert.Equal(1, GameState.CurrentSchemaVersion);
    }

    [Fact]
    public void Load_ValidSave_StillLoads()
    {
        var game = GameBuilder.Create(6, 3).ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l1").Build();
        Assert.True(game.Execute(new Commands.OpenTicket("p1")).Accepted);

        var loaded = Game.Load(game.Save(), TestData.Catalog);

        Assert.Equal(game.Save(), loaded.Save());
    }

    private static void AssertRejectedSave(Action<GameState> corrupt)
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l1").Build();
        Assert.True(game.Execute(new Commands.OpenTicket("p1")).Accepted);
        corrupt(game.State);
        var json = game.Save();

        var ex = Assert.Throws<InvalidDataException>(() => Game.Load(json, TestData.Catalog));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Load_UnknownCardIdInDeck_Throws() =>
        AssertRejectedSave(s => s.LootDeck[3].CardId = "unknown-card");

    [Fact]
    public void Load_UnknownCardIdInHand_Throws() =>
        AssertRejectedSave(s => s.Players[1].Hand.Add(new CardInstance { InstanceId = 9001, CardId = "unknown-card" }));

    [Fact]
    public void Load_UnknownCardIdInCombat_Throws() =>
        AssertRejectedSave(s => s.Combat!.Monsters[0].CardId = "unknown-card");

    [Fact]
    public void Load_UnknownCardIdInOneShot_Throws() =>
        AssertRejectedSave(s => s.Combat!.OneShots.Add(new OneShotPlay { Card = new CardInstance { InstanceId = 9002, CardId = "unknown-card" } }));

    [Fact]
    public void Load_DuplicateInstanceId_Throws() =>
        AssertRejectedSave(s => s.Players[0].Backpack.Add(new CardInstance { InstanceId = s.TicketDeck[0].InstanceId, CardId = "equip-body" }));

    [Fact]
    public void Load_DuplicateInstanceIdBetweenDiscardAndCombat_Throws() =>
        AssertRejectedSave(s => s.TicketDiscard.Add(s.Combat!.Monsters[0]));

    [Fact]
    public void Load_WrongSchemaVersion_Throws() =>
        AssertRejectedSave(s => s.SchemaVersion = 99);

    [Fact]
    public void Load_TooFewPlayers_Throws() =>
        AssertRejectedSave(s => s.Players.RemoveAt(2));

    [Fact]
    public void Load_ActivePlayerOutOfRange_Throws() =>
        AssertRejectedSave(s => s.ActivePlayerIndex = 3);

    [Fact]
    public void Load_BrokenJson_ThrowsInvalidData() =>
        Assert.Throws<InvalidDataException>(() => Game.Load("{ nope", TestData.Catalog));

    [Fact]
    public void Load_NullJson_ThrowsInvalidData() =>
        Assert.Throws<InvalidDataException>(() => Game.Load("null", TestData.Catalog));
}
